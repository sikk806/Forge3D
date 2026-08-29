namespace Forge3D.Core.Navigation.Planning;

public sealed class VehicleKinematicModel
{
    public NavigationPose Step(NavigationPose pose, float steeringDegrees, float travelDistance, VehicleNavigationProfile profile)
    {
        var steeringRadians = steeringDegrees * MathF.PI / 180.0f;
        var headingRadians = pose.HeadingDegrees * MathF.PI / 180.0f;

        if (MathF.Abs(steeringRadians) < 0.0001f)
        {
            return new NavigationPose(
                pose.X + (MathF.Sin(headingRadians) * travelDistance),
                pose.Z + (MathF.Cos(headingRadians) * travelDistance),
                NormalizeDegrees(pose.HeadingDegrees));
        }

        var turnRadius = profile.Wheelbase / MathF.Tan(steeringRadians);
        var headingDelta = travelDistance / turnRadius;
        var nextHeading = headingRadians + headingDelta;

        return new NavigationPose(
            pose.X + (turnRadius * (MathF.Cos(headingRadians) - MathF.Cos(nextHeading))),
            pose.Z + (turnRadius * (MathF.Sin(nextHeading) - MathF.Sin(headingRadians))),
            NormalizeDegrees(nextHeading * 180.0f / MathF.PI));
    }

    private static float NormalizeDegrees(float degrees)
    {
        while (degrees > 180.0f)
        {
            degrees -= 360.0f;
        }

        while (degrees < -180.0f)
        {
            degrees += 360.0f;
        }

        return degrees;
    }
}
