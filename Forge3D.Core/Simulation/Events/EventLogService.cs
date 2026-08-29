namespace Forge3D.Core.Simulation.Events;

public sealed class EventLogService
{
    private readonly List<SimulationEvent> _events = [];

    public EventLogService(int maxEvents = 120)
    {
        MaxEvents = maxEvents;
    }

    public int MaxEvents { get; }

    public IReadOnlyList<SimulationEvent> Events => _events;

    public void Add(double timestamp, EventSeverity severity, string source, string code, string message)
    {
        _events.Insert(0, new SimulationEvent(timestamp, severity, source, code, message));

        while (_events.Count > MaxEvents)
        {
            _events.RemoveAt(_events.Count - 1);
        }
    }

    public void Clear()
    {
        _events.Clear();
    }
}
