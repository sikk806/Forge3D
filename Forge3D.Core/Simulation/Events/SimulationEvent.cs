namespace Forge3D.Core.Simulation.Events;

public readonly record struct SimulationEvent(
    double Timestamp,
    EventSeverity Severity,
    string Source,
    string Code,
    string Message);
