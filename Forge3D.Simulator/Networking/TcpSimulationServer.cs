using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Forge3D.Contracts.Commands;
using Forge3D.Contracts.Connection;
using Forge3D.Contracts.States;
using Forge3D.Simulator.Commands;
using Forge3D.Simulator.Hosting;

namespace Forge3D.Simulator.Networking;

public sealed class TcpSimulationServer : IAsyncDisposable
{
    private readonly SimulationHost _host;
    private readonly SimulationCommandHandler _commandHandler;
    private readonly object _simulationGate = new();
    private readonly ConcurrentDictionary<int, ClientSession> _clients = [];
    private readonly UdpClient _udp = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly TcpListener _listener;
    private int _nextClientId;
    private Task? _acceptTask;
    private Task? _simulationTask;

    public TcpSimulationServer(SimulationHost host, IPAddress address, int port)
    {
        _host = host;
        _commandHandler = new SimulationCommandHandler(host);
        _listener = new TcpListener(address, port);
    }

    public IPEndPoint LocalEndPoint => (IPEndPoint)_listener.LocalEndpoint;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _host.ResetToDropScenario();
        _listener.Start();
        _acceptTask = AcceptLoopAsync(_stop.Token);
        _simulationTask = SimulationLoopAsync(_stop.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_stop.IsCancellationRequested)
        {
            return;
        }

        _stop.Cancel();
        _listener.Stop();
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _udp.Dispose();

        await IgnoreCancellationAsync(_acceptTask).ConfigureAwait(false);
        await IgnoreCancellationAsync(_simulationTask).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _stop.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient tcpClient;
            try
            {
                tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            tcpClient.NoDelay = true;
            var client = new ClientSession(Interlocked.Increment(ref _nextClientId), tcpClient);
            _clients[client.Id] = client;
            _ = Task.Run(() => ClientReadLoopAsync(client, cancellationToken), CancellationToken.None);

            SimulationSnapshot snapshot;
            lock (_simulationGate)
            {
                snapshot = _host.Snapshot();
            }

            await TrySendSnapshotAsync(client, snapshot, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ClientReadLoopAsync(ClientSession client, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await client.Reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    return;
                }

                var command = JsonSerializer.Deserialize<SimulationCommand>(line, SimulationNetworkOptions.JsonSerializerOptions);
                if (command is null)
                {
                    continue;
                }

                if (command is SubscribeUdpSnapshotsCommand subscribe)
                {
                    client.UdpEndPoint = new IPEndPoint(((IPEndPoint)client.RemoteEndPoint).Address, subscribe.Port);
                    continue;
                }

                lock (_simulationGate)
                {
                    _commandHandler.Handle(command);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        catch (JsonException)
        {
        }
        finally
        {
            RemoveClient(client);
        }
    }

    private async Task SimulationLoopAsync(CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsed = clock.Elapsed.TotalSeconds;
            clock.Restart();

            SimulationSnapshot snapshot;
            lock (_simulationGate)
            {
                _host.Tick((float)Math.Min(elapsed, 0.1));
                snapshot = _host.Snapshot();
            }

            await BroadcastSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(16), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task BroadcastSnapshotAsync(SimulationSnapshot snapshot, CancellationToken cancellationToken)
    {
        await BroadcastUdpSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);

        foreach (var client in _clients.Values)
        {
            if (!await TrySendSnapshotAsync(client, snapshot, cancellationToken).ConfigureAwait(false))
            {
                RemoveClient(client);
            }
        }
    }

    private async Task BroadcastUdpSnapshotAsync(SimulationSnapshot snapshot, CancellationToken cancellationToken)
    {
        var udpClients = _clients.Values
            .Select(client => client.UdpEndPoint)
            .Where(endPoint => endPoint is not null)
            .Cast<IPEndPoint>()
            .ToList();
        if (udpClients.Count == 0)
        {
            return;
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, SimulationNetworkOptions.JsonSerializerOptions);
        if (bytes.Length > 60_000)
        {
            return;
        }

        foreach (var endPoint in udpClients)
        {
            try
            {
                await _udp.SendAsync(bytes, endPoint, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private static async Task<bool> TrySendSnapshotAsync(ClientSession client, SimulationSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var line = JsonSerializer.Serialize(snapshot, SimulationNetworkOptions.JsonSerializerOptions);
            await client.WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await client.Writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
                await client.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                client.WriteGate.Release();
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private void RemoveClient(ClientSession client)
    {
        if (_clients.TryRemove(client.Id, out var removed))
        {
            removed.Dispose();
        }
    }

    private static async Task IgnoreCancellationAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException)
        {
        }
    }

    private sealed class ClientSession : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;

        public ClientSession(int id, TcpClient client)
        {
            Id = id;
            _client = client;
            _stream = client.GetStream();
            Reader = new StreamReader(_stream);
            Writer = new StreamWriter(_stream);
            RemoteEndPoint = client.Client.RemoteEndPoint
                ?? throw new InvalidOperationException("TCP client does not have a remote endpoint.");
        }

        public int Id { get; }

        public StreamReader Reader { get; }

        public StreamWriter Writer { get; }

        public SemaphoreSlim WriteGate { get; } = new(1, 1);

        public EndPoint RemoteEndPoint { get; }

        public IPEndPoint? UdpEndPoint { get; set; }

        public void Dispose()
        {
            WriteGate.Dispose();
            Writer.Dispose();
            Reader.Dispose();
            _stream.Dispose();
            _client.Dispose();
        }
    }
}
