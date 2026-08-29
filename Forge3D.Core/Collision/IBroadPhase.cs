namespace Forge3D.Core.Collision;

public interface IBroadPhase
{
    BroadPhaseResult FindPairs(IReadOnlyList<Collider> colliders);
}
