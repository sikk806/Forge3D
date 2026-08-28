using Forge3D.Core.Collision;

namespace Forge3D.Core.Solver;

public interface IContactSolver
{
    void Solve(IList<Contact> contacts, float deltaTime);
}
