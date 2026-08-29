using System.Windows.Media;
using System.Windows.Media.Media3D;
using MediaColor = System.Windows.Media.Color;
using Vector3 = System.Numerics.Vector3;

namespace Forge3D.Editor.Rendering;

public static class MeshFactory
{
    public static GeometryModel3D CreateSphere(Vector3 center, float radius, MediaColor color)
    {
        const int thetaSegments = 12;
        const int phiSegments = 6;

        var mesh = new MeshGeometry3D();

        for (var phi = 0; phi <= phiSegments; phi++)
        {
            var v = phi / (double)phiSegments;
            var phiAngle = Math.PI * v;

            for (var theta = 0; theta <= thetaSegments; theta++)
            {
                var u = theta / (double)thetaSegments;
                var thetaAngle = Math.PI * 2.0 * u;
                var x = radius * Math.Sin(phiAngle) * Math.Cos(thetaAngle);
                var y = radius * Math.Cos(phiAngle);
                var z = radius * Math.Sin(phiAngle) * Math.Sin(thetaAngle);
                mesh.Positions.Add(ToPoint3D(center + new Vector3((float)x, (float)y, (float)z)));
                mesh.Normals.Add(new Vector3D(x, y, z));
            }
        }

        for (var phi = 0; phi < phiSegments; phi++)
        {
            for (var theta = 0; theta < thetaSegments; theta++)
            {
                var first = phi * (thetaSegments + 1) + theta;
                var second = first + thetaSegments + 1;
                mesh.TriangleIndices.Add(first);
                mesh.TriangleIndices.Add(second);
                mesh.TriangleIndices.Add(first + 1);
                mesh.TriangleIndices.Add(second);
                mesh.TriangleIndices.Add(second + 1);
                mesh.TriangleIndices.Add(first + 1);
            }
        }

        mesh.Freeze();
        return new GeometryModel3D(mesh, CreateMaterial(color));
    }

    public static GeometryModel3D CreateBox(Vector3 center, Vector3 halfExtents, MediaColor color)
    {
        var min = center - halfExtents;
        var max = center + halfExtents;
        var points = new[]
        {
            new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z), new Vector3(min.X, max.Y, min.Z),
            new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z)
        };

        var mesh = new MeshGeometry3D();
        foreach (var point in points)
        {
            mesh.Positions.Add(ToPoint3D(point));
        }

        AddFace(mesh, 0, 1, 2, 3);
        AddFace(mesh, 5, 4, 7, 6);
        AddFace(mesh, 4, 0, 3, 7);
        AddFace(mesh, 1, 5, 6, 2);
        AddFace(mesh, 3, 2, 6, 7);
        AddFace(mesh, 4, 5, 1, 0);

        mesh.Freeze();
        return new GeometryModel3D(mesh, CreateMaterial(color));
    }

    public static GeometryModel3D CreatePlane(MediaColor color)
    {
        var mesh = new MeshGeometry3D();
        mesh.Positions.Add(new Point3D(-12.0, 0.0, -12.0));
        mesh.Positions.Add(new Point3D(12.0, 0.0, -12.0));
        mesh.Positions.Add(new Point3D(12.0, 0.0, 12.0));
        mesh.Positions.Add(new Point3D(-12.0, 0.0, 12.0));
        AddFace(mesh, 0, 1, 2, 3);
        mesh.Freeze();
        return new GeometryModel3D(mesh, CreateMaterial(color));
    }

    public static GeometryModel3D CreateLine(Vector3 start, Vector3 end, MediaColor color, float thickness)
    {
        var direction = end - start;
        if (direction.LengthSquared() <= 0.000001f)
        {
            direction = Vector3.UnitY * 0.001f;
        }

        var normalized = Vector3.Normalize(direction);
        var up = Math.Abs(Vector3.Dot(normalized, Vector3.UnitY)) > 0.95f ? Vector3.UnitX : Vector3.UnitY;
        var side = Vector3.Normalize(Vector3.Cross(direction, up)) * thickness;
        var lift = Vector3.Normalize(Vector3.Cross(side, direction)) * thickness;

        var mesh = new MeshGeometry3D();
        var points = new[]
        {
            start - side - lift, start + side - lift, start + side + lift, start - side + lift,
            end - side - lift, end + side - lift, end + side + lift, end - side + lift
        };

        foreach (var point in points)
        {
            mesh.Positions.Add(ToPoint3D(point));
        }

        AddFace(mesh, 0, 1, 2, 3);
        AddFace(mesh, 5, 4, 7, 6);
        AddFace(mesh, 4, 0, 3, 7);
        AddFace(mesh, 1, 5, 6, 2);
        AddFace(mesh, 3, 2, 6, 7);
        AddFace(mesh, 4, 5, 1, 0);

        mesh.Freeze();
        return new GeometryModel3D(mesh, CreateMaterial(color));
    }

    public static Material CreateMaterial(MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Material material = new DiffuseMaterial(brush);
        material.Freeze();
        return material;
    }

    private static void AddFace(MeshGeometry3D mesh, int a, int b, int c, int d)
    {
        mesh.TriangleIndices.Add(a);
        mesh.TriangleIndices.Add(b);
        mesh.TriangleIndices.Add(c);
        mesh.TriangleIndices.Add(a);
        mesh.TriangleIndices.Add(c);
        mesh.TriangleIndices.Add(d);
    }

    private static Point3D ToPoint3D(Vector3 vector)
    {
        return new Point3D(vector.X, vector.Y, vector.Z);
    }
}
