using System.Numerics;
using Forge3D.Core.Simulation.Sensors;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Core.Simulation.Safety;

public sealed class SafetyEvaluator
{
    public float WarningDistance { get; set; } = 5.0f;

    public float CriticalDistance { get; set; } = 2.0f;

    public bool AutomaticEmergencyStopEnabled { get; set; } = true;

    public SafetyResult Evaluate(VehicleEntity vehicle, SensorEntity sensor)
    {
        if (sensor.Detections.Count == 0)
        {
            return SafetyResult.Safe;
        }

        var nearest = sensor.Detections.OrderBy(item => item.Distance).First();
        var state = nearest.Distance <= CriticalDistance
            ? SafetyState.Critical
            : nearest.Distance <= WarningDistance ? SafetyState.Warning : SafetyState.Safe;

        var ttc = CalculateTimeToCollision(vehicle, nearest.Distance);
        return new SafetyResult(state, nearest.TargetName, nearest.Distance, ttc);
    }

    private static float? CalculateTimeToCollision(VehicleEntity vehicle, float distance)
    {
        var body = vehicle.PhysicsBody;
        if (body is null)
        {
            return null;
        }

        var forward = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, body.Orientation));
        var closingSpeed = MathF.Max(0.0f, Vector3.Dot(body.LinearVelocity, forward));
        if (closingSpeed <= 0.05f)
        {
            return null;
        }

        var value = distance / closingSpeed;
        return float.IsFinite(value) ? value : null;
    }
}
