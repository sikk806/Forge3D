namespace Forge3D.Contracts.States;

public sealed record SafetyStateDto(
    string State,
    string TargetName,
    float Distance,
    float? TimeToCollisionSeconds);
