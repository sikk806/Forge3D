namespace Forge3D.Core.Navigation;

public sealed class PathSimplifier
{
    public IReadOnlyList<PathPoint> Simplify(IReadOnlyList<PathPoint> points)
    {
        if (points.Count <= 2)
        {
            return points;
        }

        var simplified = new List<PathPoint> { points[0] };
        for (var i = 1; i < points.Count - 1; i++)
        {
            var previous = simplified[^1];
            var current = points[i];
            var next = points[i + 1];

            if (!IsCollinear(previous, current, next))
            {
                simplified.Add(current);
            }
        }

        simplified.Add(points[^1]);
        return simplified;
    }

    private static bool IsCollinear(PathPoint a, PathPoint b, PathPoint c)
    {
        var abX = b.X - a.X;
        var abZ = b.Z - a.Z;
        var bcX = c.X - b.X;
        var bcZ = c.Z - b.Z;
        var cross = (abX * bcZ) - (abZ * bcX);
        return MathF.Abs(cross) <= 0.0001f;
    }
}
