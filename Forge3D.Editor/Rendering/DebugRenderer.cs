using System.Windows.Media;
using System.Windows.Media.Media3D;
using Forge3D.Core.Collision;
using Forge3D.Core.Simulation.Sensors;
using Forge3D.Editor.ViewModels;
using MediaColor = System.Windows.Media.Color;
using NumericsQuaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace Forge3D.Editor.Rendering;

public sealed class DebugRenderer
{
    private const int MaxDebugBodies = 120;
    private const int MaxDebugContacts = 80;

    public Model3DGroup Build(MainViewModel viewModel)
    {
        var group = new Model3DGroup();

        if (viewModel.ShowVelocityDebug)
        {
            AddVelocity(group, viewModel);
        }

        if (viewModel.ShowContactDebug || viewModel.ShowNormalDebug)
        {
            AddContacts(group, viewModel);
        }

        if (viewModel.ShowBoundsDebug)
        {
            AddBounds(group, viewModel);
        }

        AddEngineeringVisualization(group, viewModel);
        AddNavigationPath(group, viewModel);
        return group;
    }

    private static void AddVelocity(Model3DGroup group, MainViewModel viewModel)
    {
        foreach (var item in viewModel.Objects.Where(item => !item.Body.IsStatic).Take(MaxDebugBodies))
        {
            var start = item.Body.Position;
            var velocity = item.Body.LinearVelocity;
            if (velocity.LengthSquared() > 0.01f)
            {
                group.Children.Add(MeshFactory.CreateLine(start, start + Vector3.Normalize(velocity) * MathF.Min(velocity.Length() * 0.15f, 1.5f), Colors.Cyan, 0.025f));
            }
        }
    }

    private static void AddContacts(Model3DGroup group, MainViewModel viewModel)
    {
        foreach (var contact in viewModel.World.Contacts.Take(MaxDebugContacts))
        {
            if (viewModel.ShowContactDebug)
            {
                group.Children.Add(MeshFactory.CreateSphere(contact.Point, 0.07f, Colors.OrangeRed));
            }

            if (viewModel.ShowNormalDebug)
            {
                group.Children.Add(MeshFactory.CreateLine(contact.Point, contact.Point + contact.Normal * 0.75f, Colors.LimeGreen, 0.03f));
            }
        }
    }

    private static void AddBounds(Model3DGroup group, MainViewModel viewModel)
    {
        foreach (var item in viewModel.Objects.Where(item => item.Collider is not PlaneCollider).Take(MaxDebugBodies))
        {
            AddBounds(group, item.Collider.ComputeBounds());
        }
    }

    private static void AddEngineeringVisualization(Model3DGroup group, MainViewModel viewModel)
    {
        var waypoints = viewModel.Waypoints.ToList();
        for (var i = 0; i < waypoints.Count; i++)
        {
            var waypoint = waypoints[i];
            var color = waypoint.IsReached ? Colors.LightGreen : Colors.Yellow;
            group.Children.Add(MeshFactory.CreateSphere(waypoint.Position + new Vector3(0.0f, 0.18f, 0.0f), 0.18f, color));

            if (i > 0)
            {
                group.Children.Add(MeshFactory.CreateLine(waypoints[i - 1].Position + new Vector3(0.0f, 0.12f, 0.0f), waypoint.Position + new Vector3(0.0f, 0.12f, 0.0f), Colors.Yellow, 0.025f));
            }
        }

        if (viewModel.SensorFovDebug && viewModel.Sensor is { State: not SensorState.Fault and not SensorState.Offline } sensor)
        {
            var origin = sensor.Owner.Position + new Vector3(0.0f, 0.35f, 0.0f);
            var forward = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, sensor.Owner.Orientation));
            var left = RotateAroundY(forward, -sensor.FieldOfViewDegrees * 0.5f);
            var right = RotateAroundY(forward, sensor.FieldOfViewDegrees * 0.5f);
            group.Children.Add(MeshFactory.CreateLine(origin, origin + left * sensor.Range, MediaColor.FromRgb(120, 220, 255), 0.02f));
            group.Children.Add(MeshFactory.CreateLine(origin, origin + forward * sensor.Range, MediaColor.FromRgb(80, 180, 230), 0.015f));
            group.Children.Add(MeshFactory.CreateLine(origin, origin + right * sensor.Range, MediaColor.FromRgb(120, 220, 255), 0.02f));
        }
    }

    private static void AddNavigationPath(Model3DGroup group, MainViewModel viewModel)
    {
        if (!viewModel.ShowNavigationPathDebug || viewModel.NavigationPath.Count < 2)
        {
            return;
        }

        for (var i = 1; i < viewModel.NavigationPath.Count; i++)
        {
            var previous = viewModel.NavigationPath[i - 1];
            var current = viewModel.NavigationPath[i];
            group.Children.Add(MeshFactory.CreateLine(
                new Vector3(previous.X, 0.2f, previous.Z),
                new Vector3(current.X, 0.2f, current.Z),
                MediaColor.FromRgb(255, 180, 80),
                0.035f));
        }

        var start = viewModel.NavigationPath[0];
        var goal = viewModel.NavigationPath[^1];
        group.Children.Add(MeshFactory.CreateSphere(new Vector3(start.X, 0.28f, start.Z), 0.16f, MediaColor.FromRgb(120, 220, 255)));
        group.Children.Add(MeshFactory.CreateSphere(new Vector3(goal.X, 0.28f, goal.Z), 0.22f, MediaColor.FromRgb(255, 120, 80)));
    }

    private static Vector3 RotateAroundY(Vector3 vector, float degrees)
    {
        return Vector3.Normalize(Vector3.Transform(vector, NumericsQuaternion.CreateFromAxisAngle(Vector3.UnitY, degrees * MathF.PI / 180.0f)));
    }

    private static void AddBounds(Model3DGroup group, Core.Mathematics.Aabb bounds)
    {
        var min = bounds.Min;
        var max = bounds.Max;
        var p = new[]
        {
            new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z), new Vector3(min.X, max.Y, min.Z),
            new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z)
        };

        var color = MediaColor.FromRgb(255, 230, 120);
        AddEdge(group, p[0], p[1], color);
        AddEdge(group, p[1], p[2], color);
        AddEdge(group, p[2], p[3], color);
        AddEdge(group, p[3], p[0], color);
        AddEdge(group, p[4], p[5], color);
        AddEdge(group, p[5], p[6], color);
        AddEdge(group, p[6], p[7], color);
        AddEdge(group, p[7], p[4], color);
        AddEdge(group, p[0], p[4], color);
        AddEdge(group, p[1], p[5], color);
        AddEdge(group, p[2], p[6], color);
        AddEdge(group, p[3], p[7], color);
    }

    private static void AddEdge(Model3DGroup group, Vector3 a, Vector3 b, MediaColor color)
    {
        group.Children.Add(MeshFactory.CreateLine(a, b, color, 0.012f));
    }
}
