using System.Numerics;

namespace Forge3D.Contracts.States;

public sealed record EntityStateDto(
    int Id,
    string Name,
    string EntityType,
    string ColliderType,
    Vector3 Position,
    Quaternion Orientation,
    Vector3 PreviousPosition,
    Quaternion PreviousOrientation,
    Vector3 LinearVelocity,
    Vector3 AngularVelocity,
    Vector3 HalfExtents,
    float Radius,
    bool IsStatic,
    bool IsActive,
    float Mass,
    float LinearDamping,
    float AngularDamping,
    float Friction,
    float Restitution);
