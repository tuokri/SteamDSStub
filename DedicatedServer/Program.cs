// See https://aka.ms/new-console-template for more information

using System.Net;
using SteamKit2;
using SteamKit2.Internal;

Console.WriteLine("Hello, World!");

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

const uint appId = 418460;

var isRunning = true;

var lastUpdate = DateTime.UtcNow;

while (isRunning)
{
    manager.RunWaitCallbacks(TimeSpan.FromSeconds(0.5));

    if (DateTime.UtcNow > (lastUpdate + TimeSpan.FromSeconds(60)))
    {
        Console.WriteLine("updating...");
        SendStatusUpdate();
        lastUpdate = DateTime.UtcNow;
    }
}

return 0;

void SendStatusUpdate()
{
    var details = new SteamGameServer.StatusDetails
    {
        AppID = appId,
        ServerFlags = EServerFlags.Active | EServerFlags.Dedicated | EServerFlags.Secure,
        GameDirectory = "RS2",
        // Address = IPAddress.Parse(""),  // Not used by Steam.
        Port = 7777,
        QueryPort = 27015,
        Version = "1091",
    };
    server?.SendStatus(details);

    var gsData = new ClientMsgProtobuf<CMsgGameServerData>(EMsg.AMGameServerUpdate);
    gsData.Body.revision = 17;
    gsData.Body.query_port = 27015;
    gsData.Body.game_port = 7777;
    gsData.Body.server_name = "test server";
    gsData.Body.app_id = appId;
    gsData.Body.product = "RS2";
    gsData.Body.gamedir = "RS2";
    gsData.Body.map = "VNTE-CuChi";
    gsData.Body.os = "w";
    gsData.Body.max_players = 64;
    gsData.Body.version = "1091";
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
        if (callback.Result == EResult.AccountLogonDenied)
        {
            // if we receive AccountLogonDenied or one of it's flavors (AccountLogonDeniedNoMailSent, etc)
            // then the account we're logging into is SteamGuard protected
            // see sample 5 for how SteamGuard can be handled

            Console.WriteLine("unable to logon to Steam: This account is SteamGuard protected.");

            isRunning = false;
            return;
        }

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

internal class DebugLogListener : IDebugListener
{
    public void WriteLine(string category, string msg)
    {
        Console.WriteLine("DebugLog: {0}: {1}", category, msg);
    }
}
