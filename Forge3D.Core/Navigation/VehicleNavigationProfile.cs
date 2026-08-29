namespace Forge3D.Core.Navigation;

public sealed class VehicleNavigationProfile
{
    public static VehicleNavigationProfile Default => new();

    public float Wheelbase { get; init; } = 1.2f;

    public float MaxSteeringAngleDegrees { get; init; } = 30.0f;

    public float Width { get; init; } = 1.1f;

    public float Length { get; init; } = 1.7f;

    public bool AllowReverse { get; init; }
}
