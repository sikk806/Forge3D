using System.Numerics;

namespace Forge3D.Core.Navigation.Following;

public sealed class PathFollower
{
    public PathFollower(float reachRadius = 0.65f)
    {
        ReachRadius = reachRadius;
    }

    public float ReachRadius { get; }

    public float LookAheadDistance { get; set; } = 1.8f;

    public float LookAheadSpeedFactor { get; set; } = 0.25f;

    public int CurrentIndex { get; private set; }

    public void Reset()
    {
        CurrentIndex = 0;
    }

    public bool TryGetTarget(Vector3 position, IReadOnlyList<PathPoint> path, out PathPoint target, out float desiredHeadingDegrees)
    {
        return TryGetTarget(position, 0.0f, path, out target, out desiredHeadingDegrees);
    }

    public bool TryGetTarget(Vector3 position, float currentSpeed, IReadOnlyList<PathPoint> path, out PathPoint target, out float desiredHeadingDegrees)
    {
        target = default;
        desiredHeadingDegrees = 0.0f;

        if (path.Count == 0 || CurrentIndex >= path.Count)
        {
            return false;
        }

        while (CurrentIndex < path.Count - 1 && Distance(position, path[CurrentIndex]) <= ReachRadius)
        {
            CurrentIndex++;
        }

        target = GetLookAheadTarget(position, currentSpeed, path);
        desiredHeadingDegrees = MathF.Atan2(target.X - position.X, target.Z - position.Z) * 180.0f / MathF.PI;
        return true;
    }

    private PathPoint GetLookAheadTarget(Vector3 position, float currentSpeed, IReadOnlyList<PathPoint> path)
    {
        var lookAhead = LookAheadDistance + (currentSpeed * LookAheadSpeedFactor);
        var previous = new PathPoint(position.X, position.Z);

        for (var index = CurrentIndex; index < path.Count; index++)
        {
            var next = path[index];
            var segmentLength = Distance(previous, next);

            if (segmentLength >= lookAhead)
            {
                var t = Math.Clamp(lookAhead / MathF.Max(0.001f, segmentLength), 0.0f, 1.0f);
                return new PathPoint(
                    previous.X + ((next.X - previous.X) * t),
                    previous.Z + ((next.Z - previous.Z) * t),
                    next.HeadingDegrees);
            }

            lookAhead -= segmentLength;
            previous = next;
        }

        return path[^1];
    }

    private static float Distance(Vector3 position, PathPoint point)
    {
        var dx = position.X - point.X;
        var dz = position.Z - point.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    private static float Distance(PathPoint a, PathPoint b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
