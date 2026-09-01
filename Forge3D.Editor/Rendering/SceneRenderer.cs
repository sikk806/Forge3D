using System.Windows.Media;
using System.Windows.Media.Media3D;
using Forge3D.Contracts.States;
using MediaColor = System.Windows.Media.Color;
using Vector3 = System.Numerics.Vector3;

namespace Forge3D.Editor.Rendering;

public sealed class SceneRenderer
{
    public ModelVisual3D CreateGrid()
    {
        var grid = new Model3DGroup();
        var minor = MediaColor.FromRgb(38, 52, 60);
        var major = MediaColor.FromRgb(54, 74, 84);
        var axisX = MediaColor.FromRgb(110, 58, 58);
        var axisZ = MediaColor.FromRgb(58, 83, 116);

        for (var i = -10; i <= 10; i++)
        {
            var thickness = i == 0 ? 0.018f : i % 5 == 0 ? 0.012f : 0.008f;
            grid.Children.Add(MeshFactory.CreateLine(new Vector3(i, 0.002f, -10), new Vector3(i, 0.002f, 10), i == 0 ? axisZ : i % 5 == 0 ? major : minor, thickness));
            grid.Children.Add(MeshFactory.CreateLine(new Vector3(-10, 0.002f, i), new Vector3(10, 0.002f, i), i == 0 ? axisX : i % 5 == 0 ? major : minor, thickness));
        }

        return new ModelVisual3D { Content = grid };
    }

    public SceneVisual CreateVisual(EntityStateDto entity)
    {
        var model = entity.ColliderType switch
        {
            "Sphere" => MeshFactory.CreateSphere(Vector3.Zero, entity.Radius, Colors.DeepSkyBlue),
            "Box" => MeshFactory.CreateBox(Vector3.Zero, entity.HalfExtents, Colors.LightSteelBlue),
            "Plane" => MeshFactory.CreatePlane(MediaColor.FromRgb(58, 66, 58)),
            _ => new GeometryModel3D()
        };

        model.BackMaterial = model.Material;
        var element = new ModelUIElement3D { Model = model };
        return new SceneVisual(entity.Id, entity.ColliderType == "Plane", element, model);
    }

    public void UpdateVisual(SceneVisual visual, EntityStateDto entity, bool selected, float interpolationAlpha)
    {
        var position = entity.ColliderType == "Plane"
            ? entity.Position
            : Vector3.Lerp(entity.PreviousPosition, entity.Position, interpolationAlpha);
        var orientation = entity.ColliderType == "Plane"
            ? entity.Orientation
            : System.Numerics.Quaternion.Slerp(entity.PreviousOrientation, entity.Orientation, interpolationAlpha);

        visual.Update(position, orientation, GetColor(entity, selected));
    }

    private static MediaColor GetColor(EntityStateDto entity, bool selected)
    {
        if (entity.ColliderType == "Plane")
        {
            return MediaColor.FromRgb(58, 66, 58);
        }

        if (selected)
        {
            return Colors.Gold;
        }

        return entity.ColliderType switch
        {
            "Sphere" => Colors.DeepSkyBlue,
            "Box" => Colors.LightSteelBlue,
            _ => Colors.White
        };
    }
}
