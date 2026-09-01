using Forge3D.Contracts.Commands;
using Forge3D.Simulator.Hosting;

namespace Forge3D.Simulator.Commands;

public sealed class SimulationCommandHandler
{
    private readonly SimulationHost _host;

    public SimulationCommandHandler(SimulationHost host)
    {
        _host = host;
    }

    public void Handle(SimulationCommand command)
    {
        switch (command)
        {
            case StartSimulationCommand:
                _host.Start();
                break;
            case PauseSimulationCommand:
                _host.Pause();
                break;
            case StepSimulationCommand step:
                _host.Step(step.Steps);
                break;
            case ResetSimulationCommand:
                _host.ResetToDropScenario();
                break;
            case LoadScenarioCommand load:
                _host.LoadScenario(load.Scenario, load.StressBodyCount);
                break;
            case AddBoxCommand addBox:
                _host.AddBox(addBox.Position, addBox.HalfExtents);
                break;
            case AddSphereCommand addSphere:
                _host.AddSphere(addSphere.Position);
                break;
            case AddObstacleCommand addObstacle:
                _host.AddCustomObstacle(addObstacle.Name, addObstacle.Position, addObstacle.HalfExtents);
                break;
            case AddWaypointCommand addWaypoint:
                _host.AddWaypoint(addWaypoint.Name, addWaypoint.Position, addWaypoint.ReachRadius);
                break;
            case ClearWaypointsCommand:
                _host.ClearWaypoints();
                break;
            case SetWaypointPositionCommand waypointPosition:
                _host.SetWaypointPosition(waypointPosition.WaypointId, waypointPosition.Position);
                break;
            case DeleteWaypointCommand deleteWaypoint:
                _host.DeleteWaypoint(deleteWaypoint.WaypointId);
                break;
            case DeleteEntityCommand deleteEntity:
                _host.DeleteEntity(deleteEntity.EntityId);
                break;
            case DeleteRuntimeEntityCommand deleteRuntimeEntity:
                _host.DeleteRuntimeEntity(deleteRuntimeEntity.EntityId);
                break;
            case PasteEntityCommand pasteEntity:
                _host.PasteEntity(pasteEntity);
                break;
            case ApplyForceCommand force:
                _host.FindCollider(force.EntityId)?.Body.ApplyForce(force.Force);
                break;
            case ApplyTorqueCommand torque:
                _host.FindCollider(torque.EntityId)?.Body.ApplyTorque(torque.Torque);
                break;
            case ApplyImpulseCommand impulse:
                _host.FindCollider(impulse.EntityId)?.Body.ApplyImpulse(impulse.Impulse);
                break;
            case ApplyImpulseAtPointCommand impulseAtPoint:
                _host.FindCollider(impulseAtPoint.EntityId)?.Body.ApplyImpulseAtPoint(impulseAtPoint.Impulse, impulseAtPoint.Point);
                break;
            case SetEntityPoseCommand pose:
                if (_host.FindCollider(pose.EntityId)?.Body is { } body)
                {
                    body.Position = pose.Position;
                    body.Orientation = pose.Orientation;
                    body.LinearVelocity = System.Numerics.Vector3.Zero;
                    body.AngularVelocity = System.Numerics.Vector3.Zero;
                    _host.SnapRenderState();
                }

                break;
            case SetVehicleTargetCommand target:
                if (_host.Runtime.Vehicle is { } vehicle)
                {
                    vehicle.TargetSpeed = target.TargetSpeed;
                    vehicle.TargetHeadingDegrees = target.TargetHeadingDegrees;
                }

                break;
            case StartMissionCommand:
                _host.StartMission();
                break;
            case PauseMissionCommand:
                _host.Runtime.MissionController.Pause();
                break;
            case ResumeMissionCommand:
                _host.Runtime.MissionController.Resume();
                break;
            case AbortMissionCommand:
                _host.Runtime.MissionController.Abort();
                break;
            case ResetMissionCommand:
                _host.ResetMission();
                break;
            case StopVehicleCommand:
                _host.StopVehicle();
                break;
            case EmergencyStopCommand:
                _host.EmergencyStop();
                break;
            case ApplyFaultCommand fault:
                _host.ToggleFault(fault.Fault);
                break;
            case ClearFaultsCommand:
                _host.ClearFaults();
                break;
            case PlanPathCommand plan:
                _host.PlanPath(plan.GoalX, plan.GoalZ, plan.TargetHeading, plan.GridResolution, plan.MobilityModel, plan.Planner);
                break;
            case ClearPathCommand:
                _host.ClearPath();
                break;
            case ImportDataCommand import:
                _host.ImportData(import.FilePath);
                break;
            case SetPhysicsSettingsCommand settings:
                _host.World.Settings.FixedDeltaTime = Math.Clamp(settings.FixedDeltaTime, 1.0f / 240.0f, 1.0f / 15.0f);
                _host.World.Gravity = new System.Numerics.Vector3(_host.World.Gravity.X, settings.GravityY, _host.World.Gravity.Z);
                break;
            case SubscribeUdpSnapshotsCommand:
                break;
        }
    }
}
