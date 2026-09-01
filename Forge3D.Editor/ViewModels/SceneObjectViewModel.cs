using System.Numerics;
using Forge3D.Core.Collision;
using Forge3D.Core.Dynamics;
using Forge3D.Contracts.States;

namespace Forge3D.Editor.ViewModels;

public sealed class SceneObjectViewModel : ViewModelBase
{
    private Collider? _collider;
    private EntityStateDto _state;

    public SceneObjectViewModel(Collider collider)
    {
        _collider = collider;
        _state = FromCollider(collider);
    }

    public SceneObjectViewModel(EntityStateDto state)
    {
        _state = state;
    }

    public int Id => _state.Id;

    public Collider? Collider => _collider;

    public RigidBody? Body => _collider?.Body;

    public string Name
    {
        get => _state.Name;
        set
        {
            if (_collider is not null)
            {
                _collider.Body.Name = value;
            }

            _state = _state with { Name = value };
            OnPropertyChanged();
        }
    }

    public string Type => _state.ColliderType;

    public bool IsStatic
    {
        get => _state.IsStatic;
        set
        {
            if (_collider is not null)
            {
                _collider.Body.IsStatic = value;
            }

            _state = _state with { IsStatic = value };
            Refresh();
        }
    }

    public float PositionX
    {
        get => _state.Position.X;
        set => SetPosition(new Vector3(value, _state.Position.Y, _state.Position.Z));
    }

    public float PositionY
    {
        get => _state.Position.Y;
        set => SetPosition(new Vector3(_state.Position.X, value, _state.Position.Z));
    }

    public float PositionZ
    {
        get => _state.Position.Z;
        set => SetPosition(new Vector3(_state.Position.X, _state.Position.Y, value));
    }

    public float Mass
    {
        get => _state.Mass;
        set
        {
            var mass = MathF.Max(0.001f, value);
            if (_collider is not null)
            {
                _collider.Body.Mass = mass;
            }

            _state = _state with { Mass = mass };
            Refresh();
        }
    }

    public float LinearDamping
    {
        get => _state.LinearDamping;
        set
        {
            var damping = Math.Clamp(value, 0.0f, 10.0f);
            if (_collider is not null)
            {
                _collider.Body.LinearDamping = damping;
            }

            _state = _state with { LinearDamping = damping };
            Refresh();
        }
    }

    public float AngularDamping
    {
        get => _state.AngularDamping;
        set
        {
            var damping = Math.Clamp(value, 0.0f, 10.0f);
            if (_collider is not null)
            {
                _collider.Body.AngularDamping = damping;
            }

            _state = _state with { AngularDamping = damping };
            Refresh();
        }
    }

    public float Friction
    {
        get => _state.Friction;
        set
        {
            var friction = Math.Clamp(value, 0.0f, 2.0f);
            if (_collider is not null)
            {
                _collider.Body.Material.Friction = friction;
            }

            _state = _state with { Friction = friction };
            Refresh();
        }
    }

    public float Restitution
    {
        get => _state.Restitution;
        set
        {
            var restitution = Math.Clamp(value, 0.0f, 1.0f);
            if (_collider is not null)
            {
                _collider.Body.Material.Restitution = restitution;
            }

            _state = _state with { Restitution = restitution };
            Refresh();
        }
    }

    public float RotationX
    {
        get => ToEulerDegrees(_state.Orientation).X;
        set => SetRotation(value, RotationY, RotationZ);
    }

    public float RotationY
    {
        get => ToEulerDegrees(_state.Orientation).Y;
        set => SetRotation(RotationX, value, RotationZ);
    }

    public float RotationZ
    {
        get => ToEulerDegrees(_state.Orientation).Z;
        set => SetRotation(RotationX, RotationY, value);
    }

    public float LinearVelocityX
    {
        get => _state.LinearVelocity.X;
        set
        {
            var velocity = new Vector3(value, _state.LinearVelocity.Y, _state.LinearVelocity.Z);
            if (_collider is not null)
            {
                _collider.Body.LinearVelocity = velocity;
            }

            _state = _state with { LinearVelocity = velocity };
            Refresh();
        }
    }

