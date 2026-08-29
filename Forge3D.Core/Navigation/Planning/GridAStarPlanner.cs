using System.Diagnostics;
using Forge3D.Core.Navigation.Collision;

namespace Forge3D.Core.Navigation.Planning;

public sealed class GridAStarPlanner : IPathPlanner
{
    private static readonly (int X, int Z, float Cost)[] Directions =
    [
        (-1, 0, 1.0f), (1, 0, 1.0f), (0, -1, 1.0f), (0, 1, 1.0f),
        (-1, -1, 1.4142f), (-1, 1, 1.4142f), (1, -1, 1.4142f), (1, 1, 1.4142f)
    ];

    private readonly PathCollisionChecker _collisionChecker;

    public GridAStarPlanner()
        : this(new PathCollisionChecker())
    {
    }

    public GridAStarPlanner(PathCollisionChecker collisionChecker)
    {
        _collisionChecker = collisionChecker;
    }

    public PathResult Plan(PathRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var grid = Grid.FromRequest(request);
        var start = grid.ToCell(request.Start.X, request.Start.Z);
        var goal = grid.ToCell(request.Goal.X, request.Goal.Z);

        if (!grid.Contains(start) || !grid.Contains(goal))
        {
            stopwatch.Stop();
            return PathResult.Failed(0, stopwatch.Elapsed, "Start or goal is outside planning bounds.");
        }

        if (IsBlocked(start) || IsBlocked(goal))
        {
            stopwatch.Stop();
            return PathResult.Failed(0, stopwatch.Elapsed, "Start or goal is blocked.");
        }

        var open = new PriorityQueue<Cell, float>();
        var cameFrom = new Dictionary<Cell, Cell>();
        var costSoFar = new Dictionary<Cell, float> { [start] = 0.0f };
        var expanded = 0;

        open.Enqueue(start, 0.0f);

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            expanded++;

            if (current.Equals(goal))
            {
                stopwatch.Stop();
                var points = Reconstruct(grid, cameFrom, current, request.Goal.HeadingDegrees);
                var length = ComputeLength(points);
                return new PathResult(true, points, expanded, stopwatch.Elapsed, length, length);
            }

            foreach (var direction in Directions)
            {
                var next = new Cell(current.X + direction.X, current.Z + direction.Z);
                if (!grid.Contains(next) || IsBlocked(next))
                {
                    continue;
                }

                var nextCost = costSoFar[current] + direction.Cost;
                if (costSoFar.TryGetValue(next, out var oldCost) && nextCost >= oldCost)
                {
                    continue;
                }

                costSoFar[next] = nextCost;
                cameFrom[next] = current;
                open.Enqueue(next, nextCost + Heuristic(next, goal));
            }
        }

        stopwatch.Stop();
        return PathResult.Failed(expanded, stopwatch.Elapsed, "Goal is unreachable.");

        bool IsBlocked(Cell cell)
        {
            var point = grid.ToWorld(cell);
            return _collisionChecker.IsBlocked(point.X, point.Z, request.Vehicle, request.Obstacles);
        }
    }

    private static float Heuristic(Cell a, Cell b)
    {
        var dx = Math.Abs(a.X - b.X);
        var dz = Math.Abs(a.Z - b.Z);
        return Math.Max(dx, dz) + (0.4142f * Math.Min(dx, dz));
    }

    private static IReadOnlyList<PathPoint> Reconstruct(Grid grid, Dictionary<Cell, Cell> cameFrom, Cell current, float goalHeading)
    {
        var cells = new List<Cell> { current };
        while (cameFrom.TryGetValue(current, out var previous))
        {
            current = previous;
            cells.Add(current);
        }

        cells.Reverse();
        var points = new List<PathPoint>(cells.Count);
        for (var i = 0; i < cells.Count; i++)
        {
            var world = grid.ToWorld(cells[i]);
            var heading = i < cells.Count - 1
                ? MathF.Atan2(grid.ToWorld(cells[i + 1]).X - world.X, grid.ToWorld(cells[i + 1]).Z - world.Z) * 180.0f / MathF.PI
                : goalHeading;
            points.Add(new PathPoint(world.X, world.Z, heading));
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

    private readonly record struct Cell(int X, int Z);

    private sealed class Grid
    {
        private Grid(PathRequest request)
        {
            Resolution = Math.Max(0.1f, request.GridResolution);
            MinX = request.WorldMinX;
            MinZ = request.WorldMinZ;
            Width = (int)MathF.Ceiling((request.WorldMaxX - request.WorldMinX) / Resolution) + 1;
            Height = (int)MathF.Ceiling((request.WorldMaxZ - request.WorldMinZ) / Resolution) + 1;
        }

        public float Resolution { get; }

        public float MinX { get; }

        public float MinZ { get; }

        public int Width { get; }

        public int Height { get; }

        public static Grid FromRequest(PathRequest request) => new(request);

        public Cell ToCell(float x, float z)
        {
            return new Cell(
                (int)MathF.Round((x - MinX) / Resolution),
                (int)MathF.Round((z - MinZ) / Resolution));
        }

        public PathPoint ToWorld(Cell cell)
        {
            return new PathPoint(MinX + (cell.X * Resolution), MinZ + (cell.Z * Resolution));
        }

        public bool Contains(Cell cell)
        {
            return cell.X >= 0 && cell.Z >= 0 && cell.X < Width && cell.Z < Height;
        }
    }
}
