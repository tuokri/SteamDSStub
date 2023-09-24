using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using NetCoreServer;

namespace A2SServer;

public class Info
{
    public byte Protocol = 0;
    public string Name = "\0";
    public string Map = "\0";
    public string GameDir = "\0";
    public string GameName = "\0";
    public short AppId = 0;
    public byte Players = 0;
    public byte MaxPlayers = 0;
    public byte NumBots = 0;
    public byte ServerType = 0;
    public byte OperatingSystem = (byte)'w';
    public byte PasswordProtected = 0;
    public byte Secure = 0;
    public string Version = "0\0";

    // Extra data.
    public ushort Port = 0;
    public long SteamId = 0;
    public string Keywords = "\0";
    public long GameId = 0;
}

public class A2SServer : UdpServer
{
    private readonly byte[] _prefix = { 0xff, 0xff, 0xff, 0xff };
    private const string QueryStr = "Source Engine Query\0";

    public Info Info { get; set; } = new();
    public Dictionary<string, string> Rules { get; set; } = new() { ["\0"] = "\0" };
    // public List<Dictionary<string, string>> Players { get; set; } = new();

    private readonly int _challenge = 0;

    public A2SServer(IPAddress address, int port) : base(address, port)
    {
        _challenge = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
    }

    protected override void OnStarted()
    {
        ReceiveAsync();
    }

    protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
    {
        Console.WriteLine(
            $"OnReceived: {endpoint}: {size}: " +
            $"{Encoding.ASCII.GetString(buffer, (int)offset, (int)size)}");

        // A2S requests should never exceed 29 bytes.
        if (size is < 5 or > 30)
        {
            // Console.WriteLine("bad size");
            return;
        }

        var prefix = buffer.Take(4);
        if (!prefix.SequenceEqual(_prefix))
        {
            // Console.WriteLine("bad prefix");
            return;
        }

        var ok = false;
        var sendSize = 0;
        var sendBuffer = Array.Empty<byte>();

        var header = buffer[4];
        // Console.WriteLine($"header: {header}");

        try
        {
            ok = header switch
            {
                0x54 => HandleInfoRequest(ref buffer, size, out sendBuffer, out sendSize),
                0x55 => HandlePlayerRequest(ref buffer, size, out sendBuffer, out sendSize),
                0x56 => HandleRulesRequest(ref buffer, size, out sendBuffer, out sendSize),
                _ => false
            };
        }
        catch (Exception e)
        {
            Console.WriteLine($"error handling request from: {endpoint}: {e}");
            ok = false;
        }

        if (ok)
        {
            Console.WriteLine($"responding to 0x{header:X} request from {endpoint}: {sendSize}");
            SendAsync(endpoint, sendBuffer, 0, sendSize);
        }
        else
        {
            OnSent(endpoint, 0);
        }
    }

    private bool HandleInfoRequest(ref byte[] buffer, long bufferSize, out byte[] sendBuffer,
        out int sendSize)
    {
        if (bufferSize >= 24)
        {
            var payload = new ArraySegment<byte>(buffer, 5, 20);
            var ascii = Encoding.ASCII.GetString(payload);
            if (ascii.SequenceEqual(QueryStr))
            {
                // Console.WriteLine("ok...");

                var basicInfo = new byte[]
                {
                    0xff,
                    0xff,
                    0xff,
                    0xff,
                    0x49,
                    Info.Protocol,
                };

                var bytes = basicInfo.Concat(Encoding.ASCII.GetBytes(Info.Name + '\0'))
                    .Concat(Encoding.ASCII.GetBytes(Info.Map + '\0'))
                    .Concat(Encoding.ASCII.GetBytes(Info.GameDir + '\0'))
                    .Concat(Encoding.ASCII.GetBytes(Info.GameName + '\0'))
                    .Concat(BitConverter.GetBytes(Info.AppId));

                var moreInfo = new[]
                {
                    Info.Players,
                    Info.MaxPlayers,
                    Info.NumBots,
                    Info.ServerType,
                    Info.OperatingSystem,
                    Info.PasswordProtected,
                    Info.Secure,
                };

                bytes = bytes.Concat(moreInfo).Concat(Encoding.ASCII.GetBytes(Info.Version + '\0'));

                const byte edf = 0x80 | 0x10 | 0x20 | 0x01;
                bytes = bytes.Concat(new[] { edf });

                bytes = bytes.Concat(BitConverter.GetBytes(Info.Port))
                    .Concat(BitConverter.GetBytes(Info.SteamId))
                    .Concat(Encoding.ASCII.GetBytes(Info.Keywords + '\0'))
                    .Concat(BitConverter.GetBytes(Info.GameId));

                sendBuffer = bytes.ToArray();
                sendSize = sendBuffer.Length;
                return true;
            }
        }

        sendBuffer = Array.Empty<byte>();
        sendSize = 0;
        return false;
    }

    private bool HandlePlayerRequest(ref byte[] buffer, long bufferSize, out byte[] sendBuffer,
        out int sendSize)
    {
        // Console.WriteLine($"HandlePlayerRequest: {bufferSize}");

        if (bufferSize == 9)
        {
            var challengeOk = CheckChallenge(ref buffer);

            // Console.WriteLine($"challenge: {challenge:X} challengeOk: {challengeOk}");

            IEnumerable<byte> allData;

            if (!challengeOk)
            {
                allData = MakeChallengePacket();
            }
            else
            {
                var data = new byte[]
                {
                    0xff,
                    0xff,
                    0xff,
                    0xff,
                    0x44,
                    0x00,
                };

                allData = data;
            }

            sendBuffer = allData.ToArray();
            sendSize = sendBuffer.Length;
            return true;
        }

        sendBuffer = Array.Empty<byte>();
        sendSize = 0;
        return false;
    }

    private bool HandleRulesRequest(ref byte[] buffer, long bufferSize, out byte[] sendBuffer,
        out int sendSize)
    {
        if (bufferSize == 9)
        {
            var challengeOk = CheckChallenge(ref buffer);

            // Console.WriteLine($"challenge: {challenge:X} challengeOk: {challengeOk}");

            IEnumerable<byte> allData;

            if (!challengeOk)
            {
                allData = MakeChallengePacket();
            }
            else
            {
                var data = new byte[]
                {
                    0xff,
                    0xff,
                    0xff,
                    0xff,
                    0x45,
                    (byte)Rules.Count,
                };

                allData = Rules.Aggregate<KeyValuePair<string, string>, IEnumerable<byte>>(data,
                    (current, kv) => current.Concat(Encoding.ASCII.GetBytes(kv.Key + '\0'))
                        .Concat(Encoding.ASCII.GetBytes(kv.Value + '\0')));
            }

            sendBuffer = allData.ToArray();
            sendSize = sendBuffer.Length;
            return true;
        }

        sendBuffer = Array.Empty<byte>();
        sendSize = 0;
        return false;
    }

    protected override void OnSent(EndPoint endpoint, long sent)
    {
        ReceiveAsync();
    }

    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"A2SServer OnError: {error}");
        ReceiveAsync();
    }

    private IEnumerable<byte> MakeChallengePacket()
    {
        var data = new byte[]
        {
            0xff,
            0xff,
            0xff,
            0xff,
            0x41,
        };

        var allData = data.Concat(BitConverter.GetBytes(_challenge));
        return allData.ToArray();
    }

    private bool CheckChallenge(ref byte[] buffer)
    {
        var payload = new ArraySegment<byte>(buffer, 5, 4);
        var challenge = BitConverter.ToInt32(payload);

        if (challenge is -1 or 0)
        {
            return false;
        }

        return challenge == _challenge;
    }
}
