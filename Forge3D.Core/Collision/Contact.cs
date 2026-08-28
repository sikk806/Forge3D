using System.Numerics;
using Forge3D.Core.Dynamics;

namespace Forge3D.Core.Collision;

public readonly record struct Contact(
    RigidBody BodyA,
    RigidBody BodyB,
    Vector3 Point,
    Vector3 Normal,
    float Penetration,
    float Friction,
    float Restitution,
    Vector3 RelativeVelocity = default,
    float AppliedImpulse = 0.0f);
