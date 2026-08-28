using System.Numerics;
using Forge3D.Core.Collision;
using Forge3D.Core.Dynamics;

namespace Forge3D.Editor.ViewModels;

public sealed class SceneObjectViewModel : ViewModelBase
{
    private readonly Collider _collider;

    public SceneObjectViewModel(Collider collider)
    {
        _collider = collider;
    }

    public int Id => _collider.Id;

    public Collider Collider => _collider;

    public RigidBody Body => _collider.Body;

    public string Name
    {
        get => Body.Name;
        set
        {
            Body.Name = value;
            OnPropertyChanged();
        }
    }

    public ColliderType Type => _collider.Type;

    public bool IsStatic
    {
        get => Body.IsStatic;
        set
        {
            Body.IsStatic = value;
            Refresh();
        }
    }

    public float PositionX
    {
        get => Body.Position.X;
        set => SetPosition(new Vector3(value, Body.Position.Y, Body.Position.Z));
    }

    public float PositionY
    {
        get => Body.Position.Y;
        set => SetPosition(new Vector3(Body.Position.X, value, Body.Position.Z));
    }

    public float PositionZ
    {
        get => Body.Position.Z;
        set => SetPosition(new Vector3(Body.Position.X, Body.Position.Y, value));
    }

    public float Mass
    {
        get => Body.Mass;
        set
        {
            Body.Mass = MathF.Max(0.001f, value);
            Refresh();
        }
    }

    public float LinearDamping
    {
        get => Body.LinearDamping;
        set
        {
            Body.LinearDamping = Math.Clamp(value, 0.0f, 10.0f);
            Refresh();
        }
    }

    public float AngularDamping
    {
        get => Body.AngularDamping;
        set
        {
            Body.AngularDamping = Math.Clamp(value, 0.0f, 10.0f);
            Refresh();
        }
    }

    public float Friction
    {
        get => Body.Material.Friction;
        set
        {
            Body.Material.Friction = Math.Clamp(value, 0.0f, 2.0f);
            Refresh();
        }
    }

    public float Restitution
    {
        get => Body.Material.Restitution;
        set
        {
            Body.Material.Restitution = Math.Clamp(value, 0.0f, 1.0f);
            Refresh();
        }
    }

    public float RotationX
    {
        get => ToEulerDegrees(Body.Orientation).X;
        set => SetRotation(value, RotationY, RotationZ);
    }

    public float RotationY
    {
        get => ToEulerDegrees(Body.Orientation).Y;
        set => SetRotation(RotationX, value, RotationZ);
    }

    public float RotationZ
    {
        get => ToEulerDegrees(Body.Orientation).Z;
        set => SetRotation(RotationX, RotationY, value);
    }

    public float LinearVelocityX
    {
        get => Body.LinearVelocity.X;
        set
        {
            Body.LinearVelocity = new Vector3(value, Body.LinearVelocity.Y, Body.LinearVelocity.Z);
            Refresh();
        }
    }

    public float LinearVelocityY
    {
        get => Body.LinearVelocity.Y;
        set
        {
            Body.LinearVelocity = new Vector3(Body.LinearVelocity.X, value, Body.LinearVelocity.Z);
            Refresh();
        }
    }

    public float LinearVelocityZ
    {
        get => Body.LinearVelocity.Z;
        set
        {
            Body.LinearVelocity = new Vector3(Body.LinearVelocity.X, Body.LinearVelocity.Y, value);
            Refresh();
        }
    }

    public float AngularVelocityX
    {
        get => Body.AngularVelocity.X;
        set
        {
            Body.AngularVelocity = new Vector3(value, Body.AngularVelocity.Y, Body.AngularVelocity.Z);
            Refresh();
        }
    }

    public float AngularVelocityY
    {
        get => Body.AngularVelocity.Y;
        set
        {
            Body.AngularVelocity = new Vector3(Body.AngularVelocity.X, value, Body.AngularVelocity.Z);
            Refresh();
        }
    }

