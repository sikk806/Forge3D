using System.Numerics;
using System.Text.Json.Serialization;

namespace Forge3D.Contracts.Commands;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(StartSimulationCommand), "start-simulation")]
[JsonDerivedType(typeof(PauseSimulationCommand), "pause-simulation")]
[JsonDerivedType(typeof(StepSimulationCommand), "step-simulation")]
[JsonDerivedType(typeof(ResetSimulationCommand), "reset-simulation")]
[JsonDerivedType(typeof(LoadScenarioCommand), "load-scenario")]
[JsonDerivedType(typeof(AddBoxCommand), "add-box")]
[JsonDerivedType(typeof(AddSphereCommand), "add-sphere")]
[JsonDerivedType(typeof(AddObstacleCommand), "add-obstacle")]
[JsonDerivedType(typeof(AddWaypointCommand), "add-waypoint")]
[JsonDerivedType(typeof(ClearWaypointsCommand), "clear-waypoints")]
[JsonDerivedType(typeof(SetWaypointPositionCommand), "set-waypoint-position")]
[JsonDerivedType(typeof(DeleteWaypointCommand), "delete-waypoint")]
[JsonDerivedType(typeof(DeleteEntityCommand), "delete-entity")]
[JsonDerivedType(typeof(DeleteRuntimeEntityCommand), "delete-runtime-entity")]
[JsonDerivedType(typeof(PasteEntityCommand), "paste-entity")]
[JsonDerivedType(typeof(ApplyForceCommand), "apply-force")]
[JsonDerivedType(typeof(ApplyTorqueCommand), "apply-torque")]
[JsonDerivedType(typeof(ApplyImpulseCommand), "apply-impulse")]
[JsonDerivedType(typeof(ApplyImpulseAtPointCommand), "apply-impulse-at-point")]
[JsonDerivedType(typeof(SetEntityPoseCommand), "set-entity-pose")]
[JsonDerivedType(typeof(SetVehicleTargetCommand), "set-vehicle-target")]
[JsonDerivedType(typeof(StartMissionCommand), "start-mission")]
[JsonDerivedType(typeof(PauseMissionCommand), "pause-mission")]
[JsonDerivedType(typeof(ResumeMissionCommand), "resume-mission")]
[JsonDerivedType(typeof(AbortMissionCommand), "abort-mission")]
[JsonDerivedType(typeof(ResetMissionCommand), "reset-mission")]
[JsonDerivedType(typeof(StopVehicleCommand), "stop-vehicle")]
[JsonDerivedType(typeof(EmergencyStopCommand), "emergency-stop")]
[JsonDerivedType(typeof(ApplyFaultCommand), "apply-fault")]
[JsonDerivedType(typeof(ClearFaultsCommand), "clear-faults")]
[JsonDerivedType(typeof(PlanPathCommand), "plan-path")]
[JsonDerivedType(typeof(ClearPathCommand), "clear-path")]
[JsonDerivedType(typeof(ImportDataCommand), "import-data")]
[JsonDerivedType(typeof(SetPhysicsSettingsCommand), "set-physics-settings")]
[JsonDerivedType(typeof(SubscribeUdpSnapshotsCommand), "subscribe-udp-snapshots")]
public abstract record SimulationCommand;

public sealed record StartSimulationCommand : SimulationCommand;

public sealed record PauseSimulationCommand : SimulationCommand;

public sealed record StepSimulationCommand(int Steps = 1) : SimulationCommand;

public sealed record ResetSimulationCommand : SimulationCommand;

public sealed record LoadScenarioCommand(SimulationScenarioKind Scenario, int? StressBodyCount = null) : SimulationCommand;

public sealed record AddBoxCommand(Vector3 Position, Vector3 HalfExtents) : SimulationCommand;

public sealed record AddSphereCommand(Vector3 Position) : SimulationCommand;

public sealed record AddObstacleCommand(string Name, Vector3 Position, Vector3 HalfExtents) : SimulationCommand;

public sealed record AddWaypointCommand(string Name, Vector3 Position, float ReachRadius = 0.8f) : SimulationCommand;

public sealed record ClearWaypointsCommand : SimulationCommand;

public sealed record SetWaypointPositionCommand(string WaypointId, Vector3 Position) : SimulationCommand;

public sealed record DeleteWaypointCommand(string WaypointId) : SimulationCommand;

public sealed record DeleteEntityCommand(int EntityId) : SimulationCommand;

public sealed record DeleteRuntimeEntityCommand(string EntityId) : SimulationCommand;

public sealed record PasteEntityCommand(
    string Name,
    string ColliderType,
    Vector3 Position,
    Quaternion Orientation,
    Vector3 HalfExtents,
    float Radius,
    bool IsStatic,
    float Mass,
    float Friction,
    float Restitution,
    float LinearDamping,
    float AngularDamping) : SimulationCommand;

public sealed record ApplyForceCommand(int EntityId, Vector3 Force) : SimulationCommand;

public sealed record ApplyTorqueCommand(int EntityId, Vector3 Torque) : SimulationCommand;

public sealed record ApplyImpulseCommand(int EntityId, Vector3 Impulse) : SimulationCommand;

public sealed record ApplyImpulseAtPointCommand(int EntityId, Vector3 Impulse, Vector3 Point) : SimulationCommand;

public sealed record SetEntityPoseCommand(int EntityId, Vector3 Position, Quaternion Orientation) : SimulationCommand;

public sealed record SetVehicleTargetCommand(float TargetSpeed, float TargetHeadingDegrees) : SimulationCommand;

public sealed record StartMissionCommand : SimulationCommand;

public sealed record PauseMissionCommand : SimulationCommand;

public sealed record ResumeMissionCommand : SimulationCommand;

public sealed record AbortMissionCommand : SimulationCommand;

public sealed record ResetMissionCommand : SimulationCommand;

public sealed record StopVehicleCommand : SimulationCommand;

public sealed record EmergencyStopCommand : SimulationCommand;

public sealed record ApplyFaultCommand(FaultKind Fault) : SimulationCommand;

public sealed record ClearFaultsCommand : SimulationCommand;

public sealed record PlanPathCommand(
    float GoalX,
    float GoalZ,
    float TargetHeading,
    float GridResolution,
    MobilityModelKind MobilityModel,
    PlannerKind Planner) : SimulationCommand;

public sealed record ClearPathCommand : SimulationCommand;

public sealed record ImportDataCommand(string FilePath) : SimulationCommand;

public sealed record SetPhysicsSettingsCommand(float FixedDeltaTime, float GravityY) : SimulationCommand;

public sealed record SubscribeUdpSnapshotsCommand(int Port) : SimulationCommand;
