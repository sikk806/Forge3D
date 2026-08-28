using System.Numerics;

namespace Forge3D.Core.Mathematics;

public struct Transform
{
    public Vector3 Position { get; set; }

    public Quaternion Rotation { get; set; }

    public Vector3 Scale { get; set; }

    public Transform(Vector3 position)
        : this(position, Quaternion.Identity, Vector3.One)
    {
    }

    public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Position = position;
        Rotation = Quaternion.Normalize(rotation);
        Scale = scale;
    }

    public static Transform Identity => new(Vector3.Zero, Quaternion.Identity, Vector3.One);
}
