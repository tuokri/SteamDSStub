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

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using A2SServer;
using Microsoft.Extensions.Hosting;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Host;
using Tomlyn;
using Tomlyn.Model;
using Timer = System.Timers.Timer;

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

var playersTable = (TomlTable)cfg["players"];
var playersStrategy = (string)playersTable["strategy"]; // TODO
var playersGame = (string)playersTable["game"]; // TODO
var playerNames =
    (from name in (TomlArray)playersTable["names"] select (string)name).ToList();

const int startScoreMin = -50;
const int startScoreMax = 250;
const float startDurationMinSeconds = 0;
const float startDurationMaxSeconds = 60;

var simPlayers = new List<PlayerInfo>();

var scoreDeltas = new WeightedRandomBag<int>(
[
    new Tuple<int, float>(-5, 0.1f),
    new Tuple<int, float>(-2, 0.1f),
    new Tuple<int, float>(0, 0.2f),
    new Tuple<int, float>(2, 0.2f),
    new Tuple<int, float>(5, 0.5f),
    new Tuple<int, float>(10, 0.2f),
    new Tuple<int, float>(15, 0.1f),
]);
const float timerIntervalSecs = 5;
float roundTime = 0;
var rng = new Random();
var maxRoundTime = RandFloat(ref rng, 1800, 3600);
Console.WriteLine($"using maxRoundTime: {maxRoundTime}");
var playerUpdateTimer =
    new Timer(TimeSpan.FromSeconds(timerIntervalSecs).TotalMilliseconds);
playerUpdateTimer.Elapsed += (_, _) =>
{
    roundTime += timerIntervalSecs;

    if (roundTime > maxRoundTime)
    {
        Console.WriteLine("resetting player simulation round");

        roundTime = 0;
        // Reset player scores and play times.
        for (var i = 0; i < simPlayers.Count; ++i)
        {
            simPlayers[i].Score = rng.Next(startScoreMin, startScoreMax);
            simPlayers[i].Duration =
                RandFloat(ref rng, startDurationMinSeconds, startDurationMaxSeconds);
        }

        return;
    }

    for (var i = 0; i < simPlayers.Count; ++i)
    {
        simPlayers[i].Score += scoreDeltas.GetRandom();
        simPlayers[i].Duration += timerIntervalSecs;
    }
};
playerUpdateTimer.AutoReset = true;
playerUpdateTimer.Enabled = true;

// Get random match duration.
// Set game players from pool of names.
// Set random starting times for players in range.
// Increment times every x seconds.
// Adjust player scores semi randomly every x seconds.

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

var challengeUpdateTimer = new Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
challengeUpdateTimer.Elapsed += (_, _) =>
{
    challenge = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
    Console.WriteLine($"updated challenge: 0x{challenge:x}");
};
challengeUpdateTimer.AutoReset = true;
challengeUpdateTimer.Enabled = true;

var socketHost = SuperSocketHostBuilder
    .Create<A2SRequestPackage, A2SPipelineFilter>()
    .UseUdp()
    .UsePackageDecoder<A2SPackageDecoder>()
    .UsePackageHandler(async (s, p) =>
    {
        if (p.Challenge != challenge)
        {
            await s.SendAsync(Utils.MakeChallengeResponsePacket(challenge));
            return;
        }

        var response = p.Header switch
        {
            Constants.A2SInfoRequestHeader => Utils.MakeInfoResponsePacket(info),
            Constants.A2SRulesRequestHeader => Utils.MakeRulesResponsePacket(rules),
            Constants.A2SPlayerRequestHeader => Utils.MakePlayerResponsePacket(simPlayers),
            _ => throw new ProtocolException($"invalid header: 0x{p.Header:x}")
        };

        Console.WriteLine($"responding to 0x{p.Header:x} " +
                          $"request from {s.Connection.RemoteEndPoint} {response.Length}");
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

static float RandFloat(ref Random rng, float min, float max)
{
    return (float)rng.NextDouble() * (max - min) + min;
}