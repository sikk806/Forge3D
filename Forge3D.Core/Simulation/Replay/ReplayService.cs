using Forge3D.Core.Dynamics;

namespace Forge3D.Core.Simulation.Replay;

public sealed class ReplayService
{
    private readonly List<ReplayFrame> _frames = [];

    public ReplayService(int maxFrames = 1800)
    {
        MaxFrames = maxFrames;
    }

    public int MaxFrames { get; }

    public IReadOnlyList<ReplayFrame> Frames => _frames;

    public int FrameCount => _frames.Count;

    public void Capture(double time, IEnumerable<RigidBody> bodies)
    {
        var snapshots = bodies
            .Select(body => new BodySnapshot(body.Name, body.Position, body.Orientation, body.LinearVelocity, body.AngularVelocity))
            .ToArray();

        if (snapshots.Length == 0)
        {
            return;
        }

        _frames.Add(new ReplayFrame(time, snapshots));

        while (_frames.Count > MaxFrames)
        {
            _frames.RemoveAt(0);
        }
    }

    public bool TryApply(int index, IEnumerable<RigidBody> bodies, out double time)
    {
        time = 0.0;
        if (index < 0 || index >= _frames.Count)
        {
            return false;
        }

        var byName = bodies.ToDictionary(body => body.Name);
        var frame = _frames[index];
        foreach (var snapshot in frame.Bodies)
        {
            if (!byName.TryGetValue(snapshot.Name, out var body))
            {
                continue;
            }

            body.Position = snapshot.Position;
            body.Orientation = snapshot.Orientation;
            body.LinearVelocity = snapshot.LinearVelocity;
            body.AngularVelocity = snapshot.AngularVelocity;
        }

        time = frame.Time;
        return true;
    }

    public void Clear()
    {
        _frames.Clear();
    }
}
