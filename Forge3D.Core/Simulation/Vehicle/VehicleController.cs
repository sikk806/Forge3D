using System.Numerics;

namespace Forge3D.Core.Simulation.Vehicle;

public sealed class VehicleController
{
    public float SpeedGain { get; set; } = 18.0f;

    public float HeadingGain { get; set; } = 8.0f;

    public float BrakeGain { get; set; } = 35.0f;

    public float MaxForce { get; set; } = 80.0f;

    public float MaxTorque { get; set; } = 18.0f;

    public float MotorScale { get; set; } = 1.0f;

    public bool CommandsEnabled { get; set; } = true;

    public void Update(VehicleEntity vehicle)
    {
        var body = vehicle.PhysicsBody;
        if (body is null || body.IsStatic)
        {
            return;
        }

        if (!CommandsEnabled)
        {
            vehicle.MotionState = MotionState.Fault;
            return;
        }

        var forward = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, body.Orientation));
        var currentForwardSpeed = Vector3.Dot(body.LinearVelocity, forward);
        var targetSpeed = vehicle.MotionState == MotionState.EmergencyStop ? 0.0f : vehicle.TargetSpeed;
        var speedError = targetSpeed - currentForwardSpeed;
        var gain = vehicle.MotionState == MotionState.EmergencyStop ? BrakeGain : SpeedGain;
        var forceMagnitude = Math.Clamp(speedError * gain, -MaxForce, MaxForce) * MotorScale;
        body.ApplyForce(forward * forceMagnitude);

        var headingError = NormalizeAngle(vehicle.TargetHeadingDegrees - vehicle.HeadingDegrees);
        var torque = Math.Clamp(headingError * HeadingGain * MathF.PI / 180.0f, -MaxTorque, MaxTorque) * MotorScale;
        body.ApplyTorque(Vector3.UnitY * torque);

        vehicle.MotionState = vehicle.MotionState == MotionState.EmergencyStop
            ? MotionState.EmergencyStop
            : MathF.Abs(targetSpeed) > 0.01f ? MotionState.Moving : MotionState.Idle;
    }

    public void Stop(VehicleEntity vehicle)
    {
        vehicle.TargetSpeed = 0.0f;
        vehicle.MotionState = MotionState.Stopping;
    }

    public void EmergencyStop(VehicleEntity vehicle)
    {
        vehicle.TargetSpeed = 0.0f;
        vehicle.MotionState = MotionState.EmergencyStop;
    }

    private static float NormalizeAngle(float degrees)
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
