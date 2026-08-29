namespace Forge3D.Core.Simulation.Telemetry;

public readonly record struct TelemetrySample(double Time, float PositionY, float Speed, float AngularSpeed, float KineticEnergy);
