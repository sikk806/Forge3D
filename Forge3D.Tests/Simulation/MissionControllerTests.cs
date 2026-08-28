using System.Numerics;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Simulation.Mission;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Tests.Simulation;

public sealed class MissionControllerTests
{
    [Fact]
    public void Update_AdvancesWaypointWhenReached()
    {
        var mission = new MissionController();
        mission.SetWaypoints([
            new WaypointEntity("wp1", "WP1", Vector3.Zero, 1, 1.0f),
            new WaypointEntity("wp2", "WP2", new Vector3(5.0f, 0.0f, 0.0f), 2, 1.0f)
        ]);
        var vehicle = new VehicleEntity("vehicle", "Vehicle", new RigidBody("Vehicle", Vector3.Zero));

        mission.Start();
        mission.Update(vehicle);

        Assert.Equal("wp2", mission.CurrentWaypoint?.Id);
        Assert.True(mission.Progress > 0.0f);
    }

    [Fact]
    public void Update_CompletesMissionWhenFinalWaypointReached()
    {
        var mission = new MissionController();
        mission.SetWaypoints([
            new WaypointEntity("wp1", "WP1", Vector3.Zero, 1, 1.0f)
        ]);
        var vehicle = new VehicleEntity("vehicle", "Vehicle", new RigidBody("Vehicle", Vector3.Zero))
        {
            TargetSpeed = 2.0f
        };

        mission.Start();
        mission.Update(vehicle);

        Assert.Equal(MissionState.Completed, mission.State);
        Assert.Equal(0.0f, vehicle.TargetSpeed);
        Assert.Equal(1.0f, mission.Progress);
    }
}
