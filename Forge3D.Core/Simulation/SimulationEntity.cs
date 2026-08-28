using System.Numerics;
using Forge3D.Core.Dynamics;

namespace Forge3D.Core.Simulation;

public class SimulationEntity
{
    public SimulationEntity(string id, string name, EntityType type)
    {
        Id = id;
        Name = name;
        EntityType = type;
    }

    public string Id { get; }

    public string Name { get; set; }

    public EntityType EntityType { get; }

    public bool IsActive { get; set; } = true;

    public RigidBody? PhysicsBody { get; set; }

    public Vector3 Position
    {
        get => PhysicsBody?.Position ?? _position;
        set
        {
            _position = value;
            if (PhysicsBody is not null)
            {
                PhysicsBody.Position = value;
            }
        }
    }

    public Quaternion Orientation
    {
        get => PhysicsBody?.Orientation ?? _orientation;
        set
        {
            _orientation = Quaternion.Normalize(value);
            if (PhysicsBody is not null)
            {
                PhysicsBody.Orientation = _orientation;
            }
        }
    }

    private Vector3 _position;
    private Quaternion _orientation = Quaternion.Identity;
}
