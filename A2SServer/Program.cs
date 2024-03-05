using A2SServer;
using Microsoft.Extensions.Hosting;
using SuperSocket.ProtoBase;
using SuperSocket;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography;
using Tomlyn.Model;
using Tomlyn;


var fileName = args[0];
Console.WriteLine($"reading config from '{fileName}'");
var cfg = Toml.ToModel(File.ReadAllText(fileName));
var serverTable = (TomlTable)cfg["server"];

var host = (string)serverTable["host"];
var gamePort = Convert.ToUInt16((long)serverTable["gameport"]);
var queryPort = Convert.ToUInt16((long)serverTable["queryport"]);
var appId = (long)serverTable["appid"];
var gameDir = (string)serverTable["gamedir"];
var version = (string)serverTable["version"];
var serverName = (string)serverTable["server_name"];
var gameName = (string)serverTable["game_name"];
var map = (string)serverTable["map"];
var protocol = Convert.ToByte((long)serverTable["protocol"]);
var players = Convert.ToByte((long)serverTable["players"]);
var maxPlayers = Convert.ToByte((long)serverTable["max_players"]);
var numBots = Convert.ToByte((long)serverTable["num_bots"]);
var os = (byte)((string)serverTable["os"])[0];
var serverType = (byte)((string)serverTable["server_type"])[0];
var secure = Convert.ToByte((bool)serverTable["secure"]);
var keywords = (string)serverTable["keywords"];
var steamId = (long)serverTable["steamid"];
var passwordProtected = Convert.ToByte((bool)serverTable["password_protected"]);

var rulesTable = (TomlTable)cfg["rules"];
var rules = rulesTable.ToDictionary(
    kv => kv.Key, kv => (string)kv.Value);

Console.WriteLine($"starting A2S server on '{host}:{queryPort}'");

var addr = host is "" or "0.0.0.0"
    ? IPAddress.Any
    : Dns.GetHostAddresses(host, AddressFamily.InterNetwork)[0];

Console.WriteLine($"resolved '{host}' to '{addr}'");

var info = new Info
{
    Protocol = protocol,
    ServerName = serverName,
    Map = map,
    GameDir = gameDir,
    GameName = gameName,
    AppId = 0,
    Players = players,
    MaxPlayers = maxPlayers,
    NumBots = numBots,
    ServerType = serverType,
    OperatingSystem = os,
    PasswordProtected = passwordProtected,
    Secure = secure,
    Version = version,
    Port = gamePort,
    SteamId = steamId,
    Keywords = keywords,
    GameId = appId
};

var challenge = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);

Console.WriteLine($"generated challenge: 0x{challenge:x}");

var socketHost = SuperSocketHostBuilder
    .Create<A2SRequestPackage, A2SPipelineFilter>()
    .UseUdp()
    .UsePackageDecoder<A2SPackageDecoder>()
    .UsePackageHandler(async (s, p) =>
    {
        if (p.Challenge != challenge)
        {
            // Send challenge response.
            await s.SendAsync(Utils.MakeChallengeResponsePacket(challenge));
            return;
        }

        var response = p.Header switch
        {
            Constants.A2SInfoRequestHeader => Utils.MakeInfoResponsePacket(ref info),
            Constants.A2SRulesRequestHeader => Utils.MakeRulesResponsePacket(ref rules),
            Constants.A2SPlayerRequestHeader => Utils.MakePlayerResponsePacket(),
            _ => throw new ProtocolException($"invalid header: 0x{p.Header:x}")
        };

        Console.WriteLine($"responding to 0x{p.Header:x} request from {s.Channel.RemoteEndPoint}");
        await s.SendAsync(response);
    }).ConfigureSuperSocket(options =>
    {
        options.Name = "A2SServer";
        options.Listeners =
        [
            new ListenOptions
            {
                Ip = addr.ToString(),
                Port = queryPort
            }
        ];
    }).Build();

await socketHost.RunAsync();

Console.WriteLine("stopping");

return 0;
