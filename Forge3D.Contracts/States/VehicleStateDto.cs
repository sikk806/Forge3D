using System.Numerics;

namespace Forge3D.Contracts.States;

public sealed record VehicleStateDto(
    string Id,
    string Name,
    int? PhysicsEntityId,
    Vector3 Position,
    Quaternion Orientation,
    float HeadingDegrees,
    float TargetSpeed,
    float TargetHeadingDegrees,
    string MotionState,
    int CollisionCount);
