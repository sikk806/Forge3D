using Forge3D.Contracts.Commands;
using Forge3D.Contracts.Connection;
using Forge3D.Contracts.States;
using Forge3D.Simulator.Commands;
using Forge3D.Simulator.Hosting;

namespace Forge3D.Simulator.Connection;

public sealed class LocalSimulationConnection : ISimulationConnection
{
    private readonly SimulationCommandHandler _commandHandler;
    private bool _disposed;

    public LocalSimulationConnection()
        : this(new SimulationHost())
    {
    }

    public LocalSimulationConnection(SimulationHost host)
    {
        Host = host;
        _commandHandler = new SimulationCommandHandler(host);
    }

    public SimulationHost Host { get; }

    public bool IsConnected => Status == ConnectionStatus.Connected;

    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

    public SimulationSnapshot? CurrentSnapshot { get; private set; }

    public event EventHandler<SimulationSnapshot>? SnapshotReceived;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Status = ConnectionStatus.Connected;
        Host.ResetToDropScenario();
        PublishSnapshot();
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        Status = ConnectionStatus.Disconnected;
        PublishSnapshot();
        return Task.CompletedTask;
    }

    public Task SendCommandAsync(SimulationCommand command, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        _commandHandler.Handle(command);
        PublishSnapshot();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Status = ConnectionStatus.Disconnected;
        _disposed = true;
    }

    private void PublishSnapshot()
    {
        CurrentSnapshot = Host.Snapshot();
        SnapshotReceived?.Invoke(this, CurrentSnapshot);
    }
}
