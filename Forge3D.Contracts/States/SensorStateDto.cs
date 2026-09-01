using System.Numerics;

namespace Forge3D.Contracts.States;

public sealed record SensorStateDto(
    string Id,
    string Name,
    string State,
    Vector3 OwnerPosition,
    Quaternion OwnerOrientation,
    float Range,
    float FieldOfViewDegrees,
    IReadOnlyList<SensorDetectionDto> Detections);

public sealed record SensorDetectionDto(
    string TargetName,
    string TargetType,
    float Distance,
    float RelativeBearingDegrees);
