using System.Numerics;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Simulation.Telemetry;

namespace Forge3D.Tests.Simulation;

public sealed class TelemetryRecorderTests
{
    [Fact]
    public void TrySample_RespectsSamplingIntervalAndKeepsRecentSamples()
    {
        var body = new RigidBody("Body", new Vector3(0.0f, 3.0f, 0.0f))
        {
            LinearVelocity = new Vector3(2.0f, 0.0f, 0.0f)
        };
        var recorder = new TelemetryRecorder(maxSamples: 1, sampleRateHz: 10.0);

        var first = recorder.TrySample(body, 0.05, 0.05, out _);
        var second = recorder.TrySample(body, 0.10, 0.05, out var sample);
        recorder.TrySample(body, 0.20, 0.10, out _);

        Assert.False(first);
        Assert.True(second);
        Assert.Equal(3.0f, sample.PositionY);
        Assert.Equal(2.0f, sample.Speed);
        Assert.Single(recorder.Samples);
    }
}
