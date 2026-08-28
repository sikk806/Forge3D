using System.Numerics;
using Forge3D.Core.Dynamics;

namespace Forge3D.Core.Simulation.Vehicle;

public sealed class VehicleEntity : SimulationEntity
{
    public VehicleEntity(string id, string name, RigidBody body)
        : base(id, name, EntityType.Vehicle)
    {
        PhysicsBody = body;
    }

    public float TargetSpeed { get; set; }

    public float TargetHeadingDegrees { get; set; }

    public MotionState MotionState { get; set; } = MotionState.Idle;

    public string CurrentWaypointId { get; set; } = string.Empty;

    public float CurrentSpeed => PhysicsBody?.LinearVelocity.Length() ?? 0.0f;

    public float AngularSpeed => PhysicsBody?.AngularVelocity.Length() ?? 0.0f;

    public float HeadingDegrees
    {
        get
        {
            var forward = Vector3.Transform(Vector3.UnitZ, Orientation);
            return MathF.Atan2(forward.X, forward.Z) * 180.0f / MathF.PI;
        }
    }

    public int SensorDetectionCount { get; set; }

    public int CollisionCount { get; set; }
}
