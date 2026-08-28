using System.Numerics;
using Forge3D.Core.Collision;
using Forge3D.Core.Dynamics;

namespace Forge3D.Tests.Collision;

public sealed class ColliderTests
{
    [Fact]
    public void SphereCollider_ComputesWorldBoundsFromBodyPosition()
    {
        var body = new RigidBody("Sphere", new Vector3(1.0f, 2.0f, 3.0f));
        var collider = new SphereCollider(body, radius: 2.0f);

        var bounds = collider.ComputeBounds();

        Assert.Equal(new Vector3(-1.0f, 0.0f, 1.0f), bounds.Min);
        Assert.Equal(new Vector3(3.0f, 4.0f, 5.0f), bounds.Max);
    }

    [Fact]
    public void Aabb_ReportsIntersection()
    {
        var bodyA = new RigidBody("A", Vector3.Zero);
        var bodyB = new RigidBody("B", new Vector3(0.5f, 0.0f, 0.0f));
        var colliderA = new BoxCollider(bodyA, Vector3.One);
        var colliderB = new BoxCollider(bodyB, Vector3.One);

        Assert.True(colliderA.ComputeBounds().Intersects(colliderB.ComputeBounds()));
    }

    [Fact]
    public void RotatedBoxCollider_ComputesExpandedWorldBounds()
    {
        var body = new RigidBody("Rotated Box", Vector3.Zero)
        {
            Orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4.0f)
        };
        var collider = new BoxCollider(body, new Vector3(1.0f, 1.0f, 1.0f));

        var bounds = collider.ComputeBounds();

        Assert.True(bounds.Size.X > 2.0f);
        Assert.True(bounds.Size.Z > 2.0f);
    }
}
