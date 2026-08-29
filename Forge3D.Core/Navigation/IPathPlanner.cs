namespace Forge3D.Core.Navigation;

public interface IPathPlanner
{
    PathResult Plan(PathRequest request);
}
