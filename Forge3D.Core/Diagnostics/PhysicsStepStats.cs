namespace Forge3D.Core.Diagnostics;

public readonly record struct PhysicsStepStats(
    int BodyCount,
    TimeSpan TotalPhysicsTime,
    int ColliderCount = 0,
    int PotentialPairCount = 0,
    int CandidatePairCount = 0,
    int ContactCount = 0,
    TimeSpan BroadPhaseTime = default,
    TimeSpan NarrowPhaseTime = default,
    TimeSpan CollisionTime = default,
    TimeSpan SolverTime = default);
