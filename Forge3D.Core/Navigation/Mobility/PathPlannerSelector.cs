using Forge3D.Core.Navigation.Planning;

namespace Forge3D.Core.Navigation.Mobility;

public sealed class PathPlannerSelector
{
    public IPathPlanner Select(MobilityModelType mobility, PlannerSelection planner)
    {
        return planner switch
        {
            PlannerSelection.GridAStar => new GridAStarPlanner(),
            PlannerSelection.HybridAStar => new HybridAStarPlanner(),
            _ => mobility == MobilityModelType.CarLike ? new HybridAStarPlanner() : new GridAStarPlanner()
        };
    }
}
