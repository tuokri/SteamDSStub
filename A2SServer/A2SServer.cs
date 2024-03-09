/*
 * Copyright (C) 2023-2024  Tuomo Kriikkula
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using SuperSocket.ProtoBase;
using System.Buffers;
using System.Text;

namespace A2SServer;

public static class Constants
{
    public const int A2SPrefix = -1; // 0xFFFFFFFF.
    public const string A2SQueryStr = "Source Engine Query\0";
    public const byte A2SInfoRequestHeader = 0x54;
    public const byte A2SPlayerRequestHeader = 0x55;
    public const byte A2SRulesRequestHeader = 0x56;
    public const byte A2SChallengeResponseHeader = 0x41;
    public const byte A2SInfoResponseHeader = 0x49;
    public const byte A2SPlayerResponseHeader = 0x44;
    public const byte A2SRulesResponseHeader = 0x45;
    public const byte EdfPort = 0x80;
    public const byte EdfSteamId = 0x10;
    public const byte EdfKeywords = 0x20;
    public const byte EdfGameId = 0x01;
}

public class A2SRequestPackage
{
    public byte Header { get; set; }
    public int Challenge { get; set; }
}

public class A2SPackageDecoder : IPackageDecoder<A2SRequestPackage>
{
    private static bool CheckInfoHeader(ref SequenceReader<byte> reader)
    {
        var queryStr = reader.ReadString(Constants.A2SQueryStr.Length);
        if (queryStr is not Constants.A2SQueryStr)
        {
            throw new ProtocolException("invalid query string");
        }

        return true;
    }

    public A2SRequestPackage Decode(ref ReadOnlySequence<byte> buffer, object context)
    {
        var reader = new SequenceReader<byte>(buffer);
        if (!reader.TryReadLittleEndian(out int prefix))
        {
            throw new ProtocolException("failed to read prefix");
        }

        if (prefix != Constants.A2SPrefix)
        {
            throw new ProtocolException($"invalid prefix: 0x{prefix:x}");
        }

        var package = new A2SRequestPackage();

        reader.TryRead(out byte header);
        package.Header = header;

        var validHeader = header switch
        {
            Constants.A2SInfoRequestHeader => CheckInfoHeader(ref reader),
            Constants.A2SPlayerRequestHeader => true,
            Constants.A2SRulesRequestHeader => true,
            _ => false
        };
        if (!validHeader)
        {
            throw new ProtocolException($"invalid header: 0x{header:x}");
        }

        // Challenge is not always included in the end of the request.
        // If the request ends here, assume challenge is -1.
        var challenge = -1;
        if (!reader.End && !reader.TryReadLittleEndian(out challenge))
        {
            throw new ProtocolException(
                $"failed to read challenge, " +
                $"length: {reader.Length}, consumed: {reader.Consumed}, " +
                $"remaining: {reader.Remaining}");
        }

        package.Challenge = challenge;

        return package;
    }
}

public class PlayerInfo
{
    public string Name = "";
    public int Score = 0;
    public float Duration = 0;
}

public class Info
{
    public byte Protocol = 0;
    public string ServerName = "\0";
    public string Map = "\0";
    public string GameDir = "\0";
    public string GameName = "\0";
    public short AppId = 0; // Unused, use GameId instead.
    public byte Players = 0;
    public byte MaxPlayers = 0;
    public byte NumBots = 0;
    public byte ServerType = 0;
    public byte OperatingSystem = (byte)'w';
    public byte PasswordProtected = 0;
    public byte Secure = 0;
    public string Version = "0\0";

    // Extra data, indicated by the EDF flag.
    public ushort Port = 0;
    public long SteamId = 0;
    public string Keywords = "\0";
    public long GameId = 0;
}

// TODO: better name?
public static class Utils
{
    public static byte[] MakeInfoResponsePacket(Info info)
    {
        var basicInfo = new byte[]
        {
            0xff,
            0xff,
            0xff,
            0xff,
            Constants.A2SInfoResponseHeader,
            info.Protocol,
        };

        var bytes = basicInfo
            .Concat(Encoding.ASCII.GetBytes(info.ServerName + '\0'))
            .Concat(Encoding.ASCII.GetBytes(info.Map + '\0'))
            .Concat(Encoding.ASCII.GetBytes(info.GameDir + '\0'))
            .Concat(Encoding.ASCII.GetBytes(info.GameName + '\0'))
            .Concat(BitConverter.GetBytes(info.AppId));

        var moreInfo = new[]
        {
            info.Players,
            info.MaxPlayers,
            info.NumBots,
            info.ServerType,
            info.OperatingSystem,
            info.PasswordProtected,
            info.Secure,
        };

        bytes = bytes.Concat(moreInfo).Concat(Encoding.ASCII.GetBytes(info.Version + '\0'));

        const byte edf = Constants.EdfPort | Constants.EdfSteamId | Constants.EdfKeywords |
                         Constants.EdfGameId;
        bytes = bytes.Concat(new[] { edf });

        bytes = bytes.Concat(BitConverter.GetBytes(info.Port))
            .Concat(BitConverter.GetBytes(info.SteamId))
            .Concat(Encoding.ASCII.GetBytes(info.Keywords + '\0'))
            .Concat(BitConverter.GetBytes(info.GameId));

        return bytes.ToArray();
    }

    public static byte[] MakePlayerResponsePacket(List<PlayerInfo> players)
    {
        var data = new byte[]
        {
            0xff,
            0xff,
            0xff,
            0xff,
            Constants.A2SPlayerResponseHeader,
            (byte)players.Count,
        };

        List<byte> playerData = [];
        foreach (var player in players)
        {
            playerData.Add(0); // Index seems to always be 0?
            playerData.AddRange(Encoding.ASCII.GetBytes(player.Name + '\0'));
            playerData.AddRange(BitConverter.GetBytes(player.Score));
            playerData.AddRange(BitConverter.GetBytes(player.Duration));
        }

        return data.Concat(playerData).ToArray();
    }

    public static byte[] MakeRulesResponsePacket(Dictionary<string, string> rules)
    {
        var data = new byte[]
        {
            0xff,
            0xff,
            0xff,
            0xff,
            Constants.A2SRulesResponseHeader,
        };

        var rulesLen = (short)rules.Count;

        List<byte> rulesData = [];
        foreach (var kv in rules)
        {
            rulesData.AddRange(Encoding.ASCII.GetBytes(kv.Key + '\0'));
            rulesData.AddRange(Encoding.ASCII.GetBytes(kv.Value + '\0'));
        }

        return data.Concat(BitConverter.GetBytes(rulesLen)).Concat(rulesData).ToArray();
    }

    public static byte[] MakeChallengeResponsePacket(int challenge)
    {
        var data = new byte[]
        {
            0xff,
            0xff,
            0xff,
            0xff,
            Constants.A2SChallengeResponseHeader,
            0x00,
            0x00,
            0x00,
            0x00
        };

        var challengeBytes = BitConverter.GetBytes(challenge);
        data[5] = challengeBytes[0];
        data[6] = challengeBytes[1];
        data[7] = challengeBytes[2];
        data[8] = challengeBytes[3];

        return data;
    }
}

public class A2SPipelineFilter : PipelineFilterBase<A2SRequestPackage>
{
    public override A2SRequestPackage Filter(ref SequenceReader<byte> reader)
    {
        // A2S requests should never exceed 29 bytes.
        if (reader.Length is < 5 or > 30)
        {
            return null!;
        }

        var pack = reader.Sequence;
        try
        {
            return DecodePackage(ref pack);
        }
        finally
        {
            reader.Advance(reader.Length);
        }
    }
}
