using Forge3D.Core.Dynamics;

namespace Forge3D.Core.Simulation.Telemetry;

public sealed class TelemetryRecorder
{
    private readonly List<TelemetrySample> _samples = [];
    private double _sampleAccumulator;

    public TelemetryRecorder(int maxSamples = 360, double sampleRateHz = 20.0)
    {
        MaxSamples = maxSamples;
        SampleRateHz = sampleRateHz;
    }

    public int MaxSamples { get; }

    public double SampleRateHz { get; }

    public IReadOnlyList<TelemetrySample> Samples => _samples;

    public bool TrySample(RigidBody? body, double simulationTime, double deltaTime, out TelemetrySample sample)
    {
        sample = default;
        if (body is null)
        {
            return false;
        }

        _sampleAccumulator += deltaTime;
        if (_sampleAccumulator < 1.0 / SampleRateHz)
        {
            return false;
        }

        _sampleAccumulator = 0.0;
        sample = new TelemetrySample(
            simulationTime,
            body.Position.Y,
            body.LinearVelocity.Length(),
            body.AngularVelocity.Length(),
            body.IsStatic ? 0.0f : 0.5f * body.Mass * body.LinearVelocity.LengthSquared());
        _samples.Add(sample);

        while (_samples.Count > MaxSamples)
        {
            _samples.RemoveAt(0);
        }

        return true;
    }

    public void Clear()
    {
        _samples.Clear();
        _sampleAccumulator = 0.0;
    }
}
