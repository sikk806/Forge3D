using System.Diagnostics;
using Forge3D.Contracts.Commands;
using Forge3D.Contracts.Connection;
using Forge3D.Contracts.States;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Simulation;
using Forge3D.Simulator.Commands;
using Forge3D.Simulator.Hosting;
using System.Windows.Threading;

namespace Forge3D.Editor.Connection;

public sealed class LocalSimulationConnection : ISimulationConnection, ILocalSimulationDiagnostics
{
    private readonly Dispatcher _dispatcher;
    private readonly Timer _timer;
    private readonly Stopwatch _frameClock = Stopwatch.StartNew();
    private readonly SimulationCommandHandler _commandHandler;
    private bool _disposed;
    private bool _isTicking;

    public LocalSimulationConnection()
        : this(new SimulationHost())
    {
    }

    public LocalSimulationConnection(SimulationHost host)
    {
        Host = host;
        _commandHandler = new SimulationCommandHandler(Host);
        _dispatcher = Dispatcher.CurrentDispatcher;
        _timer = new Timer(OnTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public SimulationHost Host { get; }

    public PhysicsWorld World => Host.World;

    public SimulationRuntime Runtime => Host.Runtime;

    public bool IsConnected => Status == ConnectionStatus.Connected;

    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

    public SimulationSnapshot? CurrentSnapshot { get; private set; }

    public event EventHandler<SimulationSnapshot>? SnapshotReceived;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Status = ConnectionStatus.Connecting;
        Host.ResetToDropScenario();
        PublishSnapshot();
        Status = ConnectionStatus.Connected;
        _frameClock.Restart();
        _timer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(16));
        PublishSnapshot();
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
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
        if (_disposed)
        {
            return;
        }

        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timer.Dispose();
        Status = ConnectionStatus.Disconnected;
        _disposed = true;
    }

    private void OnTick(object? state)
    {
        if (_disposed || _isTicking)
        {
            return;
        }

        _isTicking = true;
        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                var elapsed = _frameClock.Elapsed.TotalSeconds;
                _frameClock.Restart();
                Host.Tick((float)Math.Min(elapsed, 0.1));
                PublishSnapshot();
            }
            finally
            {
                _isTicking = false;
            }
        });
    }

    private void PublishSnapshot()
    {
        CurrentSnapshot = Host.Snapshot();
        SnapshotReceived?.Invoke(this, CurrentSnapshot);
    }
}
