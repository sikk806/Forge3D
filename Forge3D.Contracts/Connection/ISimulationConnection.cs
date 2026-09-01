using Forge3D.Contracts.Commands;
using Forge3D.Contracts.States;

namespace Forge3D.Contracts.Connection;

public interface ISimulationConnection : IDisposable
{
    bool IsConnected { get; }

    ConnectionStatus Status { get; }

    SimulationSnapshot? CurrentSnapshot { get; }

    event EventHandler<SimulationSnapshot>? SnapshotReceived;

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task SendCommandAsync(SimulationCommand command, CancellationToken cancellationToken = default);
}