    public float LinearVelocityY
    {
        get => _state.LinearVelocity.Y;
        set
        {
            var velocity = new Vector3(_state.LinearVelocity.X, value, _state.LinearVelocity.Z);
            if (_collider is not null)
            {
                _collider.Body.LinearVelocity = velocity;
            }

            _state = _state with { LinearVelocity = velocity };
            Refresh();
        }
    }

    public float LinearVelocityZ
    {
        get => _state.LinearVelocity.Z;
        set
        {
            var velocity = new Vector3(_state.LinearVelocity.X, _state.LinearVelocity.Y, value);
            if (_collider is not null)
            {
                _collider.Body.LinearVelocity = velocity;
            }

            _state = _state with { LinearVelocity = velocity };
            Refresh();
        }
    }

    public float AngularVelocityX
    {
        get => _state.AngularVelocity.X;
        set
        {
            var velocity = new Vector3(value, _state.AngularVelocity.Y, _state.AngularVelocity.Z);
            if (_collider is not null)
            {
                _collider.Body.AngularVelocity = velocity;
            }

            _state = _state with { AngularVelocity = velocity };
            Refresh();
        }
    }

    public float AngularVelocityY
    {
        get => _state.AngularVelocity.Y;
        set
        {
            var velocity = new Vector3(_state.AngularVelocity.X, value, _state.AngularVelocity.Z);
            if (_collider is not null)
            {
                _collider.Body.AngularVelocity = velocity;
            }

            _state = _state with { AngularVelocity = velocity };
            Refresh();
        }
    }

    public float AngularVelocityZ
    {
        get => _state.AngularVelocity.Z;
        set
        {
            var velocity = new Vector3(_state.AngularVelocity.X, _state.AngularVelocity.Y, value);
            if (_collider is not null)
            {
                _collider.Body.AngularVelocity = velocity;
            }

            _state = _state with { AngularVelocity = velocity };
            Refresh();
        }
    }

    public float CurrentSpeed => _state.LinearVelocity.Length();

    public float AngularSpeed => _state.AngularVelocity.Length();

    public float KineticEnergy => _state.IsStatic ? 0.0f : 0.5f * _state.Mass * _state.LinearVelocity.LengthSquared();

    public string CenterOfMass => FormatVector(_state.Position);

    public string SleepState => _state.IsActive ? "Awake" : "Sleeping";

    public void Update(EntityStateDto state)
    {
        _state = state;
        Refresh();
    }

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
        if (_collider is not null)
        {
            _collider.Body.Position = position;
        }

        _state = _state with { Position = position };
        Refresh();
    }

    private void SetRotation(float xDegrees, float yDegrees, float zDegrees)
    {
        var x = Quaternion.CreateFromAxisAngle(Vector3.UnitX, DegreesToRadians(xDegrees));
        var y = Quaternion.CreateFromAxisAngle(Vector3.UnitY, DegreesToRadians(yDegrees));
        var z = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, DegreesToRadians(zDegrees));
        var orientation = Quaternion.Normalize(z * y * x);
        if (_collider is not null)
        {
            _collider.Body.Orientation = orientation;
        }

        _state = _state with { Orientation = orientation };
        Refresh();
    }

    private static EntityStateDto FromCollider(Collider collider)
    {
        var body = collider.Body;
        var radius = collider is SphereCollider sphere ? sphere.Radius : 0.0f;
        var halfExtents = collider is BoxCollider box ? box.HalfExtents : body.HalfExtents;
        return new EntityStateDto(
            collider.Id,
            body.Name,
            "PhysicsBody",
            collider.Type.ToString(),
            body.Position,
            body.Orientation,
            body.PreviousPose.Position,
            body.PreviousPose.Orientation,
            body.LinearVelocity,
            body.AngularVelocity,
            halfExtents,
            radius,
            body.IsStatic,
            !body.IsSleeping,
            body.Mass,
            body.LinearDamping,
            body.AngularDamping,
            body.Material.Friction,
            body.Material.Restitution);
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
