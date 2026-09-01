using Forge3D.Contracts.Commands;
using Forge3D.Simulator.Commands;
using Forge3D.Simulator.Hosting;
using System.Numerics;

namespace Forge3D.Tests.Simulator;

public sealed class SimulationHostTests
{
    [Fact]
    public void Host_InitializesDropScenarioWithoutWpf()
    {
        var host = new SimulationHost();

        host.ResetToDropScenario();
        var snapshot = host.Snapshot();

        Assert.False(snapshot.IsRunning);
        Assert.True(snapshot.Entities.Count >= 3);
        Assert.Equal(0.0, snapshot.SimulationTime);
    }

    [Fact]
    public void Step_AdvancesSimulationTime()
    {
        var host = new SimulationHost();
        host.ResetToDropScenario();

        host.Step();

        Assert.True(host.SimulationTime > 0.0);
        Assert.False(host.IsRunning);
    }

    [Fact]
    public void CommandHandler_StartPauseAndScenarioCommandsReachHost()
    {
        var host = new SimulationHost();
        var handler = new SimulationCommandHandler(host);

        handler.Handle(new LoadScenarioCommand(SimulationScenarioKind.Engineering));
        handler.Handle(new StartSimulationCommand());
        Assert.True(host.IsRunning);

        handler.Handle(new PauseSimulationCommand());
        Assert.False(host.IsRunning);
        Assert.NotNull(host.Runtime.Vehicle);
    }

    [Fact]
    public void Host_PeriodicallyRefreshesPlannedPathWhileMissionRuns()
    {
        var host = new SimulationHost();
        host.LoadScenario(SimulationScenarioKind.Engineering);
        host.PlanPath(4.5f, 2.5f, 0.0f, 0.5f, MobilityModelKind.CarLike, PlannerKind.Auto);
        host.StartMission();

        for (var i = 0; i < 220; i++)
        {
            host.Tick(1.0f / 60.0f);
        }

        Assert.True(host.AutomaticReplanCount > 0);
    }

    [Fact]
    public void Host_WarnsAfterThreeStoppedReplanFailures()
    {
        var host = new SimulationHost();
        host.LoadScenario(SimulationScenarioKind.Engineering);
        host.PlanPath(-1.0f, -2.0f, 0.0f, 0.5f, MobilityModelKind.CarLike, PlannerKind.Auto);
        host.Runtime.Vehicle!.PhysicsBody!.Orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 71.5f * MathF.PI / 180.0f);
        host.StartMission();

        for (var i = 0; i < 20; i++)
        {
            host.Tick(1.0f / 60.0f);
        }

