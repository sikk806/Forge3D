namespace Forge3D.Contracts.States;

public sealed record PhysicsProfilerDto(
    int BodyCount,
    int ColliderCount,
    int PotentialPairCount,
    int CandidatePairCount,
    int ContactCount,
    double BroadPhaseMilliseconds,
    double NarrowPhaseMilliseconds,
    double SolverMilliseconds,
    double TotalPhysicsMilliseconds);
