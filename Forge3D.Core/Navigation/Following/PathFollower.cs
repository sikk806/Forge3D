using System.Numerics;

namespace Forge3D.Core.Navigation.Following;

public sealed class PathFollower
{
    public PathFollower(float reachRadius = 0.65f)
    {
        ReachRadius = reachRadius;
    }

    public float ReachRadius { get; }

    public int CurrentIndex { get; private set; }

    public void Reset()
    {
        CurrentIndex = 0;
    }

    public bool TryGetTarget(Vector3 position, IReadOnlyList<PathPoint> path, out PathPoint target, out float desiredHeadingDegrees)
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

        target = path[CurrentIndex];
        desiredHeadingDegrees = MathF.Atan2(target.X - position.X, target.Z - position.Z) * 180.0f / MathF.PI;
        return true;
    }

    private static float Distance(Vector3 position, PathPoint point)
    {
        var dx = position.X - point.X;
        var dz = position.Z - point.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
