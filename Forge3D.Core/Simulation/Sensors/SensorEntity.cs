using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Core.Simulation.Sensors;

public sealed class SensorEntity : SimulationEntity
{
    private static readonly SensorSimulator Simulator = new();
    private double _accumulator;
    private readonly List<SensorDetection> _detections = [];

    public SensorEntity(string id, string name, VehicleEntity owner)
        : base(id, name, EntityType.Sensor)
    {
        Owner = owner;
    }

    public VehicleEntity Owner { get; }

    public float Range { get; set; } = 8.0f;

    public float FieldOfViewDegrees { get; set; } = 70.0f;

    public float UpdateRateHz { get; set; } = 10.0f;

    public bool IsEnabled { get; set; } = true;

    public SensorState State { get; set; } = SensorState.Normal;

    public IReadOnlyList<SensorDetection> Detections => _detections;

    public void Update(IEnumerable<SimulationEntity> targets, double deltaTime, double simulationTime)
    {
        Simulator.Update(this, targets, deltaTime, simulationTime);
    }

    internal bool Advance(double deltaTime)
    {
        _accumulator += deltaTime;
        var interval = 1.0 / Math.Max(0.1f, UpdateRateHz);
        if (_accumulator < interval)
        {
            return false;
        }

        _accumulator = 0.0;
        return true;
    }

    internal void SetDetections(IEnumerable<SensorDetection> detections)
    {
        _detections.Clear();
        _detections.AddRange(detections);
    }
}
