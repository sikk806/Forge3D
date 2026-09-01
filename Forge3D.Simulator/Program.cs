using Forge3D.Simulator.Hosting;
using Forge3D.Simulator.Networking;
using System.Net;

var host = new SimulationHost();
host.ResetToDropScenario();

Console.WriteLine("Forge3D Simulator");
Console.WriteLine($"Ready. Bodies: {host.World.Bodies.Count}, Colliders: {host.World.Colliders.Count}");

if (args.Any(arg => string.Equals(arg, "--tcp", StringComparison.OrdinalIgnoreCase)))
{
    var port = ReadPort(args);
    await using var server = new TcpSimulationServer(host, IPAddress.Loopback, port);
    await server.StartAsync();
    Console.WriteLine($"TCP server listening on {server.LocalEndPoint}");
    Console.WriteLine("Press Ctrl+C to stop.");

    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        completion.TrySetResult();
    };

    await completion.Task;
}

static int ReadPort(string[] args)
{
    const int defaultPort = 47320;
    var portArg = args.FirstOrDefault(arg => arg.StartsWith("--port=", StringComparison.OrdinalIgnoreCase));
    if (portArg is null)
    {
        return defaultPort;
    }

    return int.TryParse(portArg["--port=".Length..], out var port) && port is > 0 and <= 65535
        ? port
        : defaultPort;
}
