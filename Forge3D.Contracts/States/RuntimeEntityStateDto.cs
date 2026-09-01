using System.Numerics;

namespace Forge3D.Contracts.States;

public sealed record RuntimeEntityStateDto(
    string Id,
    string Name,
    string EntityType,
    int? PhysicsEntityId,
    Vector3 Position);
