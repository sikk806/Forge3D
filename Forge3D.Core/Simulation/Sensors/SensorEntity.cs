using System.Numerics;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Core.Simulation.Sensors;

public sealed class SensorEntity : SimulationEntity
{
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
        if (!IsEnabled || State is SensorState.Fault or SensorState.Offline)
        {
            _detections.Clear();
            Owner.SensorDetectionCount = 0;
            return;
        }

        _accumulator += deltaTime;
        var interval = 1.0 / Math.Max(0.1f, UpdateRateHz);
        if (_accumulator < interval)
        {
            return;
        }

        _accumulator = 0.0;
        _detections.Clear();
        var origin = Owner.Position;
        var forward = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, Owner.Orientation));

        foreach (var target in targets)
        {
            if (!target.IsActive || ReferenceEquals(target, Owner) || target.EntityType is EntityType.Sensor or EntityType.Waypoint or EntityType.Environment)
            {
                continue;
            }

            var delta = target.Position - origin;
            var distance = delta.Length();
            if (distance <= 0.001f || distance > Range)
            {
                continue;
            }

            var direction = Vector3.Normalize(delta);
            var angle = MathF.Acos(Math.Clamp(Vector3.Dot(forward, direction), -1.0f, 1.0f)) * 180.0f / MathF.PI;
            if (angle > FieldOfViewDegrees * 0.5f)
            {
                continue;
            }

            var cross = Vector3.Cross(forward, direction);
            var signedBearing = MathF.CopySign(angle, cross.Y);
            _detections.Add(new SensorDetection(target.Id, target.Name, target.EntityType, distance, signedBearing, simulationTime));
        }

        Owner.SensorDetectionCount = _detections.Count;
    }
}
