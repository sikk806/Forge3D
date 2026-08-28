namespace Forge3D.Core.Simulation.Sensors;

public readonly record struct SensorDetection(
    string TargetId,
    string TargetName,
    EntityType TargetType,
    float Distance,
    float RelativeBearingDegrees,
    double Timestamp);
