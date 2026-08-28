using System.Numerics;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Mathematics;

namespace Forge3D.Core.Collision;

public sealed class SphereCollider : Collider
{
    public SphereCollider(RigidBody body, float radius, PhysicsMaterial? material = null)
        : base(body, material)
    {
        if (radius <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");
        }

        Radius = radius;
    }

    public float Radius { get; }

    public override ColliderType Type => ColliderType.Sphere;

    public override Aabb ComputeBounds()
    {
        var extent = new Vector3(Radius);
        return new Aabb(Body.Position - extent, Body.Position + extent);
    }
}
