using Forge3D.Core.Simulation;
using Forge3D.Contracts.States;

namespace Forge3D.Editor.ViewModels;

public sealed class EntityViewModel : ViewModelBase
{
    private RuntimeEntityStateDto? _state;

    public EntityViewModel(SimulationEntity entity)
    {
        Entity = entity;
    }

    public EntityViewModel(RuntimeEntityStateDto state)
    {
        _state = state;
    }

    public SimulationEntity? Entity { get; }

    public string Id => _state?.Id ?? Entity?.Id ?? string.Empty;

    public string Name => _state?.Name ?? Entity?.Name ?? string.Empty;

    public string Type => _state?.EntityType ?? Entity?.EntityType.ToString() ?? string.Empty;

    public int? PhysicsEntityId => _state?.PhysicsEntityId;

    public string DisplayName => $"{Type}  {Name}";

    public void Update(RuntimeEntityStateDto state)
    {
        _state = state;
        Refresh();
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(DisplayName));
    }
}
