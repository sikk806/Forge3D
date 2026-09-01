using System.Numerics;

namespace Forge3D.Contracts.States;

public sealed record ContactStateDto(
    int BodyAEntityId,
    int BodyBEntityId,
    string BodyAName,
    string BodyBName,
    Vector3 Point,
    Vector3 Normal,
    float Penetration,
    Vector3 RelativeVelocity,
    float AppliedImpulse);
