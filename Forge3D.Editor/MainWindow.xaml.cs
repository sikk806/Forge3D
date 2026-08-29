using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Forge3D.Core.Collision;
using Forge3D.Editor.Input;
using Forge3D.Editor.Rendering;
using Forge3D.Editor.ViewModels;
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;
using NumericsQuaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace Forge3D.Editor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TelemetryGraphRenderer _graphRenderer = new();
    private readonly DebugRenderer _debugRenderer = new();
    private readonly CameraController _cameraController;
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

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        _cameraController = new CameraController(Camera);
        DataContext = _viewModel;

        _viewModel.SceneChanged += (_, _) => RebuildScene();
        _viewModel.SimulationAdvanced += (_, _) =>
        {
            SyncScene();
            DrawGraph();
        };
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
            SphereCollider sphere => MeshFactory.CreateSphere(Vector3.Zero, sphere.Radius, Colors.DeepSkyBlue),
            BoxCollider box => MeshFactory.CreateBox(Vector3.Zero, box.HalfExtents, Colors.LightSteelBlue),
            PlaneCollider => MeshFactory.CreatePlane(MediaColor.FromRgb(58, 66, 58)),
            _ => new GeometryModel3D()
        };

        model.BackMaterial = model.Material;

        var element = new ModelUIElement3D { Model = model };
        element.MouseDown += Model_MouseDown;
        return new RenderEntry(collider, element, model);
    }

    private static void UpdateRenderEntry(RenderEntry entry, bool selected)
    {
        entry.Model.Material = MeshFactory.CreateMaterial(GetColor(entry.Collider, selected));
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
        group.Children.Add(MeshFactory.CreateLine(start, end, color, 0.075f));
        group.Children.Add(MeshFactory.CreateSphere(end, 0.22f, color));

        var handle = new ModelUIElement3D { Model = group };
        handle.MouseDown += MoveHandle_MouseDown;
        _moveAxesByHandle[handle] = axis;
        SceneRoot.Children.Add(handle);
    }

    private void RebuildDebug()
    {
        DebugRoot.Children.Clear();
        DebugRoot.Children.Add(new ModelVisual3D { Content = _debugRenderer.Build(_viewModel) });
    }

    private void AddGrid()
    {
        var grid = new Model3DGroup();
        var color = MediaColor.FromRgb(38, 52, 60);

        for (var i = -10; i <= 10; i++)
        {
            grid.Children.Add(MeshFactory.CreateLine(new Vector3(i, 0.002f, -10), new Vector3(i, 0.002f, 10), color, 0.008f));
            grid.Children.Add(MeshFactory.CreateLine(new Vector3(-10, 0.002f, i), new Vector3(10, 0.002f, i), color, 0.008f));
        }

        SceneRoot.Children.Add(new ModelVisual3D { Content = grid });
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

    private void SettingsMenu_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow
        {
            Owner = this,
            DataContext = _viewModel
        };
        window.ShowDialog();
    }

    private void ImportDataMenu_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Data files (*.csv;*.json)|*.csv;*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.DataImportPath = dialog.FileName;
            if (_viewModel.ImportDataCommand.CanExecute(null))
            {
                _viewModel.ImportDataCommand.Execute(null);
            }
        }
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
            _cameraController.Orbit(delta);
        }
        else if (_isPanning)
        {
            _cameraController.Pan(delta);
        }
        else if (_isZooming)
        {
            _cameraController.ZoomByDrag(delta);
        }
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
        _cameraController.ZoomByWheel(e.Delta);
        e.Handled = true;
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

        var cameraPosition = _cameraController.CameraPosition;
        var forward = _cameraController.Forward;
        var right = _cameraController.Right;
        var up = _cameraController.Up;
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
        var worldScale = Math.Max(0.015, _cameraController.Distance * 0.0018);
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
        return _cameraController.ProjectToViewport(world, ViewportHost.ActualWidth, ViewportHost.ActualHeight);
    }

    private double GetWorldUnitsPerPixel(float depth)
    {
        return _cameraController.GetWorldUnitsPerPixel(depth, ViewportHost.ActualHeight);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isRightMouseHeld)
        {
            return;
        }

        if (e.Key == Key.Q)
        {
            _cameraController.MoveVertical(0.35f);
            e.Handled = true;
        }
        else if (e.Key == Key.E)
        {
            _cameraController.MoveVertical(-0.35f);
            e.Handled = true;
        }
        else if (e.Key == Key.W)
        {
            _cameraController.MovePlanar(_cameraController.Forward, 0.35f);
            e.Handled = true;
        }
        else if (e.Key == Key.S)
        {
            _cameraController.MovePlanar(_cameraController.Forward, -0.35f);
            e.Handled = true;
        }
        else if (e.Key == Key.A)
        {
            _cameraController.MovePlanar(_cameraController.Right, -0.35f);
            e.Handled = true;
        }
        else if (e.Key == Key.D)
        {
            _cameraController.MovePlanar(_cameraController.Right, 0.35f);
            e.Handled = true;
        }
    }

    private void DrawGraph()
    {
        _graphRenderer.Draw(GraphCanvas, _viewModel.GraphSamples.ToList());
    }

    private sealed record RenderEntry(Collider Collider, ModelUIElement3D Element, GeometryModel3D Model);
}
