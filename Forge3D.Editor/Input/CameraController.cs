using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using Vector3 = System.Numerics.Vector3;

namespace Forge3D.Editor.Input;

public sealed class CameraController
{
    private readonly PerspectiveCamera _camera;
    private double _targetYaw = -30.0;
    private double _targetPitch = 28.0;
    private double _targetDistance = 13.0;
    private Vector3 _targetFocus = new(0.0f, 1.5f, 0.0f);
    private double _currentYaw = -30.0;
    private double _currentPitch = 28.0;
    private double _currentDistance = 13.0;
    private Vector3 _currentFocus = new(0.0f, 1.5f, 0.0f);

    public CameraController(PerspectiveCamera camera)
    {
        _camera = camera;
        ApplyCamera();
    }

    public double OrbitSensitivity { get; set; } = 0.35;

    public double RotationSmoothSpeed { get; set; } = 18.0;

    public double PanSmoothSpeed { get; set; } = 16.0;

    public double ZoomSmoothSpeed { get; set; } = 20.0;

    public float MoveSpeed { get; set; } = 7.5f;

    public double Distance => _currentDistance;

    public Vector3 CameraPosition => new((float)_camera.Position.X, (float)_camera.Position.Y, (float)_camera.Position.Z);

    public Vector3 Forward => Vector3.Normalize(_currentFocus - CameraPosition);

    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));

    public Vector3 Up => Vector3.Normalize(Vector3.Cross(Right, Forward));

    public void Orbit(Vector delta)
    {
        _targetYaw += delta.X * OrbitSensitivity;
        _targetPitch = Math.Clamp(_targetPitch - delta.Y * OrbitSensitivity, -80.0, 80.0);
    }

    public void Pan(Vector delta)
    {
        _targetFocus -= Right * (float)(delta.X * 0.02);
        _targetFocus += Up * (float)(delta.Y * 0.02);
    }

    public void ZoomByDrag(Vector delta)
    {
        _targetDistance = Math.Clamp(_targetDistance * (1.0 + delta.Y * 0.01), 3.0, 40.0);
    }

    public void ZoomByWheel(int delta)
    {
        _targetDistance = Math.Clamp(_targetDistance * (delta > 0 ? 0.9 : 1.1), 3.0, 40.0);
    }

    public void MoveVertical(float amount)
    {
        _targetFocus += Vector3.UnitY * amount;
    }

    public void MovePlanar(Vector3 direction, float amount)
    {
        direction.Y = 0.0f;

        if (direction.LengthSquared() <= 0.000001f)
        {
            return;
        }

        _targetFocus += Vector3.Normalize(direction) * amount;
    }

    public void Update(TimeSpan deltaTime, CameraInputState input, ModifierKeys modifiers)
    {
        if (input.IsEnabled && input.HasMovement)
        {
            var speedScale = modifiers.HasFlag(ModifierKeys.Shift)
                ? 3.0f
                : modifiers.HasFlag(ModifierKeys.Control) ? 0.25f : 1.0f;
            var amount = MoveSpeed * speedScale * (float)Math.Min(deltaTime.TotalSeconds, 0.05);

            if (input.Forward)
            {
                MovePlanar(Forward, amount);
            }

            if (input.Backward)
            {
                MovePlanar(Forward, -amount);
            }

            if (input.Left)
            {
                MovePlanar(Right, -amount);
            }

            if (input.Right)
            {
                MovePlanar(Right, amount);
            }

            if (input.Up)
            {
                MoveVertical(amount);
            }

            if (input.Down)
            {
                MoveVertical(-amount);
            }
        }

        var rotationT = SmoothFactor(RotationSmoothSpeed, deltaTime);
        var panT = SmoothFactor(PanSmoothSpeed, deltaTime);
        var zoomT = SmoothFactor(ZoomSmoothSpeed, deltaTime);

        _currentYaw = Lerp(_currentYaw, _targetYaw, rotationT);
        _currentPitch = Lerp(_currentPitch, _targetPitch, rotationT);
        _currentDistance = Lerp(_currentDistance, _targetDistance, zoomT);
        _currentFocus = Vector3.Lerp(_currentFocus, _targetFocus, (float)panT);
        ApplyCamera();
    }

    public void Snap()
    {
        _currentYaw = _targetYaw;
        _currentPitch = _targetPitch;
        _currentDistance = _targetDistance;
        _currentFocus = _targetFocus;
        ApplyCamera();
    }

    public Point ProjectToViewport(Vector3 world, double width, double height)
    {
        var toPoint = world - CameraPosition;
        var depth = MathF.Max(0.001f, Vector3.Dot(toPoint, Forward));
        var horizontal = Vector3.Dot(toPoint, Right);
        var vertical = Vector3.Dot(toPoint, Up);
        var fov = Math.PI * _camera.FieldOfView / 180.0;
        var scale = Math.Tan(fov * 0.5) * depth;
        var aspect = Math.Max(0.001, width / Math.Max(1.0, height));
        var x = (horizontal / (scale * aspect) * 0.5 + 0.5) * width;
        var y = (0.5 - vertical / scale * 0.5) * height;
        return new Point(x, y);
    }

    public double GetWorldUnitsPerPixel(float depth, double viewportHeight)
    {
        var fov = Math.PI * _camera.FieldOfView / 180.0;
        var visibleHeight = 2.0 * Math.Tan(fov * 0.5) * depth;
        return visibleHeight / Math.Max(1.0, viewportHeight);
    }

    private void ApplyCamera()
    {
        var yawRadians = Math.PI * _currentYaw / 180.0;
        var pitchRadians = Math.PI * _currentPitch / 180.0;
        var x = _currentDistance * Math.Cos(pitchRadians) * Math.Sin(yawRadians);
        var y = _currentDistance * Math.Sin(pitchRadians);
        var z = _currentDistance * Math.Cos(pitchRadians) * Math.Cos(yawRadians);
        var position = _currentFocus + new Vector3((float)x, (float)y, (float)z);

        _camera.Position = ToPoint3D(position);
        _camera.LookDirection = ToVector3D(_currentFocus - position);
        _camera.UpDirection = new Vector3D(0.0, 1.0, 0.0);
    }

    private static double SmoothFactor(double speed, TimeSpan deltaTime)
    {
        return 1.0 - Math.Exp(-speed * Math.Min(deltaTime.TotalSeconds, 0.05));
    }

    private static double Lerp(double current, double target, double amount)
    {
        return current + ((target - current) * amount);
    }

    private static Point3D ToPoint3D(Vector3 vector)
    {
        return new Point3D(vector.X, vector.Y, vector.Z);
    }

    private static Vector3D ToVector3D(Vector3 vector)
    {
        return new Vector3D(vector.X, vector.Y, vector.Z);
    }
}
