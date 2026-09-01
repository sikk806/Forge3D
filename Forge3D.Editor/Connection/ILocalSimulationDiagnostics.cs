using Forge3D.Core.Dynamics;
using Forge3D.Core.Simulation;

namespace Forge3D.Editor.Connection;

public interface ILocalSimulationDiagnostics
{
    PhysicsWorld World { get; }

    SimulationRuntime Runtime { get; }
}
