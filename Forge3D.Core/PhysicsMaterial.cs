namespace Forge3D.Core;

public sealed class PhysicsMaterial
{
    public PhysicsMaterial(float friction = 0.5f, float restitution = 0.1f)
    {
        Friction = friction;
        Restitution = restitution;
    }

    public float Friction { get; set; }

    public float Restitution { get; set; }

    public static PhysicsMaterial Default => new();

    public static PhysicsMaterial Rubber => new(friction: 0.8f, restitution: 0.9f);

    public static PhysicsMaterial Steel => new(friction: 0.4f, restitution: 0.2f);

    public static PhysicsMaterial Ice => new(friction: 0.05f, restitution: 0.1f);
}
