using System.Numerics;

namespace Forge3D.Contracts.States;

public sealed record MissionStateDto(
    string State,
    float Progress,
    WaypointStateDto? CurrentWaypoint,
    IReadOnlyList<WaypointStateDto> Waypoints);

public sealed record WaypointStateDto(
    string Id,
    string Name,
    Vector3 Position,
    int Sequence,
    bool IsReached,
    float AcceptanceRadius);
