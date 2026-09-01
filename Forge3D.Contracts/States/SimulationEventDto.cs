namespace Forge3D.Contracts.States;

public sealed record SimulationEventDto(
    double Timestamp,
    string Severity,
    string Source,
    string Code,
    string Message);
