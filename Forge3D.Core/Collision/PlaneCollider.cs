using System.Numerics;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Mathematics;

namespace Forge3D.Core.Collision;

public sealed class PlaneCollider : Collider
{
    public PlaneCollider(RigidBody body, Vector3 normal, float distanceFromOrigin = 0.0f, PhysicsMaterial? material = null)
        : base(body, material)
    {
        if (normal.LengthSquared() <= 0.0f)
        {
            throw new ArgumentException("Plane normal must not be zero.", nameof(normal));
        }

        Normal = Vector3.Normalize(normal);
        DistanceFromOrigin = distanceFromOrigin;
    }

    public Vector3 Normal { get; }

    public float DistanceFromOrigin { get; }

    public override ColliderType Type => ColliderType.Plane;

    public override Aabb ComputeBounds()
    {
        var extent = new Vector3(10000.0f);
        return new Aabb(-extent, extent);
    }
}
