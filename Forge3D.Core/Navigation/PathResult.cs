namespace Forge3D.Core.Navigation;

public sealed class PathResult
{
    public static PathResult Failed(int expandedNodes, TimeSpan planningTime, string message)
    {
        return new PathResult(false, [], expandedNodes, planningTime, 0.0f, 0.0f, message);
    }

    public PathResult(
        bool succeeded,
        IReadOnlyList<PathPoint> points,
        int expandedNodes,
        TimeSpan planningTime,
        float pathLength,
        float pathCost,
        string message = "")
    {
        Succeeded = succeeded;
        Points = points;
        ExpandedNodes = expandedNodes;
        PlanningTime = planningTime;
        PathLength = pathLength;
        PathCost = pathCost;
        Message = message;
    }

    public bool Succeeded { get; }

    public IReadOnlyList<PathPoint> Points { get; }

    public int ExpandedNodes { get; }

    public TimeSpan PlanningTime { get; }

    public float PathLength { get; }

    public float PathCost { get; }

    public string Message { get; }
}
