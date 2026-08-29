namespace Forge3D.Core.Simulation.Replay;

public readonly record struct ReplayFrame(double Time, BodySnapshot[] Bodies);
