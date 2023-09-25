using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using Tomlyn.Model;
using Tomlyn;

PosixSignalRegistration.Create(PosixSignal.SIGINT, HandleSignal);
PosixSignalRegistration.Create(PosixSignal.SIGTERM, HandleSignal);

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

var addr = host == ""
    ? IPAddress.Any
    : Dns.GetHostAddresses(host, AddressFamily.InterNetwork)[0];

Console.WriteLine($"resolved '{host}' to '{addr}'");

var isRunning = true;

var server = new A2SServer.A2SServer(addr, queryPort);
server.OptionReuseAddress = true;
server.OptionReceiveBufferLimit = 100;
server.OptionSendBufferLimit = 3000;

server.Info.Protocol = protocol;
server.Info.ServerName = serverName;
server.Info.Map = map;
server.Info.GameDir = gameDir;
server.Info.GameName = gameName;
server.Info.AppId = 0;
server.Info.Players = players;
server.Info.MaxPlayers = maxPlayers;
server.Info.NumBots = numBots;
server.Info.ServerType = serverType;
server.Info.OperatingSystem = os;
server.Info.PasswordProtected = passwordProtected;
server.Info.Secure = secure;
server.Info.Version = version;
server.Info.Port = gamePort;
server.Info.SteamId = steamId;
server.Info.Keywords = keywords;
server.Info.GameId = appId;

server.Rules = rules;

server.Start();

while (isRunning)
{
    Thread.Sleep(100);
}

Console.WriteLine("stopping");

server.Stop();

return 0;

void HandleSignal(PosixSignalContext context)
{
    Console.WriteLine($"got signal: {context.Signal}");
    isRunning = false;
}
