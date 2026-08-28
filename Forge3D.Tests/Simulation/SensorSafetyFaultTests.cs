using System.Numerics;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Simulation;
using Forge3D.Core.Simulation.Faults;
using Forge3D.Core.Simulation.Safety;
using Forge3D.Core.Simulation.Sensors;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Tests.Simulation;

public sealed class SensorSafetyFaultTests
{
    [Fact]
    public void Sensor_DetectsTargetInsideRangeAndFov()
    {
        var vehicle = new VehicleEntity("vehicle", "Vehicle", new RigidBody("Vehicle", Vector3.Zero));
        var sensor = new SensorEntity("sensor", "Sensor", vehicle)
        {
            Range = 5.0f,
            FieldOfViewDegrees = 90.0f,
            UpdateRateHz = 60.0f
        };
        var obstacle = new SimulationEntity("obstacle", "Obstacle", EntityType.Obstacle)
        {
            Position = new Vector3(0.0f, 0.0f, 3.0f)
        };

        sensor.Update([vehicle, obstacle], 1.0, 1.0);

        Assert.Single(sensor.Detections);
    }

    [Fact]
    public void Sensor_IgnoresTargetOutsideFov()
    {
        var vehicle = new VehicleEntity("vehicle", "Vehicle", new RigidBody("Vehicle", Vector3.Zero));
        var sensor = new SensorEntity("sensor", "Sensor", vehicle)
        {
            Range = 5.0f,
            FieldOfViewDegrees = 60.0f,
            UpdateRateHz = 60.0f
        };
        var obstacle = new SimulationEntity("obstacle", "Obstacle", EntityType.Obstacle)
        {
            Position = new Vector3(5.0f, 0.0f, 0.0f)
        };

        sensor.Update([vehicle, obstacle], 1.0, 1.0);

        Assert.Empty(sensor.Detections);
    }

    [Fact]
    public void SafetyEvaluator_ReturnsCriticalForNearDetection()
    {
        var vehicle = new VehicleEntity("vehicle", "Vehicle", new RigidBody("Vehicle", Vector3.Zero));
        var sensor = new SensorEntity("sensor", "Sensor", vehicle)
        {
            Range = 5.0f,
            FieldOfViewDegrees = 90.0f,
            UpdateRateHz = 60.0f
        };
        var obstacle = new SimulationEntity("obstacle", "Obstacle", EntityType.Obstacle)
        {
            Position = new Vector3(0.0f, 0.0f, 1.0f)
        };

        sensor.Update([vehicle, obstacle], 1.0, 1.0);
        var result = new SafetyEvaluator().Evaluate(vehicle, sensor);

        Assert.Equal(SafetyState.Critical, result.State);
    }

    [Fact]
    public void SafetyEvaluator_ReturnsWarningForWarningZoneDetection()
    {
        var vehicle = new VehicleEntity("vehicle", "Vehicle", new RigidBody("Vehicle", Vector3.Zero));
        var sensor = new SensorEntity("sensor", "Sensor", vehicle)
        {
            Range = 10.0f,
            FieldOfViewDegrees = 90.0f,
            UpdateRateHz = 60.0f
        };
        var obstacle = new SimulationEntity("obstacle", "Obstacle", EntityType.Obstacle)
        {
            Position = new Vector3(0.0f, 0.0f, 3.0f)
        };

        sensor.Update([vehicle, obstacle], 1.0, 1.0);
        var result = new SafetyEvaluator().Evaluate(vehicle, sensor);

        Assert.Equal(SafetyState.Warning, result.State);
    }

    [Fact]
    public void FaultManager_SensorFailureDisablesSensor()
    {
        var body = new RigidBody("Vehicle", Vector3.Zero);
        var vehicle = new VehicleEntity("vehicle", "Vehicle", body);
        var sensor = new SensorEntity("sensor", "Sensor", vehicle);
        var controller = new VehicleController();
        var faults = new FaultManager();

        faults.Toggle(FaultType.SensorFailure, vehicle, controller, sensor);

        Assert.False(sensor.IsEnabled);
        Assert.Equal(SensorState.Fault, sensor.State);
    }

    [Fact]
    public void FaultManager_MotorDegradationReducesControllerOutput()
    {
        var body = new RigidBody("Vehicle", Vector3.Zero);
        var vehicle = new VehicleEntity("vehicle", "Vehicle", body);
        var sensor = new SensorEntity("sensor", "Sensor", vehicle);
        var controller = new VehicleController();
        var faults = new FaultManager();

        faults.Toggle(FaultType.MotorDegradation, vehicle, controller, sensor);

        Assert.Equal(0.35f, controller.MotorScale);
    }
}
