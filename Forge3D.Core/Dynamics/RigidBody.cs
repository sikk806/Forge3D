using System.Numerics;
using Forge3D.Core.Constraints;
using Forge3D.Core.Mathematics;

namespace Forge3D.Core.Dynamics;

public sealed class RigidBody
{
    private float _mass = 1.0f;
    private Vector3 _forceAccumulator;
    private Vector3 _torqueAccumulator;

    public RigidBody(string name, Vector3 position)
    {
        Name = name;
        Transform = new Transform(position);
        Material = PhysicsMaterial.Default;
    }

    public string Name { get; set; }

    public Transform Transform { get; set; }

    public Vector3 Position
    {
        get => Transform.Position;
        set => Transform = new Transform(value, Orientation, Transform.Scale);
    }

    public Quaternion Orientation
    {
        get => Transform.Rotation;
        set => Transform = new Transform(Position, value, Transform.Scale);
    }

    public Vector3 LinearVelocity { get; set; }

    public Vector3 AngularVelocity { get; set; }

    public float Mass
    {
        get => _mass;
        set
        {
            if (value <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Mass must be greater than zero.");
            }

            _mass = value;
        }
    }

    public float InverseMass => IsStatic ? 0.0f : 1.0f / Mass;

    public Vector3 InverseInertia => IsStatic
        ? Vector3.Zero
        : new Vector3(
            SafeInverse((1.0f / 12.0f) * Mass * ((HalfExtents.Y * 2.0f * HalfExtents.Y * 2.0f) + (HalfExtents.Z * 2.0f * HalfExtents.Z * 2.0f))),
            SafeInverse((1.0f / 12.0f) * Mass * ((HalfExtents.X * 2.0f * HalfExtents.X * 2.0f) + (HalfExtents.Z * 2.0f * HalfExtents.Z * 2.0f))),
            SafeInverse((1.0f / 12.0f) * Mass * ((HalfExtents.X * 2.0f * HalfExtents.X * 2.0f) + (HalfExtents.Y * 2.0f * HalfExtents.Y * 2.0f))));

    public float LinearDamping { get; set; } = 0.01f;

    public float AngularDamping { get; set; } = 0.05f;

    public bool IsStatic { get; set; }

    public bool IsSleeping { get; set; }

    public PhysicsMaterial Material { get; set; }

    public Vector3 HalfExtents { get; set; } = Vector3.One * 0.5f;

    public MotionConstraints Constraints { get; set; } = MotionConstraints.Free3D;

    public Vector3 ForceAccumulator => _forceAccumulator;

    public Vector3 TorqueAccumulator => _torqueAccumulator;

    public void ApplyForce(Vector3 force)
    {
        if (CanMove)
        {
            _forceAccumulator += force;
        }
    }

    public void ApplyTorque(Vector3 torque)
    {
        if (CanMove)
        {
            _torqueAccumulator += torque;
        }
    }

    public void ApplyImpulse(Vector3 impulse)
    {
        if (CanMove)
        {
            LinearVelocity += impulse * InverseMass;
        }
    }

    public void ApplyImpulseAtPoint(Vector3 impulse, Vector3 worldPoint)
    {
        if (!CanMove)
        {
            return;
        }

        ApplyImpulseAtRelativePoint(impulse, worldPoint - Position);
    }

    public void ApplyImpulseAtRelativePoint(Vector3 impulse, Vector3 relativePoint)
    {
        if (!CanMove)
        {
            return;
        }

        LinearVelocity += impulse * InverseMass;
        AngularVelocity += MultiplyByInverseInertia(Vector3.Cross(relativePoint, impulse));
    }

    public void IntegrateForces(Vector3 gravity, float deltaTime)
    {
        if (!CanMove || deltaTime <= 0.0f)
        {
            return;
        }

        var acceleration = gravity + (_forceAccumulator * InverseMass);
        LinearVelocity += acceleration * deltaTime;
        AngularVelocity += MultiplyByInverseInertia(_torqueAccumulator) * deltaTime;

        LinearVelocity *= MathF.Max(0.0f, 1.0f - (LinearDamping * deltaTime));
        AngularVelocity *= MathF.Max(0.0f, 1.0f - (AngularDamping * deltaTime));
        ApplyVelocityConstraints();
    }

    public void IntegrateTransform(float deltaTime)
    {
        if (!CanMove || deltaTime <= 0.0f)
        {
            return;
        }

        ApplyVelocityConstraints();
        Position += LinearVelocity * deltaTime;

        if (AngularVelocity.LengthSquared() > 0.0f)
        {
            var angular = new Quaternion(AngularVelocity * deltaTime, 0.0f);
            var rotation = Orientation;
            rotation += (angular * rotation) * 0.5f;
            Orientation = Quaternion.Normalize(rotation);
        }
    }

    public void ClearForces()
    {
        _forceAccumulator = Vector3.Zero;
        _torqueAccumulator = Vector3.Zero;
    }

    private bool CanMove => !IsStatic && !IsSleeping;

    private Vector3 MultiplyByInverseInertia(Vector3 vector)
    {
        var local = Vector3.Transform(vector, Quaternion.Conjugate(Orientation));
        var scaled = local * InverseInertia;
        return Vector3.Transform(scaled, Orientation);
    }

    private void ApplyVelocityConstraints()
    {
        LinearVelocity = ApplyLinearConstraints(LinearVelocity);
        AngularVelocity = ApplyAngularConstraints(AngularVelocity);
    }

    private Vector3 ApplyLinearConstraints(Vector3 velocity)
    {
        return new Vector3(
            Constraints.LockTranslationX ? 0.0f : velocity.X,
            Constraints.LockTranslationY ? 0.0f : velocity.Y,
            Constraints.LockTranslationZ ? 0.0f : velocity.Z);
    }

    private Vector3 ApplyAngularConstraints(Vector3 velocity)
    {
        return new Vector3(
            Constraints.LockRotationX ? 0.0f : velocity.X,
            Constraints.LockRotationY ? 0.0f : velocity.Y,
            Constraints.LockRotationZ ? 0.0f : velocity.Z);
    }

    private static float SafeInverse(float value)
    {
        return value <= 0.000001f ? 0.0f : 1.0f / value;
    }
}
