using System.Diagnostics;
using System.Windows.Media;

namespace Forge3D.Editor.Rendering;

public sealed class RenderLoop : IDisposable
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _isRunning;
    private TimeSpan _lastElapsed;

    public event EventHandler<RenderFrameEventArgs>? Rendering;

    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        _lastElapsed = _clock.Elapsed;
        CompositionTarget.Rendering += OnRendering;
        _isRunning = true;
    }

    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRendering;
        _isRunning = false;
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var delta = now - _lastElapsed;
        _lastElapsed = now;

        if (delta <= TimeSpan.Zero)
        {
            return;
        }

        Rendering?.Invoke(this, new RenderFrameEventArgs(delta, now));
    }
}

public sealed class RenderFrameEventArgs : EventArgs
{
    public RenderFrameEventArgs(TimeSpan deltaTime, TimeSpan elapsedTime)
    {
        DeltaTime = deltaTime;
        ElapsedTime = elapsedTime;
    }

    public TimeSpan DeltaTime { get; }

    public TimeSpan ElapsedTime { get; }
}
