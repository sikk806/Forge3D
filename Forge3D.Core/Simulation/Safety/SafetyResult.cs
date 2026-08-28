namespace Forge3D.Core.Simulation.Safety;

public readonly record struct SafetyResult(
    SafetyState State,
    string TargetName,
    float Distance,
    float? TimeToCollisionSeconds)
{
    public static SafetyResult Safe => new(SafetyState.Safe, string.Empty, 0.0f, null);
}
