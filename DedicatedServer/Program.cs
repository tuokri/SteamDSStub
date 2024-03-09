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

using SteamKit2.Internal;
using SteamKit2;
using System.Runtime.InteropServices;
using Tomlyn.Model;
using Tomlyn;

Console.WriteLine("starting Steam DS");

PosixSignalRegistration.Create(PosixSignal.SIGINT, HandleSignal);
PosixSignalRegistration.Create(PosixSignal.SIGTERM, HandleSignal);

var fileName = args[0];
Console.WriteLine($"reading config from '{fileName}'");
var cfg = Toml.ToModel(File.ReadAllText(fileName));
var serverTable = (TomlTable)cfg["server"];

var appId = Convert.ToUInt32((long)serverTable["appid"]);
var gamePort = Convert.ToUInt16((long)serverTable["gameport"]);
var queryPort = Convert.ToUInt16((long)serverTable["queryport"]);
var gameDir = (string)serverTable["gamedir"];
var version = (string)serverTable["version"];
var serverName = (string)serverTable["server_name"];
var serverMap = (string)serverTable["map"];

var client = new SteamClient();
var manager = new CallbackManager(client);

#if DEBUG
DebugLog.AddListener(new DebugLogListener());
DebugLog.Enabled = true;
client.DebugNetworkListener = new NetHookNetworkListener();
#endif

var server = client.GetHandler<SteamGameServer>();

if (server == null)
{
    Console.WriteLine("no server");
    return 1;
}

manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);

manager.Subscribe<SteamGameServer.StatusReplyCallback>(OnStatusReply);
manager.Subscribe<SteamGameServer.TicketAuthCallback>(OnTicketAuth);

client.Connect();

var isRunning = true;

var lastUpdate = DateTime.UtcNow;

while (isRunning)
{
    manager.RunWaitCallbacks(TimeSpan.FromSeconds(0.5));

    if (DateTime.UtcNow > (lastUpdate + TimeSpan.FromSeconds(60)))
    {
        Console.WriteLine("updating status...");
        Console.WriteLine($"Public IP: {client.PublicIP}");
        SendStatusUpdate();
        lastUpdate = DateTime.UtcNow;
    }
}

Console.WriteLine("stopping");

return 0;

void SendStatusUpdate()
{
    var details = new SteamGameServer.StatusDetails
    {
        AppID = appId,
        ServerFlags = EServerFlags.Active | EServerFlags.Dedicated | EServerFlags.Secure,
        GameDirectory = gameDir,
        // Address = IPAddress.Parse(""),  // Not used by Steam.
        Port = gamePort,
        QueryPort = queryPort,
        Version = version,
    };
    server.SendStatus(details);

    var gsData = new ClientMsgProtobuf<CMsgGameServerData>(EMsg.AMGameServerUpdate);
    gsData.Body.revision = 17;
    gsData.Body.query_port = queryPort;
    gsData.Body.game_port = gamePort;
    gsData.Body.server_name = serverName;
    gsData.Body.app_id = appId;
    gsData.Body.product = gameDir;
    gsData.Body.gamedir = gameDir;
    gsData.Body.map = serverMap;
    gsData.Body.os = "w";
    gsData.Body.max_players = 64;
    gsData.Body.version = version;
    gsData.Body.dedicated = true;
    gsData.Body.region = "255";

    client.Send(gsData);
}

void OnConnected(SteamClient.ConnectedCallback callback)
{
    Console.WriteLine("connected to Steam");
    server.LogOnAnonymous(appId);
}

void OnDisconnected(SteamClient.DisconnectedCallback callback)
{
    Console.WriteLine($"disconnected from Steam: {callback.UserInitiated}");
    isRunning = false;
}

void OnLoggedOn(SteamUser.LoggedOnCallback callback)
{
    if (callback.Result != EResult.OK)
    {
        Console.WriteLine(
            $"unable to logon to Steam: {callback.Result} / {callback.ExtendedResult}");

        isRunning = false;
        return;
    }

    Console.WriteLine("successfully logged on!");

    SendStatusUpdate();
}

void OnLoggedOff(SteamUser.LoggedOffCallback callback)
{
    Console.WriteLine($"logged off of Steam: {callback.Result}");
}

void OnStatusReply(SteamGameServer.StatusReplyCallback callback)
{
    Console.WriteLine($"StatusReplyCallback: IsSecure={callback.IsSecure}");
}

void OnTicketAuth(SteamGameServer.TicketAuthCallback callback)
{
    Console.WriteLine(
        $"TicketAuthCallback: GameID={callback.GameID} " +
        $"SteamID={callback.SteamID} " +
        $"State={callback.State}" +
        $"AuthSessionResponse={callback.AuthSessionResponse} " +
        $"TicketCRC={callback.TicketCRC} " +
        $"TicketSequence={callback.TicketSequence}"
    );
}

void HandleSignal(PosixSignalContext context)
{
    Console.WriteLine($"got signal: {context.Signal}");
    isRunning = false;
}

internal class DebugLogListener : IDebugListener
{
    public void WriteLine(string category, string msg)
    {
        Console.WriteLine($"DebugLog: {category}: {msg}");
    }
}
