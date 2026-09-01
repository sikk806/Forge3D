namespace Forge3D.Contracts.States;

public sealed record DataImportStateDto(
    string Format,
    int TotalRecords,
    int ValidRecords,
    int InvalidRecords,
    IReadOnlyList<string> DetectedFields,
    IReadOnlyList<string> Capabilities,
    int ImportedObstacleCount);
