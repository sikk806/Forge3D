using System.Numerics;

namespace Forge3D.Core.Mathematics;

public readonly record struct Aabb(Vector3 Min, Vector3 Max)
{
    public Vector3 Center => (Min + Max) * 0.5f;

    public Vector3 Size => Max - Min;

    public bool Intersects(Aabb other)
    {
        return Min.X <= other.Max.X && Max.X >= other.Min.X
            && Min.Y <= other.Max.Y && Max.Y >= other.Min.Y
            && Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
    }
}
