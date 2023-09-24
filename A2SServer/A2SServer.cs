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
    public short Port = 0;
    public long SteamId = 0;
    public string Keywords = "\0";
    public long GameId = 0;
}

public class A2SServer : UdpServer
{
    private readonly byte[] _prefix = { 0xff, 0xff, 0xff, 0xff };
    private const string QueryStr = "Source Engine Query\0";

    public Info Info { get; set; } = new();
    public Dictionary<string, string> Rules { get; set; } = new();
    // public List<Dictionary<string, string>> Players { get; set; } = new();

    private long _challenge = 0;

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
        Console.WriteLine("Incoming: " + Encoding.UTF8.GetString(buffer, (int)offset, (int)size));

        if (size < 5)
        {
            return;
        }

        // A2S requests should never exceed 29.
        if (size > 30)
        {
            return;
        }

        var prefix = buffer.Take(4);
        if (!prefix.SequenceEqual(_prefix))
        {
            Console.WriteLine("bad prefix");
            return;
        }

        bool ok;
        int sendSize;
        byte[] sendBuffer;

        var header = buffer[4];
        Console.WriteLine($"header: {header}");

        try
        {
            switch (header)
            {
                case 0x54:
                    ok = HandleInfo(ref buffer, out sendBuffer, out sendSize);
                    break;
                case 0x55:
                    ok = HandlePlayers(ref buffer, out sendBuffer, out sendSize);
                    break;
                case 0x56:
                    ok = HandleRules(ref buffer, out sendBuffer, out sendSize);
                    break;
                default:
                    return;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"error handling request from: {endpoint}: {e}");
            return;
        }

        if (!ok) return;

        Console.WriteLine($"responding to 0x{header:X} request from {endpoint}");
        SendAsync(endpoint, sendBuffer, 0, sendSize);
    }

    private bool HandleInfo(ref byte[] buffer, out byte[] sendBuffer, out int sendSize)
    {
        if (buffer.Length >= 24)
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

                var bytes = basicInfo.Concat(Encoding.ASCII.GetBytes(Info.Name))
                    .Concat(Encoding.ASCII.GetBytes(Info.Map))
                    .Concat(Encoding.ASCII.GetBytes(Info.GameDir))
                    .Concat(Encoding.ASCII.GetBytes(Info.GameName))
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

                bytes = bytes.Concat(moreInfo).Concat(Encoding.ASCII.GetBytes(Info.Version));

                const byte edf = 0x80 | 0x10 | 0x20 | 0x01;
                bytes = bytes.Concat(new[] { edf });

                bytes = bytes.Concat(BitConverter.GetBytes(Info.Port))
                    .Concat(BitConverter.GetBytes(Info.SteamId))
                    .Concat(Encoding.ASCII.GetBytes(Info.Keywords))
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

    private bool HandlePlayers(ref byte[] buffer, out byte[] sendBuffer, out int sendSize)
    {
        if (buffer.Length == 9)
        {
        }

        sendBuffer = Array.Empty<byte>();
        sendSize = 0;
        return false;
    }

    private bool HandleRules(ref byte[] buffer, out byte[] sendBuffer, out int sendSize)
    {
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
        Console.WriteLine($"Echo UDP server caught an error with code {error}");
    }
}
