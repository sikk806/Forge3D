using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Forge3D.Core.Collision;
using Forge3D.Core.Simulation;
using Forge3D.Core.Simulation.Sensors;
using Forge3D.Editor.ViewModels;
using MediaColor = System.Windows.Media.Color;
using NumericsQuaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace Forge3D.Editor;

public partial class MainWindow : Window
{
    private const int MaxDebugBodies = 120;
    private const int MaxDebugContacts = 80;

    private readonly MainViewModel _viewModel;
    private readonly Dictionary<int, RenderEntry> _modelsByColliderId = [];
    private readonly Dictionary<ModelUIElement3D, int> _colliderIdsByModel = [];
    private readonly Dictionary<ModelUIElement3D, Vector3> _moveAxesByHandle = [];
    private Point _lastMousePosition;
    private bool _isOrbiting;
    private bool _isPanning;
    private bool _isZooming;
    private bool _isRightMouseHeld;
    private bool _isMovingSelection;
    private bool _isMovingOnViewPlane;
    private Vector3 _moveAxis;
    private Vector3 _moveStartPosition;
    private Vector3 _moveStartCameraRight;
    private Vector3 _moveStartCameraUp;
    private float _moveStartDepth;
    private Point _moveStartMousePosition;
    private double _yaw = -30.0;
    private double _pitch = 28.0;
    private double _distance = 13.0;
    private Vector3 _cameraTarget = new(0.0f, 1.5f, 0.0f);

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.SceneChanged += (_, _) => RebuildScene();
        _viewModel.SimulationAdvanced += (_, _) =>
        {
            SyncScene();
            DrawGraph();
        };
        UpdateCamera();
        RebuildScene();
        DrawGraph();
    }

    private void RebuildScene()
    {
        SceneRoot.Children.Clear();
        DebugRoot.Children.Clear();
        _modelsByColliderId.Clear();
        _colliderIdsByModel.Clear();
        _moveAxesByHandle.Clear();

        AddGrid();

        foreach (var item in _viewModel.Objects)
        {
            item.PropertyChanged -= SceneObject_PropertyChanged;
            item.PropertyChanged += SceneObject_PropertyChanged;

            var entry = CreateRenderEntry(item.Collider);
            _modelsByColliderId[item.Id] = entry;
            _colliderIdsByModel[entry.Element] = item.Id;
            SceneRoot.Children.Add(entry.Element);
        }

        SyncScene();
    }

    private void SyncScene()
    {
        foreach (var item in _viewModel.Objects)
        {
            if (_modelsByColliderId.TryGetValue(item.Id, out var entry))
            {
                UpdateRenderEntry(entry, item == _viewModel.SelectedObject);
            }
        }

        AddMoveGizmo();
        RebuildDebug();
    }

    private RenderEntry CreateRenderEntry(Collider collider)
    {
        var model = collider switch
        {
            SphereCollider sphere => CreateSphere(Vector3.Zero, sphere.Radius, Colors.DeepSkyBlue),
            BoxCollider box => CreateBox(Vector3.Zero, box.HalfExtents, Colors.LightSteelBlue),
            PlaneCollider => CreatePlane(MediaColor.FromRgb(58, 66, 58)),
            _ => new GeometryModel3D()
        };

        model.BackMaterial = model.Material;

        var element = new ModelUIElement3D { Model = model };
        element.MouseDown += Model_MouseDown;
        return new RenderEntry(collider, element, model);
    }

    private static void UpdateRenderEntry(RenderEntry entry, bool selected)
    {
        entry.Model.Material = CreateMaterial(GetColor(entry.Collider, selected));
        entry.Model.BackMaterial = entry.Model.Material;
        entry.Model.Transform = CreateTransform(entry.Collider);
    }

    private static Transform3D CreateTransform(Collider collider)
    {
        if (collider is PlaneCollider)
        {
            return Transform3D.Identity;
        }

        var transform = new Transform3DGroup();
        var position = collider.Body.Position;
        var rotation = collider.Body.Orientation;

        transform.Children.Add(new RotateTransform3D(new QuaternionRotation3D(ToMediaQuaternion(rotation))));
        transform.Children.Add(new TranslateTransform3D(position.X, position.Y, position.Z));
        return transform;
    }

    private static MediaColor GetColor(Collider collider, bool selected)
    {
        if (collider is PlaneCollider)
        {
            return MediaColor.FromRgb(58, 66, 58);
        }

        if (selected)
        {
            return Colors.Gold;
        }

        return collider switch
        {
            SphereCollider => Colors.DeepSkyBlue,
            BoxCollider => Colors.LightSteelBlue,
            _ => Colors.White
        };
    }

    private void AddMoveGizmo()
    {
        for (var i = SceneRoot.Children.Count - 1; i >= 0; i--)
        {
            if (SceneRoot.Children[i] is ModelUIElement3D element && _moveAxesByHandle.ContainsKey(element))
            {
                SceneRoot.Children.RemoveAt(i);
            }
        }

        _moveAxesByHandle.Clear();

        var selected = _viewModel.SelectedObject;
        if (selected is null || selected.Collider is PlaneCollider)
        {
            return;
        }

        var origin = selected.Body.Position;
        AddMoveHandle(origin, Vector3.Normalize(Vector3.Transform(Vector3.UnitX, selected.Body.Orientation)), Colors.IndianRed);
        AddMoveHandle(origin, Vector3.Normalize(Vector3.Transform(Vector3.UnitY, selected.Body.Orientation)), Colors.LightGreen);
        AddMoveHandle(origin, Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, selected.Body.Orientation)), Colors.DodgerBlue);
    }

    private void AddMoveHandle(Vector3 origin, Vector3 axis, MediaColor color)
    {
        const float length = 1.55f;
        var start = origin + axis * 0.45f;
        var end = origin + axis * length;
        var group = new Model3DGroup();
        group.Children.Add(CreateLine(start, end, color, 0.075f));
        group.Children.Add(CreateSphere(end, 0.22f, color));

        var handle = new ModelUIElement3D { Model = group };
        handle.MouseDown += MoveHandle_MouseDown;
        _moveAxesByHandle[handle] = axis;
        SceneRoot.Children.Add(handle);
    }

    private void RebuildDebug()
    {
        DebugRoot.Children.Clear();

        var debugGroup = new Model3DGroup();

        if (_viewModel.ShowVelocityDebug)
        {
            foreach (var item in _viewModel.Objects.Where(item => !item.Body.IsStatic).Take(MaxDebugBodies))
            {
                var start = item.Body.Position;
                var velocity = item.Body.LinearVelocity;
                if (velocity.LengthSquared() > 0.01f)
                {
                    debugGroup.Children.Add(CreateLine(start, start + Vector3.Normalize(velocity) * MathF.Min(velocity.Length() * 0.15f, 1.5f), Colors.Cyan, 0.025f));
                }
            }
        }

        if (_viewModel.ShowContactDebug || _viewModel.ShowNormalDebug)
        {
            foreach (var contact in _viewModel.World.Contacts.Take(MaxDebugContacts))
            {
                if (_viewModel.ShowContactDebug)
                {
                    debugGroup.Children.Add(CreateSphere(contact.Point, 0.07f, Colors.OrangeRed));
                }

                if (_viewModel.ShowNormalDebug)
                {
                    debugGroup.Children.Add(CreateLine(contact.Point, contact.Point + contact.Normal * 0.75f, Colors.LimeGreen, 0.03f));
                }
            }
        }

        if (_viewModel.ShowBoundsDebug)
        {
            foreach (var item in _viewModel.Objects.Where(item => item.Collider is not PlaneCollider).Take(MaxDebugBodies))
            {
                AddBounds(debugGroup, item.Collider.ComputeBounds());
            }
        }

        AddEngineeringVisualization(debugGroup);
        DebugRoot.Children.Add(new ModelVisual3D { Content = debugGroup });
    }

    private void AddEngineeringVisualization(Model3DGroup group)
    {
        var waypoints = _viewModel.Waypoints.ToList();
        for (var i = 0; i < waypoints.Count; i++)
        {
            var waypoint = waypoints[i];
            var color = waypoint.IsReached ? Colors.LightGreen : Colors.Yellow;
            group.Children.Add(CreateSphere(waypoint.Position + new Vector3(0.0f, 0.18f, 0.0f), 0.18f, color));

            if (i > 0)
            {
                group.Children.Add(CreateLine(waypoints[i - 1].Position + new Vector3(0.0f, 0.12f, 0.0f), waypoint.Position + new Vector3(0.0f, 0.12f, 0.0f), Colors.Yellow, 0.025f));
            }
        }

        if (_viewModel.SensorFovDebug && _viewModel.Sensor is { State: not SensorState.Fault and not SensorState.Offline } sensor)
        {
            var origin = sensor.Owner.Position + new Vector3(0.0f, 0.35f, 0.0f);
            var forward = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, sensor.Owner.Orientation));
            var left = RotateAroundY(forward, -sensor.FieldOfViewDegrees * 0.5f);
            var right = RotateAroundY(forward, sensor.FieldOfViewDegrees * 0.5f);
            group.Children.Add(CreateLine(origin, origin + left * sensor.Range, MediaColor.FromRgb(120, 220, 255), 0.02f));
            group.Children.Add(CreateLine(origin, origin + forward * sensor.Range, MediaColor.FromRgb(80, 180, 230), 0.015f));
            group.Children.Add(CreateLine(origin, origin + right * sensor.Range, MediaColor.FromRgb(120, 220, 255), 0.02f));
        }
    }

    private static Vector3 RotateAroundY(Vector3 vector, float degrees)
    {
        return Vector3.Normalize(Vector3.Transform(vector, NumericsQuaternion.CreateFromAxisAngle(Vector3.UnitY, degrees * MathF.PI / 180.0f)));
    }

    private void AddGrid()
    {
        var grid = new Model3DGroup();
        var color = MediaColor.FromRgb(38, 52, 60);

        for (var i = -10; i <= 10; i++)
        {
            grid.Children.Add(CreateLine(new Vector3(i, 0.002f, -10), new Vector3(i, 0.002f, 10), color, 0.008f));
            grid.Children.Add(CreateLine(new Vector3(-10, 0.002f, i), new Vector3(10, 0.002f, i), color, 0.008f));
        }

        SceneRoot.Children.Add(new ModelVisual3D { Content = grid });
    }

    private static GeometryModel3D CreateSphere(Vector3 center, float radius, MediaColor color)
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

    private static GeometryModel3D CreateBox(Vector3 center, Vector3 halfExtents, MediaColor color)
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

    private static GeometryModel3D CreatePlane(MediaColor color)
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

    private static GeometryModel3D CreateLine(Vector3 start, Vector3 end, MediaColor color, float thickness)
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

    private static void AddFace(MeshGeometry3D mesh, int a, int b, int c, int d)
    {
        mesh.TriangleIndices.Add(a);
        mesh.TriangleIndices.Add(b);
        mesh.TriangleIndices.Add(c);
        mesh.TriangleIndices.Add(a);
        mesh.TriangleIndices.Add(c);
        mesh.TriangleIndices.Add(d);
    }

    private static Material CreateMaterial(MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Material material = new DiffuseMaterial(brush);
        material.Freeze();
        return material;
    }

    private static Point3D ToPoint3D(Vector3 vector)
    {
        return new Point3D(vector.X, vector.Y, vector.Z);
    }

    private static System.Windows.Media.Media3D.Quaternion ToMediaQuaternion(NumericsQuaternion quaternion)
    {
        return new System.Windows.Media.Media3D.Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
    }

    private void Model_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ModelUIElement3D model && _colliderIdsByModel.TryGetValue(model, out var colliderId))
        {
            _viewModel.SelectByColliderId(colliderId);
            SyncScene();
            e.Handled = true;
        }
    }

    private void ObjectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncScene();
    }

    private void EntitiesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncScene();
    }

    private void SceneObject_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        SyncScene();
    }

    private void DebugToggle_Click(object sender, RoutedEventArgs e)
    {
        RebuildDebug();
    }

    private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawGraph();
    }

    private void ViewportHost_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isMovingSelection)
        {
            e.Handled = true;
            return;
        }

        ViewportHost.Focus();
        _lastMousePosition = e.GetPosition(ViewportHost);
        _isRightMouseHeld = e.RightButton == MouseButtonState.Pressed || e.ChangedButton == MouseButton.Right;

        if (e.ChangedButton == MouseButton.Left && TryBeginMoveHandle(e.GetPosition(Viewport), _lastMousePosition))
        {
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left && TrySelectObject(e.GetPosition(Viewport), _lastMousePosition, beginDrag: true))
        {
            e.Handled = true;
            return;
        }

        var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        _isOrbiting = e.ChangedButton == MouseButton.Right || (alt && e.ChangedButton == MouseButton.Left);
        _isPanning = e.ChangedButton == MouseButton.Middle || (alt && e.ChangedButton == MouseButton.Middle);
        _isZooming = alt && e.ChangedButton == MouseButton.Right;

        if (_isZooming)
        {
            _isOrbiting = false;
        }

        if (_isOrbiting || _isPanning || _isZooming)
        {
            Mouse.Capture(ViewportHost);
            e.Handled = true;
        }
    }

    private void ViewportHost_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isMovingSelection)
        {
            MoveSelectedObject(e.GetPosition(ViewportHost));
            e.Handled = true;
            return;
        }

        if (!_isOrbiting && !_isPanning && !_isZooming)
        {
            return;
        }

        var position = e.GetPosition(ViewportHost);
        var delta = position - _lastMousePosition;
        _lastMousePosition = position;

        if (_isOrbiting)
        {
            _yaw += delta.X * 0.35;
            _pitch = Math.Clamp(_pitch - delta.Y * 0.35, -80.0, 80.0);
        }
        else if (_isPanning)
        {
            var right = Vector3.Normalize(Vector3.Cross(GetCameraForward(), Vector3.UnitY));
            var up = Vector3.Normalize(Vector3.Cross(right, GetCameraForward()));
            _cameraTarget -= right * (float)(delta.X * 0.02);
            _cameraTarget += up * (float)(delta.Y * 0.02);
        }
        else if (_isZooming)
        {
            _distance = Math.Clamp(_distance * (1.0 + delta.Y * 0.01), 3.0, 40.0);
        }

        UpdateCamera();
        e.Handled = true;
    }

    private void ViewportHost_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        var wasInteracting = _isOrbiting || _isPanning || _isZooming || _isMovingSelection;
        _isOrbiting = false;
        _isPanning = false;
        _isZooming = false;
        _isMovingSelection = false;
        _isMovingOnViewPlane = false;
        _isRightMouseHeld = e.RightButton == MouseButtonState.Pressed && e.ChangedButton != MouseButton.Right;
        Mouse.Capture(null);

        if (wasInteracting)
        {
            e.Handled = true;
        }
    }

    private void ViewportHost_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _distance = Math.Clamp(_distance * (e.Delta > 0 ? 0.9 : 1.1), 3.0, 40.0);
        UpdateCamera();
        e.Handled = true;
    }

    private void UpdateCamera()
    {
        var yawRadians = Math.PI * _yaw / 180.0;
        var pitchRadians = Math.PI * _pitch / 180.0;
        var x = _distance * Math.Cos(pitchRadians) * Math.Sin(yawRadians);
        var y = _distance * Math.Sin(pitchRadians);
        var z = _distance * Math.Cos(pitchRadians) * Math.Cos(yawRadians);
        var position = _cameraTarget + new Vector3((float)x, (float)y, (float)z);

        Camera.Position = ToPoint3D(position);
        Camera.LookDirection = ToVector3D(_cameraTarget - position);
        Camera.UpDirection = new Vector3D(0.0, 1.0, 0.0);
    }

    private Vector3 GetCameraForward()
    {
        var direction = _cameraTarget - new Vector3((float)Camera.Position.X, (float)Camera.Position.Y, (float)Camera.Position.Z);
        return Vector3.Normalize(direction);
    }

    private static Vector3D ToVector3D(Vector3 vector)
    {
        return new Vector3D(vector.X, vector.Y, vector.Z);
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
        group.Children.Add(CreateLine(a, b, color, 0.012f));
    }

    private void MoveHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ModelUIElement3D handle
            || !_moveAxesByHandle.TryGetValue(handle, out var axis)
            || _viewModel.SelectedObject is null
            || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        BeginMoveSelection(axis, e.GetPosition(ViewportHost));
        e.Handled = true;
    }

    private bool TryBeginMoveHandle(Point viewportMousePosition, Point hostMousePosition)
    {
        ModelUIElement3D? hitHandle = null;

        VisualTreeHelper.HitTest(
            Viewport,
            null,
            result =>
            {
                if (result is RayHitTestResult rayHit
                    && rayHit.VisualHit is ModelUIElement3D element
                    && _moveAxesByHandle.ContainsKey(element))
                {
                    hitHandle = element;
                    return HitTestResultBehavior.Stop;
                }

                return HitTestResultBehavior.Continue;
            },
            new PointHitTestParameters(viewportMousePosition));

        if (hitHandle is null || !_moveAxesByHandle.TryGetValue(hitHandle, out var axis))
        {
            return false;
        }

        BeginMoveSelection(axis, hostMousePosition);
        return true;
    }

    private bool TrySelectObject(Point viewportMousePosition, Point hostMousePosition, bool beginDrag)
    {
        ModelUIElement3D? hitModel = null;

        VisualTreeHelper.HitTest(
            Viewport,
            null,
            result =>
            {
                if (result is RayHitTestResult rayHit
                    && rayHit.VisualHit is ModelUIElement3D element
                    && _colliderIdsByModel.ContainsKey(element))
                {
                    hitModel = element;
                    return HitTestResultBehavior.Stop;
                }

                return HitTestResultBehavior.Continue;
            },
            new PointHitTestParameters(viewportMousePosition));

        if (hitModel is null || !_colliderIdsByModel.TryGetValue(hitModel, out var colliderId))
        {
            return false;
        }

        _viewModel.SelectByColliderId(colliderId);
        SyncScene();

        if (beginDrag && _viewModel.SelectedObject?.Collider is not PlaneCollider)
        {
            BeginViewPlaneMove(hostMousePosition);
        }

        return true;
    }

    private void BeginViewPlaneMove(Point mousePosition)
    {
        if (_viewModel.SelectedObject is null)
        {
            return;
        }

        var cameraPosition = new Vector3((float)Camera.Position.X, (float)Camera.Position.Y, (float)Camera.Position.Z);
        var forward = GetCameraForward();
        var right = GetCameraRight();
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var toObject = _viewModel.SelectedObject.Body.Position - cameraPosition;

        _moveStartPosition = _viewModel.SelectedObject.Body.Position;
        _moveStartMousePosition = mousePosition;
        _moveStartCameraRight = right;
        _moveStartCameraUp = up;
        _moveStartDepth = MathF.Max(0.25f, Vector3.Dot(toObject, forward));
        _isMovingSelection = true;
        _isMovingOnViewPlane = true;
        _isOrbiting = false;
        _isPanning = false;
        _isZooming = false;
        Mouse.Capture(ViewportHost);
    }

    private void BeginMoveSelection(Vector3 axis, Point mousePosition)
    {
        if (_viewModel.SelectedObject is null)
        {
            return;
        }

        _moveAxis = Vector3.Normalize(axis);
        _moveStartPosition = _viewModel.SelectedObject.Body.Position;
        _moveStartMousePosition = mousePosition;
        _isMovingSelection = true;
        _isMovingOnViewPlane = false;
        _isOrbiting = false;
        _isPanning = false;
        _isZooming = false;
        Mouse.Capture(ViewportHost);
    }

    private void MoveSelectedObject(Point currentMousePosition)
    {
        if (_viewModel.SelectedObject is null)
        {
            return;
        }

        var mouseDelta = currentMousePosition - _moveStartMousePosition;
        var snapped = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        Vector3 nextPosition;

        if (_isMovingOnViewPlane)
        {
            var worldPerPixel = GetWorldUnitsPerPixel(_moveStartDepth);
            var offset = (_moveStartCameraRight * (float)(mouseDelta.X * worldPerPixel))
                - (_moveStartCameraUp * (float)(mouseDelta.Y * worldPerPixel));
            nextPosition = _moveStartPosition + offset;
        }
        else
        {
            var axisScreenDirection = GetScreenAxisDirection(_moveStartPosition, _moveAxis);
            var signedPixels = (mouseDelta.X * axisScreenDirection.X) + (mouseDelta.Y * axisScreenDirection.Y);
            var worldScale = Math.Max(0.015, _distance * 0.0018);
            var amount = (float)(signedPixels * worldScale);
            nextPosition = _moveStartPosition + _moveAxis * amount;
        }

        if (snapped)
        {
            nextPosition = new Vector3(
                MathF.Round(nextPosition.X / 0.25f) * 0.25f,
                MathF.Round(nextPosition.Y / 0.25f) * 0.25f,
                MathF.Round(nextPosition.Z / 0.25f) * 0.25f);
        }

        _viewModel.SelectedObject.Body.Position = nextPosition;
        _viewModel.SelectedObject.Body.LinearVelocity = Vector3.Zero;
        _viewModel.SelectedObject.Body.AngularVelocity = Vector3.Zero;
        _viewModel.SelectedObject.Refresh();
        SyncScene();
    }

    private Vector GetScreenAxisDirection(Vector3 origin, Vector3 axis)
    {
        var start = ProjectToViewport(origin);
        var end = ProjectToViewport(origin + axis);
        var direction = end - start;

        if (direction.LengthSquared < 0.0001)
        {
            return new Vector(1.0, 0.0);
        }

        direction.Normalize();
        return direction;
    }

    private Point ProjectToViewport(Vector3 world)
    {
        var cameraPosition = new Vector3((float)Camera.Position.X, (float)Camera.Position.Y, (float)Camera.Position.Z);
        var forward = Vector3.Normalize(_cameraTarget - cameraPosition);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var toPoint = world - cameraPosition;
        var depth = MathF.Max(0.001f, Vector3.Dot(toPoint, forward));
        var horizontal = Vector3.Dot(toPoint, right);
        var vertical = Vector3.Dot(toPoint, up);
        var fov = Math.PI * Camera.FieldOfView / 180.0;
        var scale = Math.Tan(fov * 0.5) * depth;
        var aspect = Math.Max(0.001, ViewportHost.ActualWidth / Math.Max(1.0, ViewportHost.ActualHeight));
        var x = (horizontal / (scale * aspect) * 0.5 + 0.5) * ViewportHost.ActualWidth;
        var y = (0.5 - vertical / scale * 0.5) * ViewportHost.ActualHeight;
        return new Point(x, y);
    }

    private double GetWorldUnitsPerPixel(float depth)
    {
        var fov = Math.PI * Camera.FieldOfView / 180.0;
        var visibleHeight = 2.0 * Math.Tan(fov * 0.5) * depth;
        return visibleHeight / Math.Max(1.0, ViewportHost.ActualHeight);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isRightMouseHeld)
        {
            return;
        }

        if (e.Key == Key.Q)
        {
            MoveCameraVertical(0.35f);
            e.Handled = true;
        }
        else if (e.Key == Key.E)
        {
            MoveCameraVertical(-0.35f);
            e.Handled = true;
        }
        else if (e.Key == Key.W)
        {
            MoveCameraPlanar(GetCameraForward(), 0.35f);
            e.Handled = true;
        }
        else if (e.Key == Key.S)
        {
            MoveCameraPlanar(GetCameraForward(), -0.35f);
            e.Handled = true;
        }
        else if (e.Key == Key.A)
        {
            MoveCameraPlanar(GetCameraRight(), -0.35f);
            e.Handled = true;
        }
        else if (e.Key == Key.D)
        {
            MoveCameraPlanar(GetCameraRight(), 0.35f);
            e.Handled = true;
        }
    }

    private void MoveCameraVertical(float amount)
    {
        _cameraTarget += Vector3.UnitY * amount;
        UpdateCamera();
    }

    private void MoveCameraPlanar(Vector3 direction, float amount)
    {
        direction.Y = 0.0f;

        if (direction.LengthSquared() <= 0.000001f)
        {
            return;
        }

        _cameraTarget += Vector3.Normalize(direction) * amount;
        UpdateCamera();
    }

    private Vector3 GetCameraRight()
    {
        return Vector3.Normalize(Vector3.Cross(GetCameraForward(), Vector3.UnitY));
    }

    private void DrawGraph()
    {
        GraphCanvas.Children.Clear();

        var samples = _viewModel.GraphSamples.ToList();
        if (samples.Count < 2 || GraphCanvas.ActualWidth <= 1.0 || GraphCanvas.ActualHeight <= 1.0)
        {
            return;
        }

        DrawSeries(samples.Select(sample => (double)sample.PositionY).ToList(), Colors.DeepSkyBlue);
        DrawSeries(samples.Select(sample => (double)sample.Speed).ToList(), Colors.LightGreen);
        DrawSeries(samples.Select(sample => (double)sample.KineticEnergy).ToList(), Colors.Orange);
        AddGraphLabel("Y", Colors.DeepSkyBlue, 8);
        AddGraphLabel("Speed", Colors.LightGreen, 42);
        AddGraphLabel("Energy", Colors.Orange, 92);
    }

    private void DrawSeries(IReadOnlyList<double> values, MediaColor color)
    {
        var min = values.Min();
        var max = values.Max();
        var range = Math.Max(0.0001, max - min);
        var width = GraphCanvas.ActualWidth;
        var height = GraphCanvas.ActualHeight;
        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.5
        };

        for (var i = 0; i < values.Count; i++)
        {
            var x = i / Math.Max(1.0, values.Count - 1.0) * width;
            var y = height - ((values[i] - min) / range * (height - 10.0)) - 5.0;
            polyline.Points.Add(new Point(x, y));
        }

        GraphCanvas.Children.Add(polyline);
    }

    private void AddGraphLabel(string text, MediaColor color, double left)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(color),
            FontSize = 11
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, 4);
        GraphCanvas.Children.Add(label);
    }

    private sealed record RenderEntry(Collider Collider, ModelUIElement3D Element, GeometryModel3D Model);
}
