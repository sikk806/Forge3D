namespace Forge3D.Core.Navigation;

public sealed class PathRequest
{
    public NavigationPose Start { get; init; }

    public NavigationPose Goal { get; init; }

    public float GridResolution { get; init; } = 0.5f;

    public float WorldMinX { get; init; } = -10.0f;

    public float WorldMaxX { get; init; } = 10.0f;

    public float WorldMinZ { get; init; } = -10.0f;

    public float WorldMaxZ { get; init; } = 10.0f;

    public VehicleNavigationProfile Vehicle { get; init; } = VehicleNavigationProfile.Default;

    public IReadOnlyList<NavigationObstacle> Obstacles { get; init; } = [];
}
