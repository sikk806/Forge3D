using System.Numerics;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Core.Simulation.Sensors;

public sealed class SensorSimulator
{
    public IReadOnlyList<SensorDetection> Update(SensorEntity sensor, IEnumerable<SimulationEntity> targets, double deltaTime, double simulationTime)
    {
        if (!sensor.IsEnabled || sensor.State is SensorState.Fault or SensorState.Offline)
        {
            sensor.SetDetections([]);
            sensor.Owner.SensorDetectionCount = 0;
            return sensor.Detections;
        }

        if (!sensor.Advance(deltaTime))
        {
            return sensor.Detections;
        }

        var detections = Detect(sensor, targets, simulationTime);
        sensor.SetDetections(detections);
        sensor.Owner.SensorDetectionCount = detections.Count;
        return sensor.Detections;
    }

    private static List<SensorDetection> Detect(SensorEntity sensor, IEnumerable<SimulationEntity> targets, double simulationTime)
    {
        var detections = new List<SensorDetection>();
        var owner = sensor.Owner;
        var origin = owner.Position;
        var forward = GetForward(owner);

        foreach (var target in targets)
        {
            if (!CanDetectTarget(owner, target))
            {
                continue;
            }

            var delta = target.Position - origin;
            var distance = delta.Length();
            if (distance <= 0.001f || distance > sensor.Range)
            {
                continue;
            }

            var direction = Vector3.Normalize(delta);
            var angle = MathF.Acos(Math.Clamp(Vector3.Dot(forward, direction), -1.0f, 1.0f)) * 180.0f / MathF.PI;
            if (angle > sensor.FieldOfViewDegrees * 0.5f)
            {
                continue;
            }

            var cross = Vector3.Cross(forward, direction);
            var signedBearing = MathF.CopySign(angle, cross.Y);
            detections.Add(new SensorDetection(target.Id, target.Name, target.EntityType, distance, signedBearing, simulationTime));
        }

        return detections;
    }

    private static Vector3 GetForward(VehicleEntity owner)
    {
        return Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, owner.Orientation));
    }

    private static bool CanDetectTarget(VehicleEntity owner, SimulationEntity target)
    {
        return target.IsActive
            && !ReferenceEquals(target, owner)
            && target.EntityType is not EntityType.Sensor and not EntityType.Waypoint and not EntityType.Environment;
    }
}
