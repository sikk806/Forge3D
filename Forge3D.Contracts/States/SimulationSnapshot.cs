namespace Forge3D.Contracts.States;

public sealed record SimulationSnapshot(
    long Sequence,
    double SimulationTime,
    bool IsRunning,
    float RenderInterpolationAlpha,
    IReadOnlyList<EntityStateDto> Entities,
    IReadOnlyList<RuntimeEntityStateDto> RuntimeEntities,
    VehicleStateDto? Vehicle,
    SensorStateDto? Sensor,
    MissionStateDto Mission,
    SafetyStateDto Safety,
    NavigationStateDto Navigation,
    PhysicsProfilerDto PhysicsStats,
    IReadOnlyList<ContactStateDto> Contacts,
    IReadOnlyList<SimulationEventDto> Events,
    DataImportStateDto DataImport);
