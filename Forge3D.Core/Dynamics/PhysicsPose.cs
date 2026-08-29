using System.Numerics;

namespace Forge3D.Core.Dynamics;

public readonly record struct PhysicsPose(Vector3 Position, Quaternion Orientation)
{
    public static PhysicsPose Interpolate(PhysicsPose previous, PhysicsPose current, float alpha)
    {
        var clamped = Math.Clamp(alpha, 0.0f, 1.0f);
        return new PhysicsPose(
            Vector3.Lerp(previous.Position, current.Position, clamped),
            Quaternion.Normalize(Quaternion.Slerp(previous.Orientation, current.Orientation, clamped)));
    }
}
