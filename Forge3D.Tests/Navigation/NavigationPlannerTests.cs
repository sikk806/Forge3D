using System.Numerics;
using Forge3D.Core.Navigation;
using Forge3D.Core.Navigation.Collision;
using Forge3D.Core.Navigation.Following;
using Forge3D.Core.Navigation.Mobility;
using Forge3D.Core.Navigation.Planning;

namespace Forge3D.Tests.Navigation;

public sealed class NavigationPlannerTests
{
    [Fact]
    public void GridAStarPlanner_FindsPathAroundObstacle()
    {
        var request = new PathRequest
        {
            Start = new NavigationPose(-2.0f, 0.0f, 0.0f),
            Goal = new NavigationPose(2.0f, 0.0f, 0.0f),
            GridResolution = 0.5f,
            WorldMinX = -4.0f,
            WorldMaxX = 4.0f,
            WorldMinZ = -4.0f,
            WorldMaxZ = 4.0f,
            Vehicle = new VehicleNavigationProfile { Width = 0.5f, Length = 0.5f },
            Obstacles = [new NavigationObstacle("wall", 0.0f, 0.0f, 0.8f, 2.0f)]
        };

        var result = new GridAStarPlanner().Plan(request);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.Points.Count > 2);
        Assert.DoesNotContain(result.Points, point => MathF.Abs(point.X) < 0.45f && MathF.Abs(point.Z) < 1.1f);
    }

    [Fact]
    public void GridAStarPlanner_ReturnsFailureForBlockedGoal()
    {
        var request = new PathRequest
        {
            Start = new NavigationPose(-2.0f, 0.0f, 0.0f),
            Goal = new NavigationPose(0.0f, 0.0f, 0.0f),
            Vehicle = new VehicleNavigationProfile { Width = 0.5f, Length = 0.5f },
            Obstacles = [new NavigationObstacle("goal-blocker", 0.0f, 0.0f, 2.0f, 2.0f)]
        };

        var result = new GridAStarPlanner().Plan(request);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void HybridAStarPlanner_ProducesHeadingTransitions()
    {
        var request = new PathRequest
        {
            Start = new NavigationPose(-2.0f, -2.0f, 0.0f),
            Goal = new NavigationPose(2.0f, 2.0f, 45.0f),
            GridResolution = 0.75f,
            Vehicle = new VehicleNavigationProfile { Width = 0.5f, Length = 0.8f, Wheelbase = 1.0f, MaxSteeringAngleDegrees = 25.0f }
        };

        var result = new HybridAStarPlanner().Plan(request);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.Points.Select(point => MathF.Round(point.HeadingDegrees)).Distinct().Count() > 1);
    }

    [Fact]
    public void PlannerSelector_ChoosesPlannerFromMobility()
    {
        var selector = new PathPlannerSelector();

        Assert.IsType<GridAStarPlanner>(selector.Select(MobilityModelType.Holonomic, PlannerSelection.Auto));
        Assert.IsType<HybridAStarPlanner>(selector.Select(MobilityModelType.CarLike, PlannerSelection.Auto));
        Assert.IsType<GridAStarPlanner>(selector.Select(MobilityModelType.CarLike, PlannerSelection.GridAStar));
    }

    [Fact]
    public void PathCollisionChecker_UsesVehicleFootprint()
    {
        var checker = new PathCollisionChecker();
        var profile = new VehicleNavigationProfile { Width = 1.0f, Length = 1.0f };

        var blocked = checker.IsBlocked(0.4f, 0.0f, profile, [new NavigationObstacle("box", 1.0f, 0.0f, 0.5f, 0.5f)]);

        Assert.True(blocked);
    }

    [Fact]
    public void PathCollisionChecker_UsesVehicleHeadingAndClearance()
    {
        var checker = new PathCollisionChecker();
        var profile = new VehicleNavigationProfile { Width = 1.0f, Length = 2.0f, Clearance = 0.2f };

        var blocked = checker.IsBlocked(
            new NavigationPose(0.0f, 0.0f, 90.0f),
            profile,
            [new NavigationObstacle("box", 1.1f, 0.0f, 0.2f, 0.2f)]);

        Assert.True(blocked);
    }

    [Fact]
    public void GridAStarPlanner_LeavesClearanceForVehicleFootprint()
    {
        var request = new PathRequest
        {
            Start = new NavigationPose(-3.0f, 0.0f, 0.0f),
            Goal = new NavigationPose(3.0f, 0.0f, 0.0f),
            GridResolution = 0.5f,
            WorldMinX = -5.0f,
            WorldMaxX = 5.0f,
            WorldMinZ = -5.0f,
            WorldMaxZ = 5.0f,
            Vehicle = new VehicleNavigationProfile { Width = 1.0f, Length = 1.7f, Clearance = 0.3f },
            Obstacles = [new NavigationObstacle("wall", 0.0f, 0.0f, 0.8f, 2.0f)]
        };

        var result = new GridAStarPlanner().Plan(request);
        var checker = new PathCollisionChecker();

        Assert.True(result.Succeeded, result.Message);
        Assert.DoesNotContain(result.Points, point => checker.IsBlocked(point.X, point.Z, request.Vehicle, request.Obstacles));
    }

    [Fact]
    public void PathFollower_ReturnsNextTargetAndHeading()
    {
        var follower = new PathFollower(reachRadius: 0.25f)
        {
            LookAheadDistance = 10.0f
        };
        var path = new[]
        {
            new PathPoint(0.0f, 0.0f),
            new PathPoint(0.0f, 2.0f)
        };

        var found = follower.TryGetTarget(Vector3.Zero, path, out var target, out var heading);

        Assert.True(found);
        Assert.Equal(0.0f, target.X);
        Assert.Equal(2.0f, target.Z);
        Assert.Equal(0.0f, heading);
    }

    [Fact]
    public void PathFollower_UsesLookAheadTarget()
    {
        var follower = new PathFollower(reachRadius: 0.25f)
        {
            LookAheadDistance = 1.0f
        };
        var path = new[]
        {
            new PathPoint(0.0f, 0.0f),
            new PathPoint(0.0f, 2.0f),
            new PathPoint(2.0f, 2.0f)
        };

        var found = follower.TryGetTarget(Vector3.Zero, path, out var target, out _);

        Assert.True(found);
        Assert.Equal(0.0f, target.X);
        Assert.Equal(1.0f, target.Z);
    }

    [Fact]
    public void PathSimplifier_RemovesCollinearIntermediatePoints()
    {
        var simplifier = new PathSimplifier();
        var points = new[]
        {
            new PathPoint(0.0f, 0.0f),
            new PathPoint(1.0f, 0.0f),
            new PathPoint(2.0f, 0.0f),
            new PathPoint(2.0f, 1.0f)
        };

        var simplified = simplifier.Simplify(points);

        Assert.Equal(3, simplified.Count);
        Assert.Equal(0.0f, simplified[0].X);
        Assert.Equal(2.0f, simplified[1].X);
        Assert.Equal(1.0f, simplified[2].Z);
    }
}
