using System.Numerics;
using Forge3D.Core;
using Forge3D.Core.Collision;
using Forge3D.Core.Dynamics;

namespace Forge3D.Tests.Collision;

public sealed class AabbBroadPhaseTests
{
    [Fact]
    public void FindPairs_FiltersSeparatedDynamicBoxes()
    {
        var material = PhysicsMaterial.Steel;
        var nearA = new BoxCollider(new RigidBody("A", Vector3.Zero), new Vector3(0.5f), material);
        var nearB = new BoxCollider(new RigidBody("B", new Vector3(0.8f, 0.0f, 0.0f)), new Vector3(0.5f), material);
        var far = new BoxCollider(new RigidBody("C", new Vector3(5.0f, 0.0f, 0.0f)), new Vector3(0.5f), material);

        var result = new AabbBroadPhase().FindPairs([nearA, nearB, far]);

        Assert.Equal(3, result.PotentialPairCount);
        Assert.Single(result.CandidatePairs);
        Assert.Equal(nearA, result.CandidatePairs[0].ColliderA);
        Assert.Equal(nearB, result.CandidatePairs[0].ColliderB);
    }
}
