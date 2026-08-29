using System.Numerics;
using Forge3D.Core.Constraints;
using Forge3D.Core.Dynamics;

namespace Forge3D.Tests.Dynamics;

public sealed class RigidBodyTests
{
    [Fact]
    public void ApplyImpulse_ChangesLinearVelocity()
    {
        var body = new RigidBody("Test Body", Vector3.Zero)
        {
            Mass = 2.0f
        };

        body.ApplyImpulse(new Vector3(4.0f, 0.0f, 0.0f));

        Assert.Equal(new Vector3(2.0f, 0.0f, 0.0f), body.LinearVelocity);
    }

    [Fact]
    public void ApplyImpulseAtPoint_ChangesAngularVelocity()
    {
        var body = new RigidBody("Test Body", Vector3.Zero);

        body.ApplyImpulseAtPoint(new Vector3(0.0f, 0.0f, 2.0f), new Vector3(1.0f, 0.0f, 0.0f));

        Assert.NotEqual(Vector3.Zero, body.AngularVelocity);
    }

    [Fact]
    public void ApplyForce_ChangesVelocityAfterIntegration()
    {
        var body = new RigidBody("Test Body", Vector3.Zero)
        {
            Mass = 2.0f
        };

        body.ApplyForce(new Vector3(10.0f, 0.0f, 0.0f));
        body.IntegrateForces(Vector3.Zero, 1.0f);

        Assert.True(body.LinearVelocity.X > 4.9f);
    }

    [Fact]
    public void ApplyTorque_ChangesAngularVelocityAfterIntegration()
    {
        var body = new RigidBody("Test Body", Vector3.Zero);

        body.ApplyTorque(new Vector3(0.0f, 0.0f, 2.0f));
        body.IntegrateForces(Vector3.Zero, 1.0f);

        Assert.NotEqual(Vector3.Zero, body.AngularVelocity);
    }

    [Fact]
    public void StaticBody_DoesNotMoveWhenForceIsApplied()
    {
        var body = new RigidBody("Ground", Vector3.Zero)
        {
            IsStatic = true
        };

        body.ApplyForce(new Vector3(0.0f, 100.0f, 0.0f));
        body.IntegrateForces(new Vector3(0.0f, -9.81f, 0.0f), 1.0f);
        body.IntegrateTransform(1.0f);

        Assert.Equal(Vector3.Zero, body.Position);
        Assert.Equal(Vector3.Zero, body.LinearVelocity);
    }

    [Fact]
    public void IntegrateTransform_RespectsPlanarXzConstraints()
    {
        var body = new RigidBody("Vehicle", new Vector3(0.0f, 0.45f, 0.0f))
        {
            Constraints = MotionConstraints.PlanarXZ,
            LinearVelocity = new Vector3(1.0f, -5.0f, 2.0f),
            AngularVelocity = new Vector3(3.0f, 4.0f, 5.0f)
        };

        body.IntegrateTransform(1.0f);

        Assert.Equal(0.45f, body.Position.Y);
        Assert.Equal(0.0f, body.LinearVelocity.Y);
        Assert.Equal(0.0f, body.AngularVelocity.X);
        Assert.Equal(0.0f, body.AngularVelocity.Z);
        Assert.NotEqual(0.0f, body.AngularVelocity.Y);
    }
}
