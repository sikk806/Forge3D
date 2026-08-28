using Forge3D.Core.Dynamics;
using Forge3D.Core.Mathematics;
using System.Threading;

namespace Forge3D.Core.Collision;

public abstract class Collider
{
    private static int _nextId;

    protected Collider(RigidBody body, PhysicsMaterial? material = null)
    {
        Id = Interlocked.Increment(ref _nextId);
        Body = body;
        Material = material ?? body.Material;
    }

    public int Id { get; }

    public string Name
    {
        get => Body.Name;
        set => Body.Name = value;
    }

    public RigidBody Body { get; }

    public PhysicsMaterial Material { get; set; }

    public abstract ColliderType Type { get; }

    public abstract Aabb ComputeBounds();
}
