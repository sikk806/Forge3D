using Forge3D.Core.Simulation.Events;

namespace Forge3D.Core.Simulation;

public readonly record struct SimulationRuntimeEvent(EventSeverity Severity, string Source, string Code, string Message);