    public float AngularVelocityZ
    {
        get => Body.AngularVelocity.Z;
        set
        {
            Body.AngularVelocity = new Vector3(Body.AngularVelocity.X, Body.AngularVelocity.Y, value);
            Refresh();
        }
    }

    public float CurrentSpeed => Body.LinearVelocity.Length();

    public float AngularSpeed => Body.AngularVelocity.Length();

    public float KineticEnergy => Body.IsStatic ? 0.0f : 0.5f * Body.Mass * Body.LinearVelocity.LengthSquared();

    public string CenterOfMass => FormatVector(Body.Position);

    public string SleepState => Body.IsSleeping ? "Sleeping" : "Awake";

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(IsStatic));
        OnPropertyChanged(nameof(PositionX));
        OnPropertyChanged(nameof(PositionY));
        OnPropertyChanged(nameof(PositionZ));
        OnPropertyChanged(nameof(RotationX));
        OnPropertyChanged(nameof(RotationY));
        OnPropertyChanged(nameof(RotationZ));
        OnPropertyChanged(nameof(Mass));
        OnPropertyChanged(nameof(Friction));
        OnPropertyChanged(nameof(Restitution));
        OnPropertyChanged(nameof(LinearDamping));
        OnPropertyChanged(nameof(AngularDamping));
        OnPropertyChanged(nameof(LinearVelocityX));
        OnPropertyChanged(nameof(LinearVelocityY));
        OnPropertyChanged(nameof(LinearVelocityZ));
        OnPropertyChanged(nameof(AngularVelocityX));
        OnPropertyChanged(nameof(AngularVelocityY));
        OnPropertyChanged(nameof(AngularVelocityZ));
        OnPropertyChanged(nameof(CurrentSpeed));
        OnPropertyChanged(nameof(AngularSpeed));
        OnPropertyChanged(nameof(KineticEnergy));
        OnPropertyChanged(nameof(CenterOfMass));
        OnPropertyChanged(nameof(SleepState));
    }

    private void SetPosition(Vector3 position)
    {
        Body.Position = position;
        Refresh();
    }

    private void SetRotation(float xDegrees, float yDegrees, float zDegrees)
    {
        var x = Quaternion.CreateFromAxisAngle(Vector3.UnitX, DegreesToRadians(xDegrees));
        var y = Quaternion.CreateFromAxisAngle(Vector3.UnitY, DegreesToRadians(yDegrees));
        var z = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, DegreesToRadians(zDegrees));
        Body.Orientation = Quaternion.Normalize(z * y * x);
        Refresh();
    }

    private static Vector3 ToEulerDegrees(Quaternion q)
    {
        var sinrCosp = 2.0f * ((q.W * q.X) + (q.Y * q.Z));
        var cosrCosp = 1.0f - (2.0f * ((q.X * q.X) + (q.Y * q.Y)));
        var roll = MathF.Atan2(sinrCosp, cosrCosp);

        var sinp = 2.0f * ((q.W * q.Y) - (q.Z * q.X));
        var pitch = MathF.Abs(sinp) >= 1.0f ? MathF.CopySign(MathF.PI / 2.0f, sinp) : MathF.Asin(sinp);

        var sinyCosp = 2.0f * ((q.W * q.Z) + (q.X * q.Y));
        var cosyCosp = 1.0f - (2.0f * ((q.Y * q.Y) + (q.Z * q.Z)));
        var yaw = MathF.Atan2(sinyCosp, cosyCosp);

        return new Vector3(RadiansToDegrees(roll), RadiansToDegrees(pitch), RadiansToDegrees(yaw));
    }

    private static float DegreesToRadians(float degrees)
    {
        return degrees * MathF.PI / 180.0f;
    }

    private static float RadiansToDegrees(float radians)
    {
        return radians * 180.0f / MathF.PI;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"{value.X:F2}, {value.Y:F2}, {value.Z:F2}";
    }
}
