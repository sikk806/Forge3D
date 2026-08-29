using System.Windows;
using System.Windows.Media.Media3D;
using Vector3 = System.Numerics.Vector3;

namespace Forge3D.Editor.Input;

public sealed class CameraController
{
    private readonly PerspectiveCamera _camera;
    private double _yaw = -30.0;
    private double _pitch = 28.0;
    private double _distance = 13.0;
    private Vector3 _target = new(0.0f, 1.5f, 0.0f);

    public CameraController(PerspectiveCamera camera)
    {
        _camera = camera;
        Update();
    }

    public double Distance => _distance;

    public Vector3 CameraPosition => new((float)_camera.Position.X, (float)_camera.Position.Y, (float)_camera.Position.Z);

    public Vector3 Forward => Vector3.Normalize(_target - CameraPosition);

    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));

    public Vector3 Up => Vector3.Normalize(Vector3.Cross(Right, Forward));

    public void Orbit(Vector delta)
    {
        _yaw += delta.X * 0.35;
        _pitch = Math.Clamp(_pitch - delta.Y * 0.35, -80.0, 80.0);
        Update();
    }

    public void Pan(Vector delta)
    {
        _target -= Right * (float)(delta.X * 0.02);
        _target += Up * (float)(delta.Y * 0.02);
        Update();
    }

    public void ZoomByDrag(Vector delta)
    {
        _distance = Math.Clamp(_distance * (1.0 + delta.Y * 0.01), 3.0, 40.0);
        Update();
    }

    public void ZoomByWheel(int delta)
    {
        _distance = Math.Clamp(_distance * (delta > 0 ? 0.9 : 1.1), 3.0, 40.0);
        Update();
    }

    public void MoveVertical(float amount)
    {
        _target += Vector3.UnitY * amount;
        Update();
    }

    public void MovePlanar(Vector3 direction, float amount)
    {
        direction.Y = 0.0f;

        if (direction.LengthSquared() <= 0.000001f)
        {
            return;
        }

        _target += Vector3.Normalize(direction) * amount;
        Update();
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

    private void Update()
    {
        var yawRadians = Math.PI * _yaw / 180.0;
        var pitchRadians = Math.PI * _pitch / 180.0;
        var x = _distance * Math.Cos(pitchRadians) * Math.Sin(yawRadians);
        var y = _distance * Math.Sin(pitchRadians);
        var z = _distance * Math.Cos(pitchRadians) * Math.Cos(yawRadians);
        var position = _target + new Vector3((float)x, (float)y, (float)z);

        _camera.Position = ToPoint3D(position);
        _camera.LookDirection = ToVector3D(_target - position);
        _camera.UpDirection = new Vector3D(0.0, 1.0, 0.0);
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
