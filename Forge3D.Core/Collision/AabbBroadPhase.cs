using System.Diagnostics;

namespace Forge3D.Core.Collision;

public sealed class AabbBroadPhase : IBroadPhase
{
    public BroadPhaseResult FindPairs(IReadOnlyList<Collider> colliders)
    {
        var stopwatch = Stopwatch.StartNew();
        var bounds = new AabbCache[colliders.Count];

        for (var i = 0; i < colliders.Count; i++)
        {
            var collider = colliders[i];
            bounds[i] = new AabbCache(collider is PlaneCollider, collider.ComputeBounds());
        }

        var candidatePairs = new List<CollisionPair>();
        var potentialPairs = 0;

        for (var i = 0; i < colliders.Count; i++)
        {
            for (var j = i + 1; j < colliders.Count; j++)
            {
                var colliderA = colliders[i];
                var colliderB = colliders[j];

                if (ReferenceEquals(colliderA.Body, colliderB.Body)
                    || (colliderA.Body.IsStatic && colliderB.Body.IsStatic))
                {
                    continue;
                }

                potentialPairs++;

                if (!bounds[i].IsPlane
                    && !bounds[j].IsPlane
                    && !bounds[i].Bounds.Intersects(bounds[j].Bounds))
                {
                    continue;
                }

                candidatePairs.Add(new CollisionPair(colliderA, colliderB));
            }
        }

        stopwatch.Stop();
        return new BroadPhaseResult(candidatePairs, potentialPairs, stopwatch.Elapsed);
    }

    private readonly record struct AabbCache(bool IsPlane, Mathematics.Aabb Bounds);
}
