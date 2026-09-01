using Forge3D.Contracts.Commands;
using Forge3D.Simulator.Connection;

namespace Forge3D.Tests.ControlStation;

public sealed class LocalSimulationConnectionTests
{
    [Fact]
    public async Task Connection_DeliversCommandsAndSnapshots()
    {
        using var connection = new LocalSimulationConnection();
        var received = 0;
        connection.SnapshotReceived += (_, _) => received++;

        await connection.ConnectAsync();
        await connection.SendCommandAsync(new StepSimulationCommand());
        await connection.DisconnectAsync();

        Assert.True(received >= 3);
        Assert.False(connection.IsConnected);
        Assert.NotNull(connection.CurrentSnapshot);
        Assert.True(connection.CurrentSnapshot.SimulationTime > 0.0);
    }
}
