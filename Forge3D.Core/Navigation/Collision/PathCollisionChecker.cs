namespace Forge3D.Core.Navigation.Collision;

public sealed class PathCollisionChecker
{
    public bool IsBlocked(float x, float z, VehicleNavigationProfile profile, IEnumerable<NavigationObstacle> obstacles)
    {
        var radius = (MathF.Sqrt((profile.Width * profile.Width) + (profile.Length * profile.Length)) * 0.5f) + profile.Clearance;

        foreach (var obstacle in obstacles)
        {
            var obstacleRadius = MathF.Sqrt((obstacle.Width * obstacle.Width) + (obstacle.Depth * obstacle.Depth)) * 0.5f;
            if (DistanceSquared(x, z, obstacle.X, obstacle.Z) > (radius + obstacleRadius) * (radius + obstacleRadius))
            {
                continue;
            }

            var overlapX = Math.Abs(x - obstacle.X) <= ((profile.Width * 0.5f) + profile.Clearance + (obstacle.Width * 0.5f));
            var overlapZ = Math.Abs(z - obstacle.Z) <= ((profile.Length * 0.5f) + profile.Clearance + (obstacle.Depth * 0.5f));
            if (overlapX && overlapZ)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsBlocked(NavigationPose pose, VehicleNavigationProfile profile, IEnumerable<NavigationObstacle> obstacles)
    {
        var corners = GetVehicleCorners(pose, profile);
        foreach (var obstacle in obstacles)
        {
            if (corners.Any(corner => IsInsideInflatedObstacle(corner.X, corner.Z, obstacle, profile.Clearance)))
            {
                return true;
            }

            if (GetObstacleCorners(obstacle).Any(corner => IsInsideVehicle(corner.X, corner.Z, pose, profile)))
            {
                return true;
            }

            if (IsBlocked(pose.X, pose.Z, profile, [obstacle]))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsSegmentBlocked(
        NavigationPose start,
        NavigationPose end,
        VehicleNavigationProfile profile,
        IEnumerable<NavigationObstacle> obstacles,
        float sampleSpacing)
    {
        var distance = MathF.Sqrt(DistanceSquared(start.X, start.Z, end.X, end.Z));
        var samples = Math.Max(2, (int)MathF.Ceiling(distance / MathF.Max(0.1f, sampleSpacing)));
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (float)samples;
            var pose = new NavigationPose(
                Lerp(start.X, end.X, t),
                Lerp(start.Z, end.Z, t),
                LerpAngle(start.HeadingDegrees, end.HeadingDegrees, t));
            if (IsBlocked(pose, profile, obstacles))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<(float X, float Z)> GetVehicleCorners(NavigationPose pose, VehicleNavigationProfile profile)
    {
        var halfWidth = (profile.Width * 0.5f) + profile.Clearance;
        var halfLength = (profile.Length * 0.5f) + profile.Clearance;
        var radians = pose.HeadingDegrees * MathF.PI / 180.0f;
        var forwardX = MathF.Sin(radians);
        var forwardZ = MathF.Cos(radians);
        var rightX = MathF.Cos(radians);
        var rightZ = -MathF.Sin(radians);

        return
        [
            AddOffset(halfWidth, halfLength),
            AddOffset(-halfWidth, halfLength),
            AddOffset(halfWidth, -halfLength),
            AddOffset(-halfWidth, -halfLength)
        ];

        (float X, float Z) AddOffset(float right, float forward)
        {
            return (
                pose.X + (rightX * right) + (forwardX * forward),
                pose.Z + (rightZ * right) + (forwardZ * forward));
        }
    }

    private static IReadOnlyList<(float X, float Z)> GetObstacleCorners(NavigationObstacle obstacle)
    {
        var halfWidth = obstacle.Width * 0.5f;
        var halfDepth = obstacle.Depth * 0.5f;
        return
        [
            (obstacle.X - halfWidth, obstacle.Z - halfDepth),
            (obstacle.X + halfWidth, obstacle.Z - halfDepth),
            (obstacle.X - halfWidth, obstacle.Z + halfDepth),
            (obstacle.X + halfWidth, obstacle.Z + halfDepth)
        ];
    }

    private static bool IsInsideInflatedObstacle(float x, float z, NavigationObstacle obstacle, float clearance)
    {
        return Math.Abs(x - obstacle.X) <= ((obstacle.Width * 0.5f) + clearance)
            && Math.Abs(z - obstacle.Z) <= ((obstacle.Depth * 0.5f) + clearance);
    }

    private static bool IsInsideVehicle(float x, float z, NavigationPose pose, VehicleNavigationProfile profile)
    {
        var dx = x - pose.X;
        var dz = z - pose.Z;
        var radians = pose.HeadingDegrees * MathF.PI / 180.0f;
        var forwardX = MathF.Sin(radians);
        var forwardZ = MathF.Cos(radians);
        var rightX = MathF.Cos(radians);
        var rightZ = -MathF.Sin(radians);
        return Math.Abs((dx * rightX) + (dz * rightZ)) <= ((profile.Width * 0.5f) + profile.Clearance)
            && Math.Abs((dx * forwardX) + (dz * forwardZ)) <= ((profile.Length * 0.5f) + profile.Clearance);
    }

    private static float DistanceSquared(float ax, float az, float bx, float bz)
    {
        var dx = ax - bx;
        var dz = az - bz;
        return (dx * dx) + (dz * dz);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + ((b - a) * t);
    }

    private static float LerpAngle(float a, float b, float t)
    {
        var delta = b - a;
        while (delta > 180.0f)
        {
            delta -= 360.0f;
        }

        while (delta < -180.0f)
        {
            delta += 360.0f;
        }

        return a + (delta * t);
    }
}
