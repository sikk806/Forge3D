using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.Json;
using Forge3D.Contracts.Commands;
using Forge3D.Contracts.Connection;
using Forge3D.Contracts.States;
using Forge3D.Simulator.Hosting;
using Forge3D.Simulator.Networking;

namespace Forge3D.Tests.Simulator;

public sealed class TcpSimulationServerTests
{
    [Fact]
    public async Task Server_StreamsSnapshotsAndAcceptsCommands()
    {
        var host = new SimulationHost();
        await using var server = new TcpSimulationServer(host, IPAddress.Loopback, 0);
        await server.StartAsync();

        using var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(server.LocalEndPoint.Address, server.LocalEndPoint.Port);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream);
        await using var writer = new StreamWriter(stream);

        var initial = await ReadSnapshotAsync(reader);
        Assert.NotNull(initial);

        var command = new AddObstacleCommand(
            "Network_Obstacle",
            new Vector3(1.0f, 0.5f, 1.0f),
            new Vector3(0.5f, 0.5f, 0.5f));
        await writer.WriteLineAsync(JsonSerializer.Serialize<SimulationCommand>(command, SimulationNetworkOptions.JsonSerializerOptions));
        await writer.FlushAsync();

        var updated = await ReadUntilAsync(reader, snapshot => snapshot.Entities.Any(entity => entity.Name == "Network_Obstacle"));

        Assert.NotNull(updated);
        Assert.Contains(updated.Entities, entity => entity.Name == "Network_Obstacle");
    }

    [Fact]
    public async Task Server_SendsUdpSnapshotsAfterSubscription()
    {
        var host = new SimulationHost();
        await using var server = new TcpSimulationServer(host, IPAddress.Loopback, 0);
        await server.StartAsync();

        using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var tcpClient = new TcpClient { NoDelay = true };
        await tcpClient.ConnectAsync(server.LocalEndPoint.Address, server.LocalEndPoint.Port);
        await using var stream = tcpClient.GetStream();
        using var reader = new StreamReader(stream);
        await using var writer = new StreamWriter(stream);
        _ = await ReadSnapshotAsync(reader);

        var udpPort = ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
        await writer.WriteLineAsync(JsonSerializer.Serialize<SimulationCommand>(
            new SubscribeUdpSnapshotsCommand(udpPort),
            SimulationNetworkOptions.JsonSerializerOptions));
        await writer.FlushAsync();

        var received = await udpClient.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var snapshot = JsonSerializer.Deserialize<SimulationSnapshot>(received.Buffer, SimulationNetworkOptions.JsonSerializerOptions);

        Assert.NotNull(snapshot);
        Assert.True(snapshot.Entities.Count > 0);
    }

    private static async Task<SimulationSnapshot> ReadSnapshotAsync(StreamReader reader)
    {
        var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(string.IsNullOrWhiteSpace(line));
        return JsonSerializer.Deserialize<SimulationSnapshot>(line, SimulationNetworkOptions.JsonSerializerOptions)!;
    }

    private static async Task<SimulationSnapshot> ReadUntilAsync(StreamReader reader, Func<SimulationSnapshot, bool> predicate)
    {
        var timeout = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTimeOffset.UtcNow < timeout)
        {
            var snapshot = await ReadSnapshotAsync(reader);
            if (predicate(snapshot))
            {
                return snapshot;
            }
        }

        throw new TimeoutException("Expected snapshot was not received.");
    }
}
