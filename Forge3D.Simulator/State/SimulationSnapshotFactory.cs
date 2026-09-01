using Forge3D.Core.Collision;
using Forge3D.Core.Diagnostics;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Simulation;
using Forge3D.Core.Simulation.Mission;
using Forge3D.Contracts.States;
using Forge3D.Simulator.Hosting;

namespace Forge3D.Simulator.State;

public sealed class SimulationSnapshotFactory
{
    private readonly SimulationHost _host;

    public SimulationSnapshotFactory(SimulationHost host)
    {
        _host = host;
    }

    public SimulationSnapshot Create(long sequence)
    {
        return new SimulationSnapshot(
            sequence,
            _host.SimulationTime,
            _host.IsRunning,
            _host.RenderInterpolationAlpha,
            _host.World.Colliders.Select(ToEntityState).ToList(),
            _host.Runtime.Entities.Select(ToRuntimeEntityState).ToList(),
            ToVehicleState(),
            ToSensorState(),
            ToMissionState(),
            ToSafetyState(),
            ToNavigationState(),
            ToProfiler(_host.World.LastStepStats),
            _host.World.Contacts.Select(ToContactState).ToList(),
            _host.EventLogService.Events.Select(item => new SimulationEventDto(
                item.Timestamp,
                item.Severity.ToString(),
                item.Source,
                item.Code,
                item.Message)).ToList(),
            _host.DataImportState);
    }

    private static EntityStateDto ToEntityState(Collider collider)
    {
        var body = collider.Body;
        var radius = collider is SphereCollider sphere ? sphere.Radius : 0.0f;
        var halfExtents = collider is BoxCollider box ? box.HalfExtents : body.HalfExtents;

        return new EntityStateDto(
            collider.Id,
            body.Name,
            "PhysicsBody",
            collider.Type.ToString(),
            body.Position,
            body.Orientation,
            body.PreviousPose.Position,
            body.PreviousPose.Orientation,
            body.LinearVelocity,
            body.AngularVelocity,
            halfExtents,
            radius,
            body.IsStatic,
            !body.IsSleeping,
            body.Mass,
            body.LinearDamping,
            body.AngularDamping,
            body.Material.Friction,
            body.Material.Restitution);
    }

    private RuntimeEntityStateDto ToRuntimeEntityState(SimulationEntity entity)
    {
        var physicsEntityId = entity.PhysicsBody is null
            ? null
            : _host.World.Colliders.FirstOrDefault(collider => ReferenceEquals(collider.Body, entity.PhysicsBody))?.Id;

        return new RuntimeEntityStateDto(
            entity.Id,
            entity.Name,
            entity.EntityType.ToString(),
            physicsEntityId,
            entity.Position);
    }

    private VehicleStateDto? ToVehicleState()
    {
        var vehicle = _host.Runtime.Vehicle;
        if (vehicle is null)
        {
            return null;
        }

        var physicsEntityId = vehicle.PhysicsBody is null
            ? null
            : _host.World.Colliders.FirstOrDefault(collider => ReferenceEquals(collider.Body, vehicle.PhysicsBody))?.Id;

        return new VehicleStateDto(
            vehicle.Id,
            vehicle.Name,
            physicsEntityId,
            vehicle.Position,
            vehicle.Orientation,
            vehicle.HeadingDegrees,
            vehicle.TargetSpeed,
            vehicle.TargetHeadingDegrees,
            vehicle.MotionState.ToString(),
            vehicle.CollisionCount);
    }

    private SensorStateDto? ToSensorState()
    {
        var sensor = _host.Runtime.Sensor;
        if (sensor is null)
        {
            return null;
        }

        return new SensorStateDto(
            sensor.Id,
            sensor.Name,
            sensor.State.ToString(),
            sensor.Owner.Position,
            sensor.Owner.Orientation,
            sensor.Range,
            sensor.FieldOfViewDegrees,
            sensor.Detections.Select(detection => new SensorDetectionDto(
                detection.TargetName,
                detection.TargetType.ToString(),
                detection.Distance,
                detection.RelativeBearingDegrees)).ToList());
    }

    private MissionStateDto ToMissionState()
    {
        var controller = _host.Runtime.MissionController;
        return new MissionStateDto(
            controller.State.ToString(),
            controller.Progress,
            controller.CurrentWaypoint is null ? null : ToWaypointState(controller.CurrentWaypoint),
            controller.Waypoints.Select(ToWaypointState).ToList());
    }

    private static WaypointStateDto ToWaypointState(WaypointEntity waypoint)
    {
        return new WaypointStateDto(
            waypoint.Id,
            waypoint.Name,
            waypoint.Position,
            waypoint.Order,
            waypoint.IsReached,
            waypoint.ReachRadius);
    }

    private SafetyStateDto ToSafetyState()
    {
        var result = _host.Runtime.SafetyResult;
        return new SafetyStateDto(
            result.State.ToString(),
            result.TargetName,
            result.Distance,
            result.TimeToCollisionSeconds);
    }

    private NavigationStateDto ToNavigationState()
    {
        return new NavigationStateDto(
            _host.NavigationPath.Select(point => new PathPointDto(point.X, point.Z, point.HeadingDegrees)).ToList(),
            _host.NavigationPathLength,
            _host.ExpandedNodes,
            _host.PlanningMilliseconds,
            _host.NavigationMessage,
            _host.NavigationSucceeded);
    }

    private static PhysicsProfilerDto ToProfiler(PhysicsStepStats stats)
    {
        return new PhysicsProfilerDto(
            stats.BodyCount,
            stats.ColliderCount,
            stats.PotentialPairCount,
            stats.CandidatePairCount,
            stats.ContactCount,
            stats.BroadPhaseTime.TotalMilliseconds,
            stats.NarrowPhaseTime.TotalMilliseconds,
            stats.SolverTime.TotalMilliseconds,
            stats.TotalPhysicsTime.TotalMilliseconds);
    }

    private ContactStateDto ToContactState(Contact contact)
    {
        return new ContactStateDto(
            GetColliderId(contact.BodyA),
            GetColliderId(contact.BodyB),
            contact.BodyA.Name,
            contact.BodyB.Name,
            contact.Point,
            contact.Normal,
            contact.Penetration,
            contact.RelativeVelocity,
            contact.AppliedImpulse);
    }

    private int GetColliderId(RigidBody body)
    {
        return _host.World.Colliders.FirstOrDefault(collider => ReferenceEquals(collider.Body, body))?.Id ?? 0;
    }
}
