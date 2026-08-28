using System.Numerics;

namespace Forge3D.Core.Simulation.Mission;

public sealed class WaypointEntity : SimulationEntity
{
    public WaypointEntity(string id, string name, Vector3 position, int order, float reachRadius = 0.8f)
        : base(id, name, EntityType.Waypoint)
    {
        Position = position;
        Order = order;
        ReachRadius = reachRadius;
    }

    public int Order { get; }

    public float ReachRadius { get; set; }

    public bool IsReached { get; set; }
}
