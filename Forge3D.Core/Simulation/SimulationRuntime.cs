using Forge3D.Core.Dynamics;
using Forge3D.Core.Simulation.Events;
using Forge3D.Core.Simulation.Faults;
using Forge3D.Core.Simulation.Mission;
using Forge3D.Core.Simulation.Safety;
using Forge3D.Core.Simulation.Sensors;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Core.Simulation;

public sealed class SimulationRuntime
{
    private readonly PhysicsWorld _world;
    private SafetyState _lastSafetyState = SafetyState.Safe;

    public SimulationRuntime(PhysicsWorld world)
    {
        _world = world;
    }

    public IList<SimulationEntity> Entities { get; } = new List<SimulationEntity>();

    public VehicleController VehicleController { get; } = new();

    public MissionController MissionController { get; } = new();

    public SafetyEvaluator SafetyEvaluator { get; } = new();

    public FaultManager FaultManager { get; } = new();

    public VehicleEntity? Vehicle { get; set; }

    public SensorEntity? Sensor { get; set; }

    public SafetyResult SafetyResult { get; private set; } = SafetyResult.Safe;

    public void Reset()
    {
        Entities.Clear();
        Vehicle = null;
        Sensor = null;
        SafetyResult = SafetyResult.Safe;
        _lastSafetyState = SafetyState.Safe;
        FaultManager.Reset();
        MissionController.SetWaypoints([]);
    }

    public IReadOnlyList<SimulationRuntimeEvent> UpdateEngineeringSystems(float deltaTime, double simulationTime)
    {
        var events = new List<SimulationRuntimeEvent>();
        if (Vehicle is null)
        {
            return events;
        }

        MissionController.Update(Vehicle);
        VehicleController.Update(Vehicle, deltaTime);

        if (Sensor is not null)
        {
            Sensor.Update(Entities, deltaTime, simulationTime);
            SafetyResult = SafetyEvaluator.Evaluate(Vehicle, Sensor);

            if (SafetyResult.State == SafetyState.Critical
                && SafetyEvaluator.AutomaticEmergencyStopEnabled
                && Vehicle.MotionState != MotionState.EmergencyStop)
            {
                VehicleController.EmergencyStop(Vehicle);
                MissionController.EmergencyStop();
                events.Add(new SimulationRuntimeEvent(
                    EventSeverity.Critical,
                    "Safety",
                    "AUTO_ESTOP",
                    $"Automatic emergency stop: {SafetyResult.TargetName} at {SafetyResult.Distance:F2} m"));
            }
            else if (SafetyResult.State != _lastSafetyState && SafetyResult.State == SafetyState.Warning)
            {
                events.Add(new SimulationRuntimeEvent(
                    EventSeverity.Warning,
                    "Safety",
                    "WARNING_ZONE",
                    $"{SafetyResult.TargetName} inside warning zone ({SafetyResult.Distance:F2} m)"));
            }

            _lastSafetyState = SafetyResult.State;
        }

        Vehicle.CollisionCount = _world.Contacts.Count(contact =>
            ReferenceEquals(contact.BodyA, Vehicle.PhysicsBody) || ReferenceEquals(contact.BodyB, Vehicle.PhysicsBody));
        return events;
    }
}
