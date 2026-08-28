using System.Numerics;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Mathematics;

namespace Forge3D.Core.Collision;

public sealed class BoxCollider : Collider
{
    public BoxCollider(RigidBody body, Vector3 halfExtents, PhysicsMaterial? material = null)
        : base(body, material)
    {
        if (halfExtents.X <= 0.0f || halfExtents.Y <= 0.0f || halfExtents.Z <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(halfExtents), "Half extents must be greater than zero on every axis.");
        }

        HalfExtents = halfExtents;
    }

    public Vector3 HalfExtents { get; }

    public override ColliderType Type => ColliderType.Box;

    public override Aabb ComputeBounds()
    {
        var corners = GetCorners();
        var min = corners[0];
        var max = corners[0];

        foreach (var corner in corners)
        {
            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }

        return new Aabb(min, max);
    }

    public Vector3[] GetAxes()
    {
        return
        [
            Vector3.Normalize(Vector3.Transform(Vector3.UnitX, Body.Orientation)),
            Vector3.Normalize(Vector3.Transform(Vector3.UnitY, Body.Orientation)),
            Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, Body.Orientation))
        ];
    }

    public Vector3[] GetCorners()
    {
        var axes = GetAxes();
        var center = Body.Position;
        var x = axes[0] * HalfExtents.X;
        var y = axes[1] * HalfExtents.Y;
        var z = axes[2] * HalfExtents.Z;

        return
        [
            center - x - y - z,
            center + x - y - z,
            center + x + y - z,
            center - x + y - z,
            center - x - y + z,
            center + x - y + z,
            center + x + y + z,
            center - x + y + z
        ];
    }

    public Vector3 ClosestPoint(Vector3 point)
    {
        var axes = GetAxes();
        var closest = Body.Position;
        var delta = point - Body.Position;

        closest += axes[0] * Math.Clamp(Vector3.Dot(delta, axes[0]), -HalfExtents.X, HalfExtents.X);
        closest += axes[1] * Math.Clamp(Vector3.Dot(delta, axes[1]), -HalfExtents.Y, HalfExtents.Y);
        closest += axes[2] * Math.Clamp(Vector3.Dot(delta, axes[2]), -HalfExtents.Z, HalfExtents.Z);

        return closest;
    }
}
