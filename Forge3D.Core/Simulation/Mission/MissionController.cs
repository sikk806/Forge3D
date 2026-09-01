using System.Numerics;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Core.Simulation.Mission;

public sealed class MissionController
{
    private readonly List<WaypointEntity> _waypoints = [];

    public IReadOnlyList<WaypointEntity> Waypoints => _waypoints;

    public MissionState State { get; private set; } = MissionState.Idle;

    public int CurrentIndex { get; private set; }

    public float LookAheadDistance { get; set; } = 1.8f;

    public float LookAheadSpeedFactor { get; set; } = 0.25f;

    public float StopAndTurnThresholdDegrees { get; set; } = 35.0f;

    public float HeadingAlignmentToleranceDegrees { get; set; } = 8.0f;

    public float CruiseSpeed { get; set; } = 3.0f;

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
            var directDirection = DirectionTo(vehicle.Position, CurrentWaypoint.Position);
            var directHeading = ToHeadingDegrees(directDirection);
            var directHeadingError = MathF.Abs(NormalizeAngle(directHeading - vehicle.HeadingDegrees));
            if (directHeadingError >= StopAndTurnThresholdDegrees)
            {
                vehicle.TargetHeadingDegrees = directHeading;
                vehicle.TargetSpeed = 0.0f;
                vehicle.CurrentWaypointId = CurrentWaypoint.Id;
                return;
            }

            var targetPosition = GetLookAheadPosition(vehicle);
            var direction = DirectionTo(vehicle.Position, targetPosition);
            vehicle.TargetHeadingDegrees = ToHeadingDegrees(direction);
            var headingError = MathF.Abs(NormalizeAngle(vehicle.TargetHeadingDegrees - vehicle.HeadingDegrees));
            vehicle.TargetSpeed = headingError <= HeadingAlignmentToleranceDegrees ? CruiseSpeed : 0.0f;
            vehicle.CurrentWaypointId = CurrentWaypoint.Id;
        }
    }

    private static Vector3 DirectionTo(Vector3 from, Vector3 to)
    {
        var delta = to - from;
        delta.Y = 0.0f;
        return delta.LengthSquared() <= 0.0001f ? Vector3.UnitZ : Vector3.Normalize(delta);
    }

    private static float ToHeadingDegrees(Vector3 direction)
    {
        return MathF.Atan2(direction.X, direction.Z) * 180.0f / MathF.PI;
    }

    private static float NormalizeAngle(float degrees)
    {
        while (degrees > 180.0f)
        {
            degrees -= 360.0f;
        }

        while (degrees < -180.0f)
        {
            degrees += 360.0f;
        }

        return degrees;
    }

    private Vector3 GetLookAheadPosition(VehicleEntity vehicle)
    {
        var lookAhead = LookAheadDistance + (vehicle.CurrentSpeed * LookAheadSpeedFactor);
        var previous = vehicle.Position;

        for (var index = CurrentIndex; index < _waypoints.Count; index++)
        {
            var candidate = _waypoints[index].Position;
            var segmentLength = DistanceXZ(previous, candidate);

            if (segmentLength >= lookAhead)
            {
                var t = Math.Clamp(lookAhead / MathF.Max(0.001f, segmentLength), 0.0f, 1.0f);
                return Vector3.Lerp(previous, candidate, t);
            }

            lookAhead -= segmentLength;
            previous = candidate;
        }

        return _waypoints.Count == 0 ? vehicle.Position : _waypoints[^1].Position;
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
