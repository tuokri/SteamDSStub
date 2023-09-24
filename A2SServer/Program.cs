using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;

PosixSignalRegistration.Create(PosixSignal.SIGINT, HandleSignal);
PosixSignalRegistration.Create(PosixSignal.SIGTERM, HandleSignal);

var host = Environment.GetEnvironmentVariable("HOST") ?? "";

var success = int.TryParse(Environment.GetEnvironmentVariable("QUERYPORT"), out var queryPort);
if (!success)
{
    Console.WriteLine("WARNING, failed to parse queryPort");
}

Console.WriteLine($"starting A2S server on '{host}:{queryPort}'");

var addr = host == "" ? IPAddress.Any : Dns.GetHostAddresses(host, AddressFamily.InterNetwork)[0];

Console.WriteLine($"resolved '{host}' to '{addr}'");

var isRunning = true;

var server = new A2SServer.A2SServer(addr, queryPort);
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
