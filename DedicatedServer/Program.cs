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

DebugLog.AddListener(new DebugLogListener());
DebugLog.Enabled = true;

var client = new SteamClient();
var manager = new CallbackManager(client);

client.DebugNetworkListener = new NetHookNetworkListener();

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
        Console.WriteLine("updating...");
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
    Console.WriteLine("disconnected from Steam: {0}", callback.UserInitiated);
    isRunning = false;
}

void OnLoggedOn(SteamUser.LoggedOnCallback callback)
{
    if (callback.Result != EResult.OK)
    {
        Console.WriteLine("unable to logon to Steam: {0} / {1}", callback.Result,
            callback.ExtendedResult);

        isRunning = false;
        return;
    }

    Console.WriteLine("successfully logged on!");

    SendStatusUpdate();
}

void OnLoggedOff(SteamUser.LoggedOffCallback callback)
{
    Console.WriteLine("logged off of Steam: {0}", callback.Result);
}

void OnStatusReply(SteamGameServer.StatusReplyCallback callback)
{
    Console.WriteLine("StatusReplyCallback: IsSecure={0}", callback.IsSecure);
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
        Console.WriteLine("DebugLog: {0}: {1}", category, msg);
    }
}
