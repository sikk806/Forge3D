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
