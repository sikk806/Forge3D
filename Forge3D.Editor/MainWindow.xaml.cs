using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Forge3D.Editor.Input;
using Forge3D.Editor.Rendering;
using Forge3D.Editor.ViewModels;
using Microsoft.Win32;
using Vector3 = System.Numerics.Vector3;

namespace Forge3D.Editor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TelemetryGraphRenderer _graphRenderer = new();
    private readonly DebugRenderer _debugRenderer = new();
    private readonly SceneRenderer _sceneRenderer = new();
    private readonly RenderLoop _renderLoop = new();
    private readonly CameraInputState _cameraInput = new();
    private readonly CameraController _cameraController;
    private readonly Dictionary<int, SceneVisual> _modelsByColliderId = [];
    private readonly Dictionary<ModelUIElement3D, int> _colliderIdsByModel = [];
    private readonly Dictionary<ModelUIElement3D, Vector3> _moveAxesByHandle = [];
    private readonly Dictionary<string, ModelUIElement3D> _waypointHandlesById = [];
    private readonly Dictionary<ModelUIElement3D, string> _waypointIdsByHandle = [];
    private ModelVisual3D? _moveGizmoRoot;
    private int? _moveGizmoColliderId;
    private readonly QuaternionRotation3D _moveGizmoRotation = new();
    private readonly TranslateTransform3D _moveGizmoTranslation = new();
    private Point _lastMousePosition;
    private bool _isOrbiting;
    private bool _isPanning;
    private bool _isZooming;
    private bool _isRightMouseHeld;
    private bool _isMovingSelection;
    private bool _isMovingOnViewPlane;
    private bool _isMovingWaypoint;
    private string? _movingWaypointId;
    private Vector3 _moveAxis;
    private Vector3 _moveStartPosition;
    private Vector3 _moveStartCameraRight;
    private Vector3 _moveStartCameraUp;
    private float _moveStartDepth;
    private Point _moveStartMousePosition;
    private double _debugRefreshAccumulator;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        _cameraController = new CameraController(Camera);
        DataContext = _viewModel;

        _viewModel.SceneChanged += (_, _) => RebuildScene();
        _viewModel.SimulationAdvanced += (_, _) =>
        {
            DrawGraph();
        };
        _renderLoop.Rendering += RenderLoop_Rendering;
        Closed += (_, _) => _renderLoop.Dispose();
        RebuildScene();
        DrawGraph();
        _renderLoop.Start();
    }

    private void RenderLoop_Rendering(object? sender, RenderFrameEventArgs e)
    {
        _cameraController.Update(e.DeltaTime, _cameraInput, Keyboard.Modifiers);
        SyncScene();
        UpdateDebugVisuals(e.DeltaTime);
    }

    private void RebuildScene()
    {
        SceneRoot.Children.Clear();
        DebugRoot.Children.Clear();
        _modelsByColliderId.Clear();
        _colliderIdsByModel.Clear();
        _moveAxesByHandle.Clear();
        _waypointHandlesById.Clear();
        _waypointIdsByHandle.Clear();
        _moveGizmoRoot = null;
        _moveGizmoColliderId = null;

        SceneRoot.Children.Add(_sceneRenderer.CreateGrid());

        foreach (var item in _viewModel.RenderEntities)
        {
            var entry = _sceneRenderer.CreateVisual(item);
            entry.Element.MouseDown += Model_MouseDown;
            _modelsByColliderId[item.Id] = entry;
            _colliderIdsByModel[entry.Element] = item.Id;
            SceneRoot.Children.Add(entry.Element);
        }

        AddWaypointHandles();
        SyncScene();
    }

    private void SyncScene()
    {
        var interpolationAlpha = _viewModel.RenderInterpolationAlpha;
        foreach (var item in _viewModel.RenderEntities)
        {
            if (_modelsByColliderId.TryGetValue(item.Id, out var entry))
            {
                _sceneRenderer.UpdateVisual(entry, item, item.Id == _viewModel.SelectedObject?.Id, interpolationAlpha);
            }
        }

        UpdateMoveGizmo(interpolationAlpha);
        SyncWaypointHandles();
    }

    private void AddWaypointHandles()
    {
        foreach (var waypoint in _viewModel.Waypoints)
        {
            var model = MeshFactory.CreateSphere(Vector3.Zero, 0.24f, waypoint.IsReached ? Colors.LightGreen : Colors.Yellow);
            var transform = new TranslateTransform3D();
            model.Transform = transform;
            var element = new ModelUIElement3D { Model = model };
            element.MouseDown += Waypoint_MouseDown;
            _waypointHandlesById[waypoint.Id] = element;
            _waypointIdsByHandle[element] = waypoint.Id;
            SceneRoot.Children.Add(element);
        }
    }

    private void SyncWaypointHandles()
    {
        var waypointIds = _viewModel.Waypoints.Select(item => item.Id).ToHashSet();
        if (!_waypointHandlesById.Keys.ToHashSet().SetEquals(waypointIds))
        {
            RebuildScene();
            return;
        }

        foreach (var waypoint in _viewModel.Waypoints)
        {
            if (!_waypointHandlesById.TryGetValue(waypoint.Id, out var element)
                || element.Model is not GeometryModel3D model)
            {
                continue;
            }

            model.Transform = new TranslateTransform3D(
                waypoint.Position.X,
                waypoint.Position.Y + 0.22f,
                waypoint.Position.Z);
            model.Material = MeshFactory.CreateMaterial(waypoint.IsReached ? Colors.LightGreen : Colors.Yellow);
            model.BackMaterial = model.Material;
        }
    }

    private void AddMoveGizmo()
    {
        if (_moveGizmoRoot is not null)
        {
            SceneRoot.Children.Remove(_moveGizmoRoot);
            _moveGizmoRoot = null;
        }

        _moveAxesByHandle.Clear();
        _moveGizmoColliderId = null;

        var selected = _viewModel.SelectedObject;
        if (selected is null || _viewModel.GetEntityState(selected.Id)?.ColliderType == "Plane")
        {
            return;
        }

        var transform = new Transform3DGroup();
        transform.Children.Add(new RotateTransform3D(_moveGizmoRotation));
        transform.Children.Add(_moveGizmoTranslation);

        _moveGizmoRoot = new ModelVisual3D { Transform = transform };
        _moveGizmoColliderId = selected.Id;
        AddMoveHandle(_moveGizmoRoot, Vector3.UnitX, Colors.IndianRed);
        AddMoveHandle(_moveGizmoRoot, Vector3.UnitY, Colors.LightGreen);
        AddMoveHandle(_moveGizmoRoot, Vector3.UnitZ, Colors.DodgerBlue);
        SceneRoot.Children.Add(_moveGizmoRoot);
    }

    private void AddMoveHandle(ModelVisual3D root, Vector3 axis, Color color)
    {
        const float length = 1.55f;
        var start = axis * 0.45f;
        var end = axis * length;
        var handleModel = new Model3DGroup();
        handleModel.Children.Add(MeshFactory.CreateLine(start, end, color, 0.075f));
        handleModel.Children.Add(MeshFactory.CreateSphere(end, 0.22f, color));

        var handle = new ModelUIElement3D { Model = handleModel };
        handle.MouseDown += MoveHandle_MouseDown;
        _moveAxesByHandle[handle] = axis;
        root.Children.Add(handle);
    }

    private void UpdateMoveGizmo(float interpolationAlpha)
    {
        var selected = _viewModel.SelectedObject;
        var selectedState = selected is null ? null : _viewModel.GetEntityState(selected.Id);
        if (selectedState is null || selectedState.ColliderType == "Plane")
        {
            if (_moveGizmoRoot is not null)
            {
                AddMoveGizmo();
            }

            return;
        }

        if (_moveGizmoRoot is null || _moveGizmoColliderId != selectedState.Id)
        {
            AddMoveGizmo();
        }

        var position = Vector3.Lerp(selectedState.PreviousPosition, selectedState.Position, interpolationAlpha);
        var orientation = System.Numerics.Quaternion.Slerp(selectedState.PreviousOrientation, selectedState.Orientation, interpolationAlpha);
        _moveGizmoRotation.Quaternion = new System.Windows.Media.Media3D.Quaternion(orientation.X, orientation.Y, orientation.Z, orientation.W);
        _moveGizmoTranslation.OffsetX = position.X;
        _moveGizmoTranslation.OffsetY = position.Y;
        _moveGizmoTranslation.OffsetZ = position.Z;
    }

    private void RebuildDebug()
    {
        DebugRoot.Children.Clear();
        DebugRoot.Children.Add(new ModelVisual3D { Content = _debugRenderer.Build(_viewModel) });
    }

    private void UpdateDebugVisuals(TimeSpan deltaTime)
    {
        if (!_viewModel.ShowVelocityDebug
            && !_viewModel.ShowContactDebug
            && !_viewModel.ShowNormalDebug
            && !_viewModel.ShowBoundsDebug
            && !_viewModel.SensorFovDebug
            && !_viewModel.ShowNavigationPathDebug)
        {
            return;
        }

        _debugRefreshAccumulator += deltaTime.TotalSeconds;
        if (_debugRefreshAccumulator < 0.05)
        {
            return;
        }

        _debugRefreshAccumulator = 0.0;
        RebuildDebug();
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

    private void ObjectsList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is { } item
            && item.DataContext is SceneObjectViewModel sceneObject)
        {
            item.IsSelected = true;
            _viewModel.SelectByColliderId(sceneObject.Id);
            ShowInspectorContextMenu(sceneObject.Id, null, null, sender as FrameworkElement);
            e.Handled = true;
        }
    }

    private void EntitiesList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is { } item
            && item.DataContext is EntityViewModel entity)
        {
            item.IsSelected = true;
            ShowInspectorContextMenu(null, null, entity.Id, sender as FrameworkElement);
            e.Handled = true;
        }
    }

    private void InspectorHost_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        InspectorHost.Focus();
        ShowInspectorContextMenu(_viewModel.SelectedObject?.Id, _viewModel.SelectedWaypointState?.Id, _viewModel.SelectedEntity?.Id, InspectorHost);
        e.Handled = true;
    }

    private void InspectorHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsTextInputFocused())
        {
            InspectorHost.Focus();
        }
    }

    private void ShowInspectorContextMenu(int? targetObjectId, string? targetWaypointId, string? targetRuntimeEntityId, FrameworkElement? placementTarget)
    {
        var deleteItem = new MenuItem
        {
            Header = _viewModel.MenuDelete,
            IsEnabled = CanDeleteContextTarget(targetObjectId, targetWaypointId, targetRuntimeEntityId)
        };
        deleteItem.Click += (_, _) => DeleteContextTarget(targetObjectId, targetWaypointId, targetRuntimeEntityId);

        var copyItem = new MenuItem
        {
            Header = _viewModel.MenuCopy,
            IsEnabled = CanCopyContextTarget(targetObjectId, targetWaypointId, targetRuntimeEntityId)
        };
        copyItem.Click += (_, _) => CopyContextTarget(targetObjectId, targetWaypointId, targetRuntimeEntityId);

        var pasteItem = new MenuItem
        {
            Header = _viewModel.MenuPaste,
            IsEnabled = _viewModel.HasClipboard
        };
        pasteItem.Click += (_, _) => _viewModel.PasteClipboardOffset();

        var menu = new ContextMenu
        {
            PlacementTarget = placementTarget ?? InspectorHost,
            Placement = PlacementMode.MousePoint
        };
        menu.Items.Add(deleteItem);
        menu.Items.Add(copyItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(pasteItem);
        menu.IsOpen = true;
    }

    private bool CanDeleteContextTarget(int? objectId, string? waypointId, string? runtimeEntityId)
    {
        if (waypointId is not null)
        {
            return _viewModel.CanDeleteWaypoint(waypointId);
        }

        if (runtimeEntityId is not null)
        {
            return _viewModel.CanDeleteRuntimeEntity(runtimeEntityId);
        }

        return objectId is { } id && _viewModel.CanDeleteObject(id);
    }

    private bool CanCopyContextTarget(int? objectId, string? waypointId, string? runtimeEntityId)
    {
        if (waypointId is not null)
        {
            return _viewModel.CanCopyWaypoint(waypointId);
        }

        if (runtimeEntityId is not null)
        {
            return _viewModel.CanCopyRuntimeEntity(runtimeEntityId);
        }

        return objectId is { } id && _viewModel.CanCopyObject(id);
    }

    private void DeleteContextTarget(int? objectId, string? waypointId, string? runtimeEntityId)
    {
        if (waypointId is not null)
        {
            _viewModel.DeleteWaypoint(waypointId);
        }
        else if (runtimeEntityId is not null)
        {
            _viewModel.DeleteRuntimeEntity(runtimeEntityId);
        }
        else if (objectId is { } id)
        {
            _viewModel.DeleteObject(id);
        }
    }

    private void CopyContextTarget(int? objectId, string? waypointId, string? runtimeEntityId)
    {
        if (waypointId is not null)
        {
            _viewModel.CopyWaypoint(waypointId);
        }
        else if (runtimeEntityId is not null)
        {
            _viewModel.CopyRuntimeEntity(runtimeEntityId);
        }
        else if (objectId is { } id)
        {
            _viewModel.CopyObject(id);
        }
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T target)
            {
                return target;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void ViewportHost_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isMovingSelection)
        {
            e.Handled = true;
            return;
        }

        if (_isMovingWaypoint)
        {
            e.Handled = true;
            return;
        }

        ViewportHost.Focus();
        _lastMousePosition = e.GetPosition(ViewportHost);

        _isRightMouseHeld = e.RightButton == MouseButtonState.Pressed || e.ChangedButton == MouseButton.Right;
        _cameraInput.IsEnabled = _isRightMouseHeld;

        if (e.ChangedButton == MouseButton.Left && TryBeginMoveHandle(e.GetPosition(Viewport), _lastMousePosition))
        {
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left && TryBeginWaypointMove(e.GetPosition(Viewport), _lastMousePosition))
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

        if (_isMovingWaypoint)
        {
            MoveWaypoint(e.GetPosition(ViewportHost));
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
        var wasInteracting = _isOrbiting || _isPanning || _isZooming || _isMovingSelection || _isMovingWaypoint;
        _isOrbiting = false;
        _isPanning = false;
        _isZooming = false;
        _isMovingSelection = false;
        _isMovingOnViewPlane = false;
        _isMovingWaypoint = false;
        _movingWaypointId = null;
        _isRightMouseHeld = e.RightButton == MouseButtonState.Pressed && e.ChangedButton != MouseButton.Right;
        if (!_isRightMouseHeld)
        {
            _cameraInput.Clear();
        }
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

        BeginMoveSelection(GetWorldMoveAxis(axis), e.GetPosition(ViewportHost));
        e.Handled = true;
    }

    private void Waypoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ModelUIElement3D handle
            && _waypointIdsByHandle.TryGetValue(handle, out var waypointId)
            && e.ChangedButton == MouseButton.Left)
        {
            BeginWaypointMove(waypointId, e.GetPosition(ViewportHost));
            e.Handled = true;
        }
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

        BeginMoveSelection(GetWorldMoveAxis(axis), hostMousePosition);
        return true;
    }

    private bool TryBeginWaypointMove(Point viewportMousePosition, Point hostMousePosition)
    {
        if (!TryHitWaypoint(viewportMousePosition, out var waypointId))
        {
            return false;
        }

        BeginWaypointMove(waypointId, hostMousePosition);
        return true;
    }

    private void BeginWaypointMove(string waypointId, Point mousePosition)
    {
        _viewModel.SelectWaypoint(waypointId);
        _movingWaypointId = waypointId;
        _moveStartMousePosition = mousePosition;
        _isMovingWaypoint = true;
        _isMovingSelection = false;
        _isMovingOnViewPlane = false;
        _isOrbiting = false;
        _isPanning = false;
        _isZooming = false;
        Mouse.Capture(ViewportHost);
    }

    private Vector3 GetWorldMoveAxis(Vector3 localAxis)
    {
        var selectedState = _viewModel.SelectedObjectState;
        return selectedState is null
            ? localAxis
            : Vector3.Normalize(Vector3.Transform(localAxis, selectedState.Orientation));
    }

    private bool TrySelectObject(Point viewportMousePosition, Point hostMousePosition, bool beginDrag)
    {
        if (!TryHitObject(viewportMousePosition, out var colliderId))
        {
            return false;
        }

        _viewModel.SelectByColliderId(colliderId);
        SyncScene();

        if (beginDrag && _viewModel.SelectedObjectState?.ColliderType != "Plane")
        {
            BeginViewPlaneMove(hostMousePosition);
        }

        return true;
    }

    private bool TryHitWaypoint(Point viewportMousePosition, out string waypointId)
    {
        ModelUIElement3D? hitHandle = null;

        VisualTreeHelper.HitTest(
            Viewport,
            null,
            result =>
            {
                if (result is RayHitTestResult rayHit
                    && rayHit.VisualHit is ModelUIElement3D element
                    && _waypointIdsByHandle.ContainsKey(element))
                {
                    hitHandle = element;
                    return HitTestResultBehavior.Stop;
                }

                return HitTestResultBehavior.Continue;
            },
            new PointHitTestParameters(viewportMousePosition));

        if (hitHandle is not null && _waypointIdsByHandle.TryGetValue(hitHandle, out waypointId!))
        {
            return true;
        }

        waypointId = string.Empty;
        return false;
    }

    private bool TryHitObject(Point viewportMousePosition, out int colliderId)
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

        if (hitModel is not null && _colliderIdsByModel.TryGetValue(hitModel, out colliderId))
        {
            return true;
        }

        colliderId = 0;
        return false;
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
        var selectedState = _viewModel.SelectedObjectState;
        if (selectedState is null)
        {
            return;
        }

        var toObject = selectedState.Position - cameraPosition;

        _moveStartPosition = selectedState.Position;
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
        var selectedState = _viewModel.SelectedObjectState;
        if (selectedState is null)
        {
            return;
        }

        _moveStartPosition = selectedState.Position;
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

        _viewModel.MoveSelectedObjectTo(nextPosition);
        SyncScene();
    }

    private void MoveWaypoint(Point currentMousePosition)
    {
        if (_movingWaypointId is null || !TryProjectToGround(currentMousePosition, 0.05f, out var nextPosition))
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            nextPosition = new Vector3(
                MathF.Round(nextPosition.X / 0.25f) * 0.25f,
                0.05f,
                MathF.Round(nextPosition.Z / 0.25f) * 0.25f);
        }

        _viewModel.MoveWaypointTo(_movingWaypointId, nextPosition);
        SyncWaypointHandles();
    }

    private bool TryProjectToGround(Point mousePosition, float groundY, out Vector3 position)
    {
        var width = Math.Max(1.0, ViewportHost.ActualWidth);
        var height = Math.Max(1.0, ViewportHost.ActualHeight);
        var ndcX = (float)((mousePosition.X / width * 2.0) - 1.0);
        var ndcY = (float)(1.0 - (mousePosition.Y / height * 2.0));
        var fov = Math.PI * Camera.FieldOfView / 180.0;
        var scale = (float)Math.Tan(fov * 0.5);
        var aspect = (float)(width / height);
        var ray = Vector3.Normalize(
            _cameraController.Forward
            + (_cameraController.Right * ndcX * scale * aspect)
            + (_cameraController.Up * ndcY * scale));

        var origin = _cameraController.CameraPosition;
        if (MathF.Abs(ray.Y) < 0.0001f)
        {
            position = default;
            return false;
        }

        var t = (groundY - origin.Y) / ray.Y;
        if (t < 0.0f)
        {
            position = default;
            return false;
        }

        var hit = origin + (ray * t);
        position = new Vector3(hit.X, groundY, hit.Z);
        return true;
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
        if (IsTextInputFocused())
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.C)
        {
            _viewModel.CopySelection();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.V)
        {
            if (InspectorHost.IsKeyboardFocusWithin)
            {
                _viewModel.PasteClipboardOffset();
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Delete)
        {
            _viewModel.DeleteSelection();
            e.Handled = true;
            return;
        }

        if (!_isRightMouseHeld)
        {
            return;
        }

        _cameraInput.IsEnabled = true;
        if (_cameraInput.SetKey(e.Key, true))
        {
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (_cameraInput.SetKey(e.Key, false))
        {
            e.Handled = true;
        }
    }

    private static bool IsTextInputFocused()
    {
        return Keyboard.FocusedElement is TextBoxBase or ComboBox;
    }

    private void DrawGraph()
    {
        _graphRenderer.Draw(GraphCanvas, _viewModel.GraphSamples.ToList());
    }
}
