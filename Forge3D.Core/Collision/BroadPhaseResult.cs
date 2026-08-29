namespace Forge3D.Core.Collision;

public readonly record struct BroadPhaseResult(
    IReadOnlyList<CollisionPair> CandidatePairs,
    int PotentialPairCount,
    TimeSpan Elapsed);
