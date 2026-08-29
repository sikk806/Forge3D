using System.Numerics;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Simulation.Replay;

namespace Forge3D.Tests.Simulation;

public sealed class ReplayServiceTests
{
    [Fact]
    public void TryApply_RestoresCapturedBodyState()
    {
        var body = new RigidBody("Body", new Vector3(1.0f, 2.0f, 3.0f))
        {
            LinearVelocity = new Vector3(4.0f, 5.0f, 6.0f)
        };
        var replay = new ReplayService();

        replay.Capture(2.5, [body]);
        body.Position = Vector3.Zero;
        body.LinearVelocity = Vector3.Zero;

        var applied = replay.TryApply(0, [body], out var time);

        Assert.True(applied);
        Assert.Equal(2.5, time);
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), body.Position);
        Assert.Equal(new Vector3(4.0f, 5.0f, 6.0f), body.LinearVelocity);
    }
}
