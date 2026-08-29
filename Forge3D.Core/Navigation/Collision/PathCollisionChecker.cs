namespace Forge3D.Core.Navigation.Collision;

public sealed class PathCollisionChecker
{
    public bool IsBlocked(float x, float z, VehicleNavigationProfile profile, IEnumerable<NavigationObstacle> obstacles)
    {
        var halfWidth = profile.Width * 0.5f;
        var halfLength = profile.Length * 0.5f;

        foreach (var obstacle in obstacles)
        {
            var overlapX = Math.Abs(x - obstacle.X) <= (halfWidth + obstacle.Width * 0.5f);
            var overlapZ = Math.Abs(z - obstacle.Z) <= (halfLength + obstacle.Depth * 0.5f);
            if (overlapX && overlapZ)
            {
                return true;
            }
        }

        return false;
    }
}
