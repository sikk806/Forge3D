using System.Numerics;
using Forge3D.Core.Collision;
using Forge3D.Core.Dynamics;

namespace Forge3D.Core.Solver;

public sealed class ImpulseContactSolver : IContactSolver
{
    private const float PenetrationSlop = 0.01f;
    private const float PenetrationPercent = 0.8f;
    private const int VelocityIterations = 8;

    public void Solve(IList<Contact> contacts, float deltaTime)
    {
        _ = deltaTime;

        for (var iteration = 0; iteration < VelocityIterations; iteration++)
        {
            for (var i = 0; i < contacts.Count; i++)
            {
                contacts[i] = SolveVelocity(contacts[i], iteration == 0);
            }
        }

        foreach (var contact in contacts)
        {
            CorrectPosition(contact);
        }
    }

    private static Contact SolveVelocity(Contact contact, bool resetAppliedImpulse)
    {
        var bodyA = contact.BodyA;
        var bodyB = contact.BodyB;
        var radiusA = contact.Point - bodyA.Position;
        var radiusB = contact.Point - bodyB.Position;
        var inverseMassSum = ComputeEffectiveMass(bodyA, bodyB, radiusA, radiusB, contact.Normal);

        if (inverseMassSum <= 0.0f)
        {
            return contact;
        }

        var relativeVelocity = GetVelocityAtPoint(bodyB, radiusB) - GetVelocityAtPoint(bodyA, radiusA);
        var appliedImpulse = resetAppliedImpulse ? 0.0f : contact.AppliedImpulse;
        var velocityAlongNormal = Vector3.Dot(relativeVelocity, contact.Normal);

        if (velocityAlongNormal > 0.0f)
        {
            return contact with { RelativeVelocity = relativeVelocity, AppliedImpulse = appliedImpulse };
        }

        var impulseScalar = -(1.0f + contact.Restitution) * velocityAlongNormal;
        impulseScalar /= inverseMassSum;
        appliedImpulse += MathF.Abs(impulseScalar);

        var impulse = impulseScalar * contact.Normal;
        bodyA.ApplyImpulseAtRelativePoint(-impulse, radiusA);
        bodyB.ApplyImpulseAtRelativePoint(impulse, radiusB);

        relativeVelocity = GetVelocityAtPoint(bodyB, radiusB) - GetVelocityAtPoint(bodyA, radiusA);
        var tangent = relativeVelocity - Vector3.Dot(relativeVelocity, contact.Normal) * contact.Normal;

        if (tangent.LengthSquared() <= 0.000001f)
        {
            return contact with { RelativeVelocity = relativeVelocity, AppliedImpulse = appliedImpulse };
        }

        tangent = Vector3.Normalize(tangent);
        var tangentEffectiveMass = ComputeEffectiveMass(bodyA, bodyB, radiusA, radiusB, tangent);
        if (tangentEffectiveMass <= 0.0f)
        {
            return contact with { RelativeVelocity = relativeVelocity, AppliedImpulse = appliedImpulse };
        }

        var frictionScalar = -Vector3.Dot(relativeVelocity, tangent) / tangentEffectiveMass;
        var maxFriction = impulseScalar * contact.Friction;
        frictionScalar = Math.Clamp(frictionScalar, -maxFriction, maxFriction);

        var frictionImpulse = frictionScalar * tangent;
        bodyA.ApplyImpulseAtRelativePoint(-frictionImpulse, radiusA);
        bodyB.ApplyImpulseAtRelativePoint(frictionImpulse, radiusB);

        return contact with
        {
            RelativeVelocity = relativeVelocity,
            AppliedImpulse = appliedImpulse + MathF.Abs(frictionScalar)
        };
    }

    private static void CorrectPosition(Contact contact)
    {
        var bodyA = contact.BodyA;
        var bodyB = contact.BodyB;
        var inverseMassSum = bodyA.InverseMass + bodyB.InverseMass;

        if (inverseMassSum <= 0.0f)
        {
            return;
        }

        var correctionMagnitude = MathF.Max(contact.Penetration - PenetrationSlop, 0.0f) / inverseMassSum * PenetrationPercent;
        var correction = correctionMagnitude * contact.Normal;

        if (!bodyA.IsStatic)
        {
            bodyA.Position -= correction * bodyA.InverseMass;
        }

        if (!bodyB.IsStatic)
        {
            bodyB.Position += correction * bodyB.InverseMass;
        }
    }

    private static Vector3 GetVelocityAtPoint(RigidBody body, Vector3 relativePoint)
    {
        return body.LinearVelocity + Vector3.Cross(body.AngularVelocity, relativePoint);
    }

    private static float ComputeEffectiveMass(RigidBody bodyA, RigidBody bodyB, Vector3 radiusA, Vector3 radiusB, Vector3 axis)
    {
        return bodyA.InverseMass
            + bodyB.InverseMass
            + Vector3.Dot(Vector3.Cross(bodyA.InverseInertia * Vector3.Cross(radiusA, axis), radiusA), axis)
            + Vector3.Dot(Vector3.Cross(bodyB.InverseInertia * Vector3.Cross(radiusB, axis), radiusB), axis);
    }
}
