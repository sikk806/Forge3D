namespace Forge3D.Core.Navigation.Planning;

public sealed class MotionPrimitiveGenerator
{
    public IReadOnlyList<float> GenerateSteeringAngles(VehicleNavigationProfile profile)
    {
        var max = MathF.Abs(profile.MaxSteeringAngleDegrees);
        return [-max, -max * 0.5f, 0.0f, max * 0.5f, max];
    }
}