        Assert.Contains(host.EventLogService.Events, item => item.Code == "AUTO_REPLAN_STALLED");
    }

    [Fact]
    public void ImportData_ReplacesPreviousImportedObstacles()
    {
        var firstFile = Path.GetTempFileName();
        var secondFile = Path.GetTempFileName();
        File.WriteAllText(firstFile, """
id,x,y,z,width,height,depth
import-a,0,0,0,1,1,1
import-b,1,0,0,1,1,1
""");
        File.WriteAllText(secondFile, """
id,x,y,z,width,height,depth
import-c,2,0,0,1,1,1
""");
        try
        {
            var host = new SimulationHost();
            host.LoadScenario(SimulationScenarioKind.Engineering);
            var baseObstacleCount = host.Runtime.Entities.Count(entity => entity.EntityType == Core.Simulation.EntityType.Obstacle);

            host.ImportData(firstFile);
            Assert.Equal(baseObstacleCount + 2, host.Runtime.Entities.Count(entity => entity.EntityType == Core.Simulation.EntityType.Obstacle));

            host.ImportData(secondFile);

            var obstacles = host.Runtime.Entities.Where(entity => entity.EntityType == Core.Simulation.EntityType.Obstacle).ToList();
            Assert.Equal(baseObstacleCount + 1, obstacles.Count);
            Assert.Contains(obstacles, entity => entity.Id == "import-c");
            Assert.DoesNotContain(obstacles, entity => entity.Id == "import-a");
            Assert.DoesNotContain(obstacles, entity => entity.Id == "import-b");
        }
        finally
        {
            File.Delete(firstFile);
            File.Delete(secondFile);
        }
    }

    [Fact]
    public void Host_AddsCustomObstacleWithoutReplacingScenarioObstacles()
    {
        var host = new SimulationHost();
        host.LoadScenario(SimulationScenarioKind.Customization);
        var baseObstacleCount = host.Runtime.Entities.Count(entity => entity.EntityType == Core.Simulation.EntityType.Obstacle);

        host.AddCustomObstacle("Manual_Box", new Vector3(1.0f, 0.5f, 1.0f), new Vector3(0.5f, 0.5f, 0.5f));

        var obstacles = host.Runtime.Entities.Where(entity => entity.EntityType == Core.Simulation.EntityType.Obstacle).ToList();
        Assert.Equal(baseObstacleCount + 1, obstacles.Count);
        Assert.Contains(obstacles, entity => entity.Name == "Manual_Box");
    }

    [Fact]
    public void Host_AddsAndClearsCustomWaypoints()
    {
        var host = new SimulationHost();
        host.LoadScenario(SimulationScenarioKind.Customization);
        host.ClearWaypoints();

        host.AddWaypoint("Manual_WP", new Vector3(2.0f, 0.05f, 1.5f), 0.7f);

        Assert.Single(host.Runtime.MissionController.Waypoints);
        Assert.Equal("Manual_WP", host.Runtime.MissionController.Waypoints[0].Name);
        Assert.Equal(Core.Simulation.Mission.MissionState.Ready, host.Runtime.MissionController.State);

        host.ClearWaypoints();

        Assert.Empty(host.Runtime.MissionController.Waypoints);
        Assert.DoesNotContain(host.Runtime.Entities, entity => entity.EntityType == Core.Simulation.EntityType.Waypoint);
    }

    [Fact]
    public void Host_AddsMultipleCustomWaypointsWithoutReplacingEarlierOnes()
    {
        var host = new SimulationHost();
        host.LoadScenario(SimulationScenarioKind.Customization);
        host.ClearWaypoints();

        host.AddWaypoint("Manual_A", new Vector3(1.0f, 0.05f, 1.0f), 0.7f);
        host.AddWaypoint("Manual_B", new Vector3(2.0f, 0.05f, 2.0f), 0.8f);
        host.AddWaypoint("Manual_C", new Vector3(3.0f, 0.05f, 3.0f), 0.9f);

        Assert.Equal(3, host.Runtime.MissionController.Waypoints.Count);
        Assert.Equal(new[] { "Manual_A", "Manual_B", "Manual_C" }, host.Runtime.MissionController.Waypoints.Select(item => item.Name));
        Assert.Equal(3, host.Runtime.MissionController.Waypoints.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void Host_DeletesRequestedWaypointOnly()
    {
        var host = new SimulationHost();
        host.LoadScenario(SimulationScenarioKind.Customization);
        host.ClearWaypoints();
        host.AddWaypoint("Manual_A", new Vector3(1.0f, 0.05f, 1.0f), 0.7f);
        host.AddWaypoint("Manual_B", new Vector3(2.0f, 0.05f, 2.0f), 0.8f);
        var waypointId = host.Runtime.MissionController.Waypoints[0].Id;

        var deleted = host.DeleteWaypoint(waypointId);

        Assert.True(deleted);
        Assert.Single(host.Runtime.MissionController.Waypoints);
        Assert.Equal("Manual_B", host.Runtime.MissionController.Waypoints[0].Name);
        Assert.DoesNotContain(host.Runtime.Entities, entity => entity.Id == waypointId);
    }

    [Fact]
    public void Host_DeletesSceneEntityButKeepsGroundProtected()
    {
        var host = new SimulationHost();
        host.LoadScenario(SimulationScenarioKind.Customization);
        var ground = host.Snapshot().Entities.First(entity => entity.ColliderType == "Plane");
        var obstacle = host.Snapshot().Entities.First(entity => entity.Name == "Obstacle_01");

        Assert.False(host.DeleteEntity(ground.Id));
        Assert.True(host.DeleteEntity(obstacle.Id));

        var snapshot = host.Snapshot();
        Assert.Contains(snapshot.Entities, entity => entity.Id == ground.Id);
        Assert.DoesNotContain(snapshot.Entities, entity => entity.Id == obstacle.Id);
    }

    [Fact]
    public void Host_DeletesRuntimeEntityById()
    {
        var host = new SimulationHost();
        host.LoadScenario(SimulationScenarioKind.Customization);

        var deleted = host.DeleteRuntimeEntity("sensor-01");

        Assert.True(deleted);
        Assert.Null(host.Runtime.Sensor);
        Assert.DoesNotContain(host.Runtime.Entities, entity => entity.Id == "sensor-01");
    }

    [Fact]
    public void Host_PastesStaticBoxAsObstacleEntity()
    {
        var host = new SimulationHost();
        host.LoadScenario(SimulationScenarioKind.Customization);
        var obstacleCount = host.Runtime.Entities.Count(entity => entity.EntityType == Core.Simulation.EntityType.Obstacle);

        var pasted = host.PasteEntity(new PasteEntityCommand(
            "Copied_Obstacle",
            "Box",
            new Vector3(1.5f, 0.5f, 1.5f),
            Quaternion.Identity,
            new Vector3(0.4f, 0.5f, 0.6f),
            0.0f,
            true,
            1.0f,
            0.8f,
            0.05f,
            0.01f,
            0.05f));

        Assert.True(pasted);
        Assert.Equal(obstacleCount + 1, host.Runtime.Entities.Count(entity => entity.EntityType == Core.Simulation.EntityType.Obstacle));
        Assert.Contains(host.Snapshot().Entities, entity => entity.Name == "Copied_Obstacle");
    }

    [Fact]
    public void Host_MovesWaypointOnGroundPlane()
    {
        var host = new SimulationHost();
        host.LoadScenario(SimulationScenarioKind.Customization);
        host.ClearWaypoints();
        host.AddWaypoint("Manual_WP", new Vector3(2.0f, 0.05f, 1.5f), 0.7f);
        var waypointId = host.Runtime.MissionController.Waypoints[0].Id;

        host.SetWaypointPosition(waypointId, new Vector3(-3.0f, 5.0f, 4.0f));

        var waypoint = host.Runtime.MissionController.Waypoints[0];
        Assert.Equal(-3.0f, waypoint.Position.X);
        Assert.Equal(0.05f, waypoint.Position.Y);
        Assert.Equal(4.0f, waypoint.Position.Z);
    }
}
