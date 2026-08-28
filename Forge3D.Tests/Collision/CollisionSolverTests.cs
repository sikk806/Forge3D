using System.Numerics;
using Forge3D.Core;
using Forge3D.Core.Collision;
using Forge3D.Core.Dynamics;

namespace Forge3D.Tests.Collision;

public sealed class CollisionSolverTests
{
    [Fact]
    public void Step_GeneratesSpherePlaneContact()
    {
        var world = new PhysicsWorld();
        var sphereBody = new RigidBody("Sphere", new Vector3(0.0f, 0.4f, 0.0f));
        var planeBody = new RigidBody("Ground", Vector3.Zero) { IsStatic = true };

        world.AddCollider(new SphereCollider(sphereBody, 0.5f));
        world.AddCollider(new PlaneCollider(planeBody, Vector3.UnitY));

        world.Step(PhysicsSettings.DefaultFixedDeltaTime);

        Assert.NotEmpty(world.Contacts);
        Assert.True(world.LastStepStats.ContactCount > 0);
        Assert.True(world.LastStepStats.CandidatePairCount > 0);
    }

    [Fact]
    public void Step_BouncesSphereOffPlaneWhenRestitutionIsHigh()
    {
        var world = new PhysicsWorld { Gravity = Vector3.Zero };
        var rubber = new PhysicsMaterial(friction: 0.5f, restitution: 0.8f);
        var sphereBody = new RigidBody("Sphere", new Vector3(0.0f, 0.45f, 0.0f))
        {
            LinearVelocity = new Vector3(0.0f, -10.0f, 0.0f),
            Material = rubber
        };
        var planeBody = new RigidBody("Ground", Vector3.Zero)
        {
            IsStatic = true,
            Material = rubber
        };

        world.AddCollider(new SphereCollider(sphereBody, 0.5f, rubber));
        world.AddCollider(new PlaneCollider(planeBody, Vector3.UnitY, material: rubber));

        world.Step(PhysicsSettings.DefaultFixedDeltaTime);

        Assert.True(sphereBody.LinearVelocity.Y > 0.0f);
        Assert.True(world.Contacts[0].AppliedImpulse > 0.0f);
        Assert.NotEqual(Vector3.Zero, world.Contacts[0].RelativeVelocity);
    }

    [Fact]
    public void Step_GeneratesContactForRotatedBoxes()
    {
        var world = new PhysicsWorld { Gravity = Vector3.Zero };
        var boxA = new RigidBody("A", Vector3.Zero)
        {
            Orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4.0f)
        };
        var boxB = new RigidBody("B", new Vector3(1.0f, 0.0f, 0.0f))
        {
            Orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 6.0f)
        };

        world.AddCollider(new BoxCollider(boxA, Vector3.One * 0.6f));
        world.AddCollider(new BoxCollider(boxB, Vector3.One * 0.6f));

        world.Step(PhysicsSettings.DefaultFixedDeltaTime);

        Assert.NotEmpty(world.Contacts);
    }

    [Fact]
    public void Step_FrictionReducesTangentialVelocity()
    {
        var material = new PhysicsMaterial(friction: 1.0f, restitution: 0.0f);
        var world = new PhysicsWorld { Gravity = Vector3.Zero };
        var box = new RigidBody("Box", new Vector3(0.0f, 0.45f, 0.0f))
        {
            LinearVelocity = new Vector3(5.0f, -1.0f, 0.0f),
            Material = material
        };
        var ground = new RigidBody("Ground", Vector3.Zero)
        {
            IsStatic = true,
            Material = material
        };

        world.AddCollider(new BoxCollider(box, new Vector3(0.5f), material));
        world.AddCollider(new PlaneCollider(ground, Vector3.UnitY, material: material));

        world.Step(PhysicsSettings.DefaultFixedDeltaTime);

        Assert.True(MathF.Abs(box.LinearVelocity.X) < 5.0f);
    }
}
