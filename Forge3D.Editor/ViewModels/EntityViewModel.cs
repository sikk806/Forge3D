using Forge3D.Core.Simulation;

namespace Forge3D.Editor.ViewModels;

public sealed class EntityViewModel : ViewModelBase
{
    public EntityViewModel(SimulationEntity entity)
    {
        Entity = entity;
    }

    public SimulationEntity Entity { get; }

    public string Name => Entity.Name;

    public EntityType Type => Entity.EntityType;

    public string DisplayName => $"{Type}  {Name}";

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DisplayName));
    }
}
