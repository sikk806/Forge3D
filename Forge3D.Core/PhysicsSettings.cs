using System.Numerics;

namespace Forge3D.Core;

public sealed class PhysicsSettings
{
    public const float DefaultFixedDeltaTime = 1.0f / 60.0f;

    public Vector3 Gravity { get; set; } = new(0.0f, -9.81f, 0.0f);

    public float FixedDeltaTime { get; set; } = DefaultFixedDeltaTime;

    public int MaxSubSteps { get; set; } = 8;
}
