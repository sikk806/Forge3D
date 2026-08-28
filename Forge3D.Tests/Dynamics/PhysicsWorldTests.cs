using System.Numerics;
using Forge3D.Core;
using Forge3D.Core.Dynamics;

namespace Forge3D.Tests.Dynamics;

public sealed class PhysicsWorldTests
{
    [Fact]
    public void Step_AppliesGravityAndMovesDynamicBodyDown()
    {
        var world = new PhysicsWorld();
        var body = new RigidBody("Falling Body", new Vector3(0.0f, 10.0f, 0.0f));

        world.AddBody(body);

        for (var i = 0; i < 60; i++)
        {
            world.Step(PhysicsSettings.DefaultFixedDeltaTime);
        }

        Assert.True(body.Position.Y < 10.0f);
        Assert.True(body.LinearVelocity.Y < 0.0f);
        Assert.Equal(1, world.LastStepStats.BodyCount);
    }

    [Fact]
    public void FixedStepRunner_ConsumesFrameDeltaInFixedSteps()
    {
        var world = new PhysicsWorld();
        var runner = new FixedStepRunner(world);

        var steps = runner.Step(PhysicsSettings.DefaultFixedDeltaTime * 3.1f);

        Assert.Equal(3, steps);
    }
}
