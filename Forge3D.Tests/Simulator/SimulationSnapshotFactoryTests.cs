using Forge3D.Contracts.Commands;
using Forge3D.Simulator.Hosting;

namespace Forge3D.Tests.Simulator;

public sealed class SimulationSnapshotFactoryTests
{
    [Fact]
    public void Snapshot_ContainsProfilerAndEngineeringState()
    {
        var host = new SimulationHost();
        host.LoadScenario(SimulationScenarioKind.Engineering);
        host.Step();

        var snapshot = host.Snapshot();

        Assert.NotNull(snapshot.Vehicle);
        Assert.NotNull(snapshot.Sensor);
        Assert.NotEmpty(snapshot.Mission.Waypoints);
        Assert.True(snapshot.PhysicsStats.BodyCount > 0);
    }

    [Fact]
    public void PlanPath_UpdatesNavigationSnapshot()
    {
        var host = new SimulationHost();
        host.LoadScenario(SimulationScenarioKind.Engineering);

        host.PlanPath(4.5f, 2.5f, 0.0f, 0.5f, MobilityModelKind.CarLike, PlannerKind.Auto);
        var snapshot = host.Snapshot();

        Assert.True(snapshot.Navigation.ExpandedNodes >= 0);
        Assert.True(snapshot.Navigation.Path.Count > 0);
    }
}
