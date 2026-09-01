using System.Windows.Media;
using System.Windows.Media.Media3D;
using MediaQuaternion = System.Windows.Media.Media3D.Quaternion;
using NumericsQuaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace Forge3D.Editor.Rendering;

public sealed class SceneVisual
{
    private readonly QuaternionRotation3D _rotation = new();
    private readonly TranslateTransform3D _translation = new();
    private Color _lastColor;

    public SceneVisual(int entityId, bool isPlane, ModelUIElement3D element, GeometryModel3D model)
    {
        EntityId = entityId;
        IsPlane = isPlane;
        Element = element;
        Model = model;

        if (!isPlane)
        {
            var transform = new Transform3DGroup();
            transform.Children.Add(new RotateTransform3D(_rotation));
            transform.Children.Add(_translation);
            Model.Transform = transform;
        }
    }

    public int EntityId { get; }

    public bool IsPlane { get; }

    public ModelUIElement3D Element { get; }

    public GeometryModel3D Model { get; }

    public void Update(Vector3 position, NumericsQuaternion orientation, Color color)
    {
        if (_lastColor != color)
        {
            Model.Material = MeshFactory.CreateMaterial(color);
            Model.BackMaterial = Model.Material;
            _lastColor = color;
        }

        if (IsPlane)
        {
            return;
        }

        _rotation.Quaternion = new MediaQuaternion(orientation.X, orientation.Y, orientation.Z, orientation.W);
        _translation.OffsetX = position.X;
        _translation.OffsetY = position.Y;
        _translation.OffsetZ = position.Z;
    }
}
