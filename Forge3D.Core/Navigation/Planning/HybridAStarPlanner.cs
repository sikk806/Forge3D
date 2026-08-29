using System.Diagnostics;
using Forge3D.Core.Navigation.Collision;

namespace Forge3D.Core.Navigation.Planning;

public sealed class HybridAStarPlanner : IPathPlanner
{
    private readonly VehicleKinematicModel _kinematicModel;
    private readonly MotionPrimitiveGenerator _primitiveGenerator;
    private readonly PathCollisionChecker _collisionChecker;

    public HybridAStarPlanner()
        : this(new VehicleKinematicModel(), new MotionPrimitiveGenerator(), new PathCollisionChecker())
    {
    }

    public HybridAStarPlanner(
        VehicleKinematicModel kinematicModel,
        MotionPrimitiveGenerator primitiveGenerator,
        PathCollisionChecker collisionChecker)
    {
        _kinematicModel = kinematicModel;
        _primitiveGenerator = primitiveGenerator;
        _collisionChecker = collisionChecker;
    }

    public PathResult Plan(PathRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var step = Math.Max(0.25f, request.GridResolution);
        var startKey = Quantize(request.Start, step);
        var goalKey = Quantize(request.Goal, step);

        var open = new PriorityQueue<Node, float>();
        var cameFrom = new Dictionary<StateKey, StateKey>();
        var states = new Dictionary<StateKey, NavigationPose> { [startKey] = request.Start };
        var costSoFar = new Dictionary<StateKey, float> { [startKey] = 0.0f };
        var start = new Node(request.Start, startKey);
        var expanded = 0;

        open.Enqueue(start, Heuristic(request.Start, request.Goal));

        while (open.Count > 0 && expanded < 6000)
        {
            var current = open.Dequeue();
            expanded++;

            if (Distance(current.Pose, request.Goal) <= Math.Max(step, 0.75f))
            {
                stopwatch.Stop();
                var points = Reconstruct(cameFrom, states, current.Key, request.Goal.HeadingDegrees);
                var length = ComputeLength(points);
                return new PathResult(true, points, expanded, stopwatch.Elapsed, length, length);
            }

            foreach (var steering in _primitiveGenerator.GenerateSteeringAngles(request.Vehicle))
            {
                if (MathF.Abs(steering) > request.Vehicle.MaxSteeringAngleDegrees + 0.001f)
                {
                    continue;
                }

                var nextPose = _kinematicModel.Step(current.Pose, steering, step, request.Vehicle);
                if (!IsInsideBounds(nextPose, request) || _collisionChecker.IsBlocked(nextPose.X, nextPose.Z, request.Vehicle, request.Obstacles))
                {
                    continue;
                }

                var nextKey = Quantize(nextPose, step);
                var nextCost = costSoFar[current.Key] + step + (MathF.Abs(steering) * 0.01f);
                if (costSoFar.TryGetValue(nextKey, out var oldCost) && nextCost >= oldCost)
                {
                    continue;
                }

                costSoFar[nextKey] = nextCost;
                cameFrom[nextKey] = current.Key;
                states[nextKey] = nextPose;
                open.Enqueue(new Node(nextPose, nextKey), nextCost + Heuristic(nextPose, request.Goal));
            }
        }

        stopwatch.Stop();
        return PathResult.Failed(expanded, stopwatch.Elapsed, "Goal is unreachable.");
    }

    private static bool IsInsideBounds(NavigationPose pose, PathRequest request)
    {
        return pose.X >= request.WorldMinX
            && pose.X <= request.WorldMaxX
            && pose.Z >= request.WorldMinZ
            && pose.Z <= request.WorldMaxZ;
    }

    private static StateKey Quantize(NavigationPose pose, float resolution)
    {
        var headingBucket = (int)MathF.Round(Normalize360(pose.HeadingDegrees) / 15.0f);
        return new StateKey(
            (int)MathF.Round(pose.X / resolution),
            (int)MathF.Round(pose.Z / resolution),
            headingBucket);
    }

    private static float Normalize360(float degrees)
    {
        while (degrees < 0.0f)
        {
            degrees += 360.0f;
        }

        while (degrees >= 360.0f)
        {
            degrees -= 360.0f;
        }

        return degrees;
    }

    private static float Heuristic(NavigationPose pose, NavigationPose goal)
    {
        return Distance(pose, goal);
    }

    private static float Distance(NavigationPose a, NavigationPose b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    private static IReadOnlyList<PathPoint> Reconstruct(
        Dictionary<StateKey, StateKey> cameFrom,
        Dictionary<StateKey, NavigationPose> states,
        StateKey current,
        float goalHeading)
    {
        var keys = new List<StateKey> { current };
        while (cameFrom.TryGetValue(current, out var previous))
        {
            current = previous;
            keys.Add(current);
        }

        keys.Reverse();
        var points = keys.Select(key =>
        {
            var pose = states[key];
            return new PathPoint(pose.X, pose.Z, pose.HeadingDegrees);
        }).ToList();

        if (points.Count > 0)
        {
            var last = points[^1];
            points[^1] = last with { HeadingDegrees = goalHeading };
        }

        return points;
    }

    private static float ComputeLength(IReadOnlyList<PathPoint> points)
    {
        var length = 0.0f;
        for (var i = 1; i < points.Count; i++)
        {
            var dx = points[i].X - points[i - 1].X;
            var dz = points[i].Z - points[i - 1].Z;
            length += MathF.Sqrt((dx * dx) + (dz * dz));
        }

        return length;
    }

    private readonly record struct StateKey(int X, int Z, int Heading);

    private readonly record struct Node(NavigationPose Pose, StateKey Key);
}
