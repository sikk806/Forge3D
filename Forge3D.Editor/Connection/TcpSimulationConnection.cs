using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows.Threading;
using Forge3D.Contracts.Commands;
using Forge3D.Contracts.Connection;
using Forge3D.Contracts.States;

namespace Forge3D.Editor.Connection;

public sealed class TcpSimulationConnection : ISimulationConnection
{
    private readonly Dispatcher _dispatcher;
    private readonly string _host;
    private readonly int _port;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private CancellationTokenSource? _readerStop;
    private TcpClient? _client;
    private UdpClient? _udpClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Task? _readerTask;
    private Task? _udpReaderTask;
    private bool _disposed;

    public TcpSimulationConnection(string host = "127.0.0.1", int port = 47320, bool useUdpSnapshots = true)
    {
        _host = host;
        _port = port;
        UseUdpSnapshots = useUdpSnapshots;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public bool UseUdpSnapshots { get; }

    public bool IsConnected => Status == ConnectionStatus.Connected;

    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

    public SimulationSnapshot? CurrentSnapshot { get; private set; }

    public event EventHandler<SimulationSnapshot>? SnapshotReceived;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsConnected)
        {
            return;
        }

        Status = ConnectionStatus.Connecting;
        _readerStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _client = new TcpClient { NoDelay = true };
        await _client.ConnectAsync(_host, _port, cancellationToken).ConfigureAwait(false);
        var stream = _client.GetStream();
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream);
        Status = ConnectionStatus.Connected;
        if (UseUdpSnapshots)
        {
            StartUdpSnapshotReceiver(_readerStop.Token);
            await SendUdpSubscriptionAsync(cancellationToken).ConfigureAwait(false);
        }

        _readerTask = Task.Run(() => ReadSnapshotsAsync(_readerStop.Token), CancellationToken.None);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (Status == ConnectionStatus.Disconnected)
        {
            return;
        }

        Status = ConnectionStatus.Disconnected;
        _readerStop?.Cancel();
        _client?.Close();
        _udpClient?.Close();

        if (_readerTask is not null)
        {
            await IgnoreDisconnectErrorAsync(_readerTask, cancellationToken).ConfigureAwait(false);
        }

        if (_udpReaderTask is not null)
        {
            await IgnoreDisconnectErrorAsync(_udpReaderTask, cancellationToken).ConfigureAwait(false);
        }

        DisposeNetworkObjects();
    }

    public async Task SendCommandAsync(SimulationCommand command, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsConnected || _writer is null)
        {
            throw new InvalidOperationException("Simulation TCP connection is not connected.");
        }

        var line = JsonSerializer.Serialize(command, SimulationNetworkOptions.JsonSerializerOptions);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _readerStop?.Cancel();
        DisposeNetworkObjects();
        _readerStop?.Dispose();
        _writeGate.Dispose();
        Status = ConnectionStatus.Disconnected;
        _disposed = true;
    }

    private async Task ReadSnapshotsAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                var snapshot = JsonSerializer.Deserialize<SimulationSnapshot>(line, SimulationNetworkOptions.JsonSerializerOptions);
                if (snapshot is null)
                {
                    continue;
                }

                await _dispatcher.BeginInvoke(() =>
                {
                    CurrentSnapshot = snapshot;
                    SnapshotReceived?.Invoke(this, snapshot);
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (JsonException)
        {
        }
        finally
        {
            if (!_disposed)
            {
                Status = ConnectionStatus.Disconnected;
            }
        }
    }

    private void StartUdpSnapshotReceiver(CancellationToken cancellationToken)
    {
        _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        _udpReaderTask = Task.Run(() => ReadUdpSnapshotsAsync(cancellationToken), CancellationToken.None);
    }

    private async Task SendUdpSubscriptionAsync(CancellationToken cancellationToken)
    {
        if (_udpClient?.Client.LocalEndPoint is not IPEndPoint localEndPoint)
        {
            return;
        }

        await SendCommandAsync(new SubscribeUdpSnapshotsCommand(localEndPoint.Port), cancellationToken).ConfigureAwait(false);
    }

    private async Task ReadUdpSnapshotsAsync(CancellationToken cancellationToken)
    {
        if (_udpClient is null)
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await _udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                var snapshot = JsonSerializer.Deserialize<SimulationSnapshot>(result.Buffer, SimulationNetworkOptions.JsonSerializerOptions);
                if (snapshot is null)
                {
                    continue;
                }

                await PublishSnapshotAsync(snapshot).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (JsonException)
        {
        }
    }

    private async Task PublishSnapshotAsync(SimulationSnapshot snapshot)
    {
        await _dispatcher.BeginInvoke(() =>
        {
            CurrentSnapshot = snapshot;
            SnapshotReceived?.Invoke(this, snapshot);
        });
    }

    private void DisposeNetworkObjects()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _udpClient?.Dispose();
        _client?.Dispose();
        _writer = null;
        _reader = null;
        _udpClient = null;
        _client = null;
    }

    private static async Task IgnoreDisconnectErrorAsync(Task task, CancellationToken cancellationToken)
    {
        try
        {
            await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
    }
}
