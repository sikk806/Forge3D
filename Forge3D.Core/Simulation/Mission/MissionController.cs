using System.Numerics;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Core.Simulation.Mission;

public sealed class MissionController
{
    private readonly List<WaypointEntity> _waypoints = [];

    public IReadOnlyList<WaypointEntity> Waypoints => _waypoints;

    public MissionState State { get; private set; } = MissionState.Idle;

    public int CurrentIndex { get; private set; }

    public WaypointEntity? CurrentWaypoint => CurrentIndex >= 0 && CurrentIndex < _waypoints.Count ? _waypoints[CurrentIndex] : null;

    public float Progress => _waypoints.Count == 0 ? 0.0f : _waypoints.Count(item => item.IsReached) / (float)_waypoints.Count;

    public void SetWaypoints(IEnumerable<WaypointEntity> waypoints)
    {
        _waypoints.Clear();
        _waypoints.AddRange(waypoints.OrderBy(item => item.Order));
        Reset();
        State = _waypoints.Count > 0 ? MissionState.Ready : MissionState.Idle;
    }

    public void Start()
    {
        if (_waypoints.Count == 0)
        {
            State = MissionState.Idle;
            return;
        }

        State = MissionState.Running;
    }

    public void Pause()
    {
        if (State == MissionState.Running)
        {
            State = MissionState.Paused;
        }
    }

    public void Resume()
    {
        if (State == MissionState.Paused)
        {
            State = MissionState.Running;
        }
    }

    public void Abort()
    {
        State = MissionState.Failed;
    }

    public void EmergencyStop()
    {
        State = MissionState.EmergencyStopped;
    }

    public void Reset()
    {
        foreach (var waypoint in _waypoints)
        {
            waypoint.IsReached = false;
        }

        CurrentIndex = 0;
        State = _waypoints.Count > 0 ? MissionState.Ready : MissionState.Idle;
    }

    public void Update(VehicleEntity vehicle)
    {
        if (State != MissionState.Running || CurrentWaypoint is null)
        {
            return;
        }

        var waypoint = CurrentWaypoint;
        var toWaypoint = waypoint.Position - vehicle.Position;
        var distance = toWaypoint.Length();

        if (distance <= waypoint.ReachRadius)
        {
            waypoint.IsReached = true;
            CurrentIndex++;

            if (CurrentIndex >= _waypoints.Count)
            {
                vehicle.TargetSpeed = 0.0f;
                State = MissionState.Completed;
                return;
            }
        }

        if (CurrentWaypoint is not null)
        {
            var direction = Vector3.Normalize(CurrentWaypoint.Position - vehicle.Position);
            vehicle.TargetHeadingDegrees = MathF.Atan2(direction.X, direction.Z) * 180.0f / MathF.PI;
            vehicle.TargetSpeed = 3.0f;
            vehicle.CurrentWaypointId = CurrentWaypoint.Id;
        }
    }
}
