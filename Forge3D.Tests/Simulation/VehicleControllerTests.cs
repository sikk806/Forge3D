using System.Numerics;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Tests.Simulation;

public sealed class VehicleControllerTests
{
    [Fact]
    public void Update_AppliesForceForTargetSpeed()
    {
        var body = new RigidBody("Vehicle", Vector3.Zero);
        var vehicle = new VehicleEntity("vehicle", "Vehicle", body)
        {
            TargetSpeed = 3.0f
        };
        var controller = new VehicleController();

        controller.Update(vehicle);

        Assert.NotEqual(Vector3.Zero, body.ForceAccumulator);
        Assert.Equal(MotionState.Moving, vehicle.MotionState);
    }

    [Fact]
    public void Update_AppliesTorqueForTargetHeading()
    {
        var body = new RigidBody("Vehicle", Vector3.Zero);
        var vehicle = new VehicleEntity("vehicle", "Vehicle", body)
        {
            TargetHeadingDegrees = 90.0f
        };
        var controller = new VehicleController();

        controller.Update(vehicle);

        Assert.True(body.TorqueAccumulator.Y > 0.0f);
    }

    [Fact]
    public void Update_ReducesForwardForceWhenHeadingErrorIsLarge()
    {
        var straightBody = new RigidBody("StraightVehicle", Vector3.Zero);
        var turningBody = new RigidBody("TurningVehicle", Vector3.Zero);
        var straightVehicle = new VehicleEntity("straight", "StraightVehicle", straightBody)
        {
            TargetSpeed = 3.0f,
            TargetHeadingDegrees = 0.0f
        };
        var turningVehicle = new VehicleEntity("turning", "TurningVehicle", turningBody)
        {
            TargetSpeed = 3.0f,
            TargetHeadingDegrees = 90.0f
        };
        var controller = new VehicleController();

        for (var i = 0; i < 60; i++)
        {
            controller.Update(straightVehicle);
            controller.Update(turningVehicle);
        }

        Assert.True(turningVehicle.CommandedSpeed < straightVehicle.CommandedSpeed);
        Assert.True(turningBody.TorqueAccumulator.Y > 0.0f);
    }

    [Fact]
    public void Update_LimitsCommandedSpeedAcceleration()
    {
        var body = new RigidBody("Vehicle", Vector3.Zero);
        var vehicle = new VehicleEntity("vehicle", "Vehicle", body)
        {
            TargetSpeed = 8.0f
        };
        var controller = new VehicleController
        {
            MaxAcceleration = 2.0f
        };

        controller.Update(vehicle, 0.5f);

        Assert.Equal(1.0f, vehicle.CommandedSpeed, precision: 3);
    }

    [Fact]
    public void EmergencyStop_SetsTargetSpeedToZero()
    {
        var vehicle = new VehicleEntity("vehicle", "Vehicle", new RigidBody("Vehicle", Vector3.Zero))
        {
            TargetSpeed = 3.0f
        };

        new VehicleController().EmergencyStop(vehicle);

        Assert.Equal(0.0f, vehicle.TargetSpeed);
        Assert.Equal(MotionState.EmergencyStop, vehicle.MotionState);
    }
}
