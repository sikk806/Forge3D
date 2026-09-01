using System.Numerics;
using Forge3D.Core;
using Forge3D.Core.Constraints;
using Forge3D.Core.Collision;
using Forge3D.Core.Data;
using Forge3D.Core.Data.Capability;
using Forge3D.Core.Data.Environment;
using Forge3D.Core.Data.Parsing;
using Forge3D.Core.Data.Schema;
using Forge3D.Core.Data.Validation;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Navigation;
using Forge3D.Core.Navigation.Mobility;
using Forge3D.Core.Simulation;
using Forge3D.Core.Simulation.Events;
using Forge3D.Core.Simulation.Faults;
using Forge3D.Core.Simulation.Mission;
using Forge3D.Core.Simulation.Replay;
using Forge3D.Core.Simulation.Sensors;
using Forge3D.Core.Simulation.Telemetry;
using Forge3D.Core.Simulation.Vehicle;
using Forge3D.Contracts.Commands;
using Forge3D.Contracts.States;
using Forge3D.Simulator.State;

namespace Forge3D.Simulator.Hosting;

public sealed class SimulationHost
{
    private readonly FixedStepRunner _fixedStepRunner;
    private readonly PathPlannerSelector _pathPlannerSelector = new();
    private readonly PathSimplifier _pathSimplifier = new();
    private readonly SchemaDetector _schemaDetector = new();
    private readonly DataValidator _dataValidator = new();
    private readonly CapabilityDetector _capabilityDetector = new();
    private readonly EnvironmentBuilder _environmentBuilder = new();
    private readonly List<PathPoint> _navigationPath = [];
    private readonly List<string> _detectedFields = [];
    private readonly List<string> _capabilities = [];
    private readonly HashSet<string> _importedObstacleEntityIds = [];
    private int _boxCounter;
    private int _sphereCounter;
    private int _customWaypointCounter;
    private long _sequence;
    private string _navigationMessage = string.Empty;
    private bool _navigationSucceeded;
    private int _importedObstacleCount;
    private int _importTotalRecords;
    private int _importValidRecords;
    private int _importInvalidRecords;
    private string _importFormat = "--";
    private bool _autoReplanEnabled;
    private bool _autoReplanWarned;
    private float _autoReplanElapsed;
    private float _lastGoalX;
    private float _lastGoalZ;
    private float _lastTargetHeading;
    private float _lastGridResolution = 0.5f;
    private MobilityModelKind _lastMobilityModel = MobilityModelKind.CarLike;
    private PlannerKind _lastPlannerKind = PlannerKind.Auto;

    public SimulationHost()
    {
        World = new PhysicsWorld();
        _fixedStepRunner = new FixedStepRunner(World);
        Runtime = new SimulationRuntime(World);
        SnapshotFactory = new SimulationSnapshotFactory(this);
    }

    public PhysicsWorld World { get; }

    public SimulationRuntime Runtime { get; }

    public ReplayService ReplayService { get; } = new();

    public TelemetryRecorder TelemetryRecorder { get; } = new();

    public EventLogService EventLogService { get; } = new();

    public SimulationSnapshotFactory SnapshotFactory { get; }

    public bool IsRunning { get; private set; }

    public double SimulationTime { get; private set; }

    public float RenderInterpolationAlpha => IsRunning ? _fixedStepRunner.InterpolationAlpha : 1.0f;

    public IReadOnlyList<PathPoint> NavigationPath => _navigationPath;

    public float NavigationPathLength { get; private set; }

    public int ExpandedNodes { get; private set; }

    public double PlanningMilliseconds { get; private set; }

    public string NavigationMessage => _navigationMessage;

    public bool NavigationSucceeded => _navigationSucceeded;

    public int StoppedReplanAttempts { get; private set; }

    public int AutomaticReplanCount { get; private set; }

    public IReadOnlyList<string> DetectedFields => _detectedFields;

    public IReadOnlyList<string> Capabilities => _capabilities;

    public DataImportStateDto DataImportState => new(
        _importFormat,
        _importTotalRecords,
        _importValidRecords,
        _importInvalidRecords,
        _detectedFields.ToList(),
        _capabilities.ToList(),
        _importedObstacleCount);

    public SimulationSnapshot Snapshot()
    {
        return SnapshotFactory.Create(++_sequence);
    }

    public void Start()
    {
        if (Runtime.Vehicle is not null
            && Runtime.MissionController.State is MissionState.Ready or MissionState.Paused)
        {
            StartMission();
            return;
        }

        IsRunning = true;
    }

    public void Pause()
    {
        IsRunning = false;
    }

    public int Tick(float frameDeltaTime)
    {
        if (!IsRunning)
        {
            return 0;
        }

        return Advance(frameDeltaTime);
    }

    public int Step(int count = 1)
    {
        IsRunning = false;
        var steps = 0;
        for (var i = 0; i < Math.Max(1, count); i++)
        {
            UpdateEngineeringSystems(World.Settings.FixedDeltaTime);
            World.Step(World.Settings.FixedDeltaTime);
            SimulationTime += World.Settings.FixedDeltaTime;
            steps++;
        }

        return steps;
    }

    public void ResetToDropScenario()
    {
        ResetWorld();
        AddGround();
        AddSphere(new Vector3(-1.2f, 5.0f, 0.0f), PhysicsMaterial.Rubber);
        AddBox(new Vector3(1.1f, 4.0f, 0.0f), new Vector3(0.6f, 0.6f, 0.6f), PhysicsMaterial.Steel);
        SnapRenderState();
    }

    public void LoadScenario(SimulationScenarioKind scenario, int? stressBodyCount = null)
    {
        switch (scenario)
        {
            case SimulationScenarioKind.Bounce:
                LoadBounceScenario();
                break;
            case SimulationScenarioKind.Friction:
                LoadFrictionScenario();
                break;
            case SimulationScenarioKind.Stack:
                LoadStackScenario();
                break;
            case SimulationScenarioKind.Stress:
                LoadStressScenario(stressBodyCount ?? 100);
                break;
            case SimulationScenarioKind.Engineering:
            case SimulationScenarioKind.Customization:
                LoadEngineeringScenario();
                break;
            default:
                ResetToDropScenario();
                break;
        }
    }

    public Collider? FindCollider(int id)
    {
        return World.Colliders.FirstOrDefault(collider => collider.Id == id);
    }

    public void AddBox(Vector3 position, Vector3 halfExtents)
    {
        AddBox(position, halfExtents, PhysicsMaterial.Steel);
    }

    public void AddSphere(Vector3 position)
    {
        AddSphere(position, PhysicsMaterial.Rubber);
    }

    public void AddCustomObstacle(string name, Vector3 position, Vector3 halfExtents)
    {
        AddObstacle(UniqueName(string.IsNullOrWhiteSpace(name) ? "Custom_Obstacle" : name), position, halfExtents);
    }

    public void AddWaypoint(string name, Vector3 position, float reachRadius = 0.8f)
    {
        var order = Runtime.MissionController.Waypoints.Count + 1;
        var waypoint = new WaypointEntity(
            $"custom-wp-{++_customWaypointCounter:000}",
            string.IsNullOrWhiteSpace(name) ? $"Waypoint_{order:00}" : name,
            position,
            order,
            Math.Clamp(reachRadius, 0.2f, 5.0f));
        var nextWaypoints = Runtime.MissionController.Waypoints.Append(waypoint).ToArray();
        Runtime.Entities.Add(waypoint);
        Runtime.MissionController.SetWaypoints(nextWaypoints);
        ClearPath();
    }

    public void ClearWaypoints()
    {
        foreach (var waypoint in Runtime.Entities.Where(entity => entity.EntityType == EntityType.Waypoint).ToList())
        {
            Runtime.Entities.Remove(waypoint);
        }

        Runtime.MissionController.SetWaypoints([]);
        ClearPath();
    }

    public void SetWaypointPosition(string waypointId, Vector3 position)
    {
        var waypoint = Runtime.MissionController.Waypoints.FirstOrDefault(item => item.Id == waypointId);
        if (waypoint is null)
        {
            return;
        }

        waypoint.Position = new Vector3(position.X, 0.05f, position.Z);
        ClearPath();
    }

    public bool DeleteWaypoint(string waypointId)
    {
        var waypoint = Runtime.MissionController.Waypoints.FirstOrDefault(item => item.Id == waypointId);
        if (waypoint is null)
        {
            return false;
        }

        Runtime.Entities.Remove(waypoint);
        var remaining = Runtime.MissionController.Waypoints
            .Where(item => item.Id != waypointId)
            .ToArray();
        Runtime.MissionController.SetWaypoints(remaining);
        ClearPath();
        return true;
    }

    public bool DeleteEntity(int entityId)
    {
        var collider = FindCollider(entityId);
        if (collider is null || collider is PlaneCollider)
        {
            return false;
        }

        var body = collider.Body;
        foreach (var entity in Runtime.Entities.Where(entity => ReferenceEquals(entity.PhysicsBody, body)).ToList())
        {
            Runtime.Entities.Remove(entity);
        }

        if (ReferenceEquals(Runtime.Vehicle?.PhysicsBody, body))
        {
            if (Runtime.Sensor is not null)
            {
                Runtime.Entities.Remove(Runtime.Sensor);
            }

            Runtime.Vehicle = null;
            Runtime.Sensor = null;
            Runtime.MissionController.Abort();
            IsRunning = false;
        }

        World.RemoveBody(body);
        _importedObstacleEntityIds.RemoveWhere(id => Runtime.Entities.All(entity => entity.Id != id));
        ClearPath();
        World.RefreshStats();
        return true;
    }

    public bool DeleteRuntimeEntity(string entityId)
    {
        var entity = Runtime.Entities.FirstOrDefault(item => string.Equals(item.Id, entityId, StringComparison.OrdinalIgnoreCase));
        if (entity is null)
        {
            return false;
        }

        if (entity is WaypointEntity)
        {
            return DeleteWaypoint(entity.Id);
        }

        if (entity.PhysicsBody is not null)
        {
            var collider = World.Colliders.FirstOrDefault(item => ReferenceEquals(item.Body, entity.PhysicsBody));
            return collider is not null && DeleteEntity(collider.Id);
        }

        if (ReferenceEquals(Runtime.Sensor, entity))
        {
            Runtime.Sensor = null;
        }

        Runtime.Entities.Remove(entity);
        ClearPath();
        return true;
    }

    public bool PasteEntity(PasteEntityCommand command)
    {
        if (string.Equals(command.ColliderType, "Plane", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var material = new PhysicsMaterial(
            Math.Clamp(command.Friction, 0.0f, 10.0f),
            Math.Clamp(command.Restitution, 0.0f, 1.0f));
        var body = new RigidBody(UniqueName(string.IsNullOrWhiteSpace(command.Name) ? "Pasted_Object" : command.Name), command.Position)
        {
            Orientation = NormalizeQuaternion(command.Orientation),
            IsStatic = command.IsStatic,
            Mass = Math.Max(0.01f, command.Mass),
            Material = material,
            HalfExtents = ClampHalfExtents(command.HalfExtents),
            LinearDamping = Math.Max(0.0f, command.LinearDamping),
            AngularDamping = Math.Max(0.0f, command.AngularDamping)
        };

        Collider collider;
        if (string.Equals(command.ColliderType, "Sphere", StringComparison.OrdinalIgnoreCase))
        {
            collider = new SphereCollider(body, Math.Max(0.05f, command.Radius), material);
        }
        else
        {
            collider = new BoxCollider(body, body.HalfExtents, material);
        }

        Register(collider);
        if (body.IsStatic)
        {
            RegisterEntity(new SimulationEntity(UniqueEntityId(body.Name), body.Name, EntityType.Obstacle) { PhysicsBody = body });
        }

        ClearPath();
        SnapRenderState();
        return true;
    }

    public void StartMission()
    {
        if (Runtime.Vehicle is null)
        {
            LoadEngineeringScenario();
        }

        Runtime.MissionController.Start();
        AddEvent(EventSeverity.Info, "Mission", "START", "Mission started");
        IsRunning = true;
    }

    public void ResetMission()
    {
        Runtime.MissionController.Reset();
        if (Runtime.Vehicle is not null)
        {
            Runtime.Vehicle.TargetSpeed = 0.0f;
            Runtime.Vehicle.MotionState = MotionState.Idle;
        }

        AddEvent(EventSeverity.Info, "Mission", "RESET", "Mission reset");
    }

    public void EmergencyStop()
    {
        if (Runtime.Vehicle is null)
        {
            return;
        }

        Runtime.VehicleController.EmergencyStop(Runtime.Vehicle);
        Runtime.MissionController.EmergencyStop();
        AddEvent(EventSeverity.Critical, "Vehicle", "MANUAL_ESTOP", "Manual emergency stop applied");
    }

    public void StopVehicle()
    {
        if (Runtime.Vehicle is not null)
        {
            Runtime.VehicleController.Stop(Runtime.Vehicle);
        }
    }

    public void ToggleFault(FaultKind fault)
    {
        if (Runtime.Vehicle is null)
        {
            LoadEngineeringScenario();
        }

        if (Runtime.Vehicle is null)
        {
            return;
        }

        var faultType = fault switch
        {
            FaultKind.SensorFailure => FaultType.SensorFailure,
            FaultKind.WheelSlip => FaultType.WheelSlip,
            FaultKind.MotorDegradation => FaultType.MotorDegradation,
            _ => FaultType.CommunicationLoss
        };

        var enabled = Runtime.FaultManager.Toggle(faultType, Runtime.Vehicle, Runtime.VehicleController, Runtime.Sensor);
        AddEvent(enabled ? EventSeverity.Warning : EventSeverity.Info, "Fault", faultType.ToString(), enabled ? $"{faultType} injected" : $"{faultType} cleared");
    }

    public void ClearFaults()
    {
        if (Runtime.Vehicle is null)
        {
            return;
        }

        Runtime.FaultManager.Clear(Runtime.Vehicle, Runtime.VehicleController, Runtime.Sensor);
        AddEvent(EventSeverity.Info, "Fault", "CLEAR", "All faults cleared");
    }

    public void PlanPath(float goalX, float goalZ, float targetHeading, float gridResolution, MobilityModelKind mobilityModel, PlannerKind plannerKind)
    {
        _lastGoalX = goalX;
        _lastGoalZ = goalZ;
        _lastTargetHeading = targetHeading;
        _lastGridResolution = gridResolution;
        _lastMobilityModel = mobilityModel;
        _lastPlannerKind = plannerKind;
        _autoReplanEnabled = true;
        _autoReplanWarned = false;
        StoppedReplanAttempts = 0;
        PlanPathCore(goalX, goalZ, targetHeading, gridResolution, mobilityModel, plannerKind);
    }

    private bool PlanPathCore(float goalX, float goalZ, float targetHeading, float gridResolution, MobilityModelKind mobilityModel, PlannerKind plannerKind)
    {
        if (Runtime.Vehicle is null)
        {
            LoadEngineeringScenario();
        }

        if (Runtime.Vehicle is null)
        {
            return false;
        }

        var plannerSelection = plannerKind switch
        {
            PlannerKind.GridAStar => PlannerSelection.GridAStar,
            PlannerKind.HybridAStar => PlannerSelection.HybridAStar,
            _ => PlannerSelection.Auto
        };
        var mobility = mobilityModel == MobilityModelKind.CarLike ? MobilityModelType.CarLike : MobilityModelType.Holonomic;
        var planner = _pathPlannerSelector.Select(mobility, plannerSelection);
        var result = planner.Plan(new PathRequest
        {
            Start = new NavigationPose(Runtime.Vehicle.Position.X, Runtime.Vehicle.Position.Z, Runtime.Vehicle.HeadingDegrees),
            Goal = new NavigationPose(goalX, goalZ, targetHeading),
            GridResolution = gridResolution,
            Vehicle = new VehicleNavigationProfile
            {
                Width = Runtime.Vehicle.PhysicsBody?.HalfExtents.X * 2.0f ?? 1.1f,
                Length = Runtime.Vehicle.PhysicsBody?.HalfExtents.Z * 2.0f ?? 1.7f,
                Clearance = 0.35f,
                Wheelbase = 1.2f,
                MaxSteeringAngleDegrees = 30.0f
            },
            Obstacles = BuildNavigationObstacles()
        });

        var followablePoints = _pathSimplifier.Simplify(result.Points);
        _navigationPath.Clear();
        _navigationPath.AddRange(followablePoints);
        NavigationPathLength = CalculatePathLength(followablePoints);
        ExpandedNodes = result.ExpandedNodes;
        PlanningMilliseconds = result.PlanningTime.TotalMilliseconds;
        _navigationSucceeded = result.Succeeded;
        _navigationMessage = result.Message;

        if (result.Succeeded)
        {
            ApplyPathAsMission(followablePoints, gridResolution);
        }

        return result.Succeeded;
    }

    public void ClearPath()
    {
        _navigationPath.Clear();
        NavigationPathLength = 0.0f;
        ExpandedNodes = 0;
        PlanningMilliseconds = 0.0;
        _navigationSucceeded = false;
        _navigationMessage = string.Empty;
        _autoReplanEnabled = false;
        _autoReplanWarned = false;
        _autoReplanElapsed = 0.0f;
        StoppedReplanAttempts = 0;
        AutomaticReplanCount = 0;
    }

    private void UpdateAutomaticReplanning(float deltaTime)
    {
        if (!_autoReplanEnabled || Runtime.Vehicle is null || Runtime.MissionController.State != MissionState.Running)
        {
            return;
        }

        _autoReplanElapsed += deltaTime;
        var stopped = Runtime.Vehicle.CurrentSpeed < 0.08f && Runtime.Vehicle.TargetSpeed > 0.1f;
        if (stopped && StoppedReplanAttempts < 3)
        {
            StoppedReplanAttempts++;
            AutomaticReplanCount++;
            var succeeded = PlanPathCore(_lastGoalX, _lastGoalZ, _lastTargetHeading, _lastGridResolution, _lastMobilityModel, _lastPlannerKind);
            if (succeeded)
            {
                _autoReplanWarned = false;
                StoppedReplanAttempts = 0;
            }
            else if (StoppedReplanAttempts >= 3 && !_autoReplanWarned)
            {
                _autoReplanWarned = true;
                AddEvent(EventSeverity.Warning, "Navigation", "AUTO_REPLAN_STALLED", "Automatic replanning failed after 3 stopped attempts");
            }

            _autoReplanElapsed = 0.0f;
            return;
        }

        if (!stopped)
        {
            StoppedReplanAttempts = 0;
            _autoReplanWarned = false;
        }

        if (_autoReplanElapsed >= 3.0f)
        {
            AutomaticReplanCount++;
            PlanPathCore(_lastGoalX, _lastGoalZ, _lastTargetHeading, _lastGridResolution, _lastMobilityModel, _lastPlannerKind);
            _autoReplanElapsed = 0.0f;
        }
    }

    public void ImportData(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            _navigationMessage = "Data import failed: file not found";
            return;
        }

        var content = File.ReadAllText(filePath);
        var parser = SelectParser(filePath, content);
        if (parser is null)
        {
            _navigationMessage = "Data import failed: unsupported format";
            return;
        }

        var dataSet = parser.Parse(content);
        var mapping = _schemaDetector.RecommendMapping(dataSet);
        var validation = _dataValidator.Validate(dataSet, mapping, [ForgeField.PositionX, ForgeField.PositionY, ForgeField.PositionZ]);
        var capabilities = _capabilityDetector.Detect(mapping);
        var importedObstacles = _environmentBuilder.BuildObstacles(dataSet);

        RemoveImportedObstacles();

        _detectedFields.Clear();
        _detectedFields.AddRange(_schemaDetector.DetectFields(dataSet));
        _capabilities.Clear();
        _capabilities.AddRange(capabilities.Select(item => item.ToString()).OrderBy(item => item));
        _importFormat = dataSet.Format;
        _importTotalRecords = validation.TotalRecords;
        _importValidRecords = validation.ValidRecords;
        _importInvalidRecords = validation.InvalidRecords;
        _importedObstacleCount = importedObstacles.Count;

        foreach (var obstacle in importedObstacles)
        {
            var entity = AddObstacle(obstacle.Id, new Vector3(obstacle.X, 0.5f, obstacle.Z), new Vector3(obstacle.Width * 0.5f, 0.5f, obstacle.Depth * 0.5f));
            _importedObstacleEntityIds.Add(entity.Id);
        }
    }

    public void SnapRenderState()
    {
        _fixedStepRunner.Reset();
        World.SnapBodyPoses();
    }

    private int Advance(float frameDeltaTime)
    {
        foreach (var item in Runtime.UpdateEngineeringSystems(World.Settings.FixedDeltaTime, SimulationTime))
        {
            AddEvent(item.Severity, item.Source, item.Code, item.Message);
        }

        var steps = _fixedStepRunner.Step(Math.Min(frameDeltaTime, 0.1f));
        SimulationTime += steps * World.Settings.FixedDeltaTime;
        UpdateAutomaticReplanning(steps * World.Settings.FixedDeltaTime);
        return steps;
    }

    private void UpdateEngineeringSystems(float deltaTime)
    {
        foreach (var item in Runtime.UpdateEngineeringSystems(deltaTime, SimulationTime))
        {
            AddEvent(item.Severity, item.Source, item.Code, item.Message);
        }
    }

    private void LoadBounceScenario()
    {
        ResetWorld();
        AddGround();
        AddSphere(new Vector3(-2.0f, 5.0f, 0.0f), new PhysicsMaterial(0.4f, 0.1f), "Restitution 0.1");
        AddSphere(new Vector3(0.0f, 5.0f, 0.0f), new PhysicsMaterial(0.4f, 0.5f), "Restitution 0.5");
        AddSphere(new Vector3(2.0f, 5.0f, 0.0f), new PhysicsMaterial(0.4f, 0.9f), "Restitution 0.9");
        SnapRenderState();
    }

    private void LoadFrictionScenario()
    {
        ResetWorld();
        AddGround();
        AddBox(new Vector3(-2.0f, 1.2f, 0.0f), new Vector3(0.5f), PhysicsMaterial.Ice, "Ice Box").Body.LinearVelocity = new Vector3(5.0f, 0.0f, 0.0f);
        AddBox(new Vector3(0.0f, 1.2f, 0.0f), new Vector3(0.5f), PhysicsMaterial.Steel, "Steel Box").Body.LinearVelocity = new Vector3(5.0f, 0.0f, 0.0f);
        AddBox(new Vector3(2.0f, 1.2f, 0.0f), new Vector3(0.5f), PhysicsMaterial.Rubber, "Rubber Box").Body.LinearVelocity = new Vector3(5.0f, 0.0f, 0.0f);
        SnapRenderState();
    }

    private void LoadStackScenario()
    {
        ResetWorld();
        AddGround();
        for (var row = 0; row < 4; row++)
        {
            for (var col = 0; col <= row; col++)
            {
                AddBox(new Vector3((col - row * 0.5f) * 1.05f, 0.6f + row * 1.05f, 0.0f), new Vector3(0.5f), PhysicsMaterial.Steel);
            }
        }

        SnapRenderState();
    }

    private void LoadStressScenario(int count)
    {
        ResetWorld();
        AddGround();
        var columns = (int)Math.Ceiling(Math.Sqrt(count));
        for (var i = 0; i < count; i++)
        {
            var x = (i % columns - columns * 0.5f) * 0.75f;
            var y = 2.0f + (i / columns) * 0.8f;
            var z = ((i / 7) % 9 - 4) * 0.75f;

            if (i % 2 == 0)
            {
                AddBox(new Vector3(x, y, z), new Vector3(0.28f), PhysicsMaterial.Steel);
            }
            else
            {
                AddSphere(new Vector3(x, y, z), PhysicsMaterial.Rubber);
            }
        }

        SnapRenderState();
    }

    private void LoadEngineeringScenario()
    {
        ResetWorld();
        AddGround();
        var vehicleMaterial = new PhysicsMaterial(0.45f, 0.05f);
        var vehicleBody = new RigidBody("Vehicle_01", new Vector3(-6.0f, 0.45f, -4.0f))
        {
            Material = vehicleMaterial,
            HalfExtents = new Vector3(0.55f, 0.35f, 0.85f),
            Mass = 8.0f,
            LinearDamping = 0.6f,
            AngularDamping = 2.5f,
            Constraints = MotionConstraints.PlanarXZ
        };
        var vehicleCollider = new BoxCollider(vehicleBody, vehicleBody.HalfExtents, vehicleMaterial);
        Register(vehicleCollider);
        Runtime.Vehicle = new VehicleEntity("vehicle-01", "Vehicle_01", vehicleBody);
        RegisterEntity(Runtime.Vehicle);

        Runtime.Sensor = new SensorEntity("sensor-01", "Sensor_01", Runtime.Vehicle)
        {
            Range = 8.0f,
            FieldOfViewDegrees = 75.0f,
            UpdateRateHz = 12.0f
        };
        RegisterEntity(Runtime.Sensor);

        AddObstacle("Obstacle_01", new Vector3(-1.0f, 0.5f, -2.0f));
        AddObstacle("Obstacle_02", new Vector3(3.0f, 0.5f, 1.8f));

        var waypoints = new[]
        {
            new WaypointEntity("wp-01", "Waypoint_01", new Vector3(-3.0f, 0.05f, -3.0f), 1),
            new WaypointEntity("wp-02", "Waypoint_02", new Vector3(2.0f, 0.05f, -2.0f), 2),
            new WaypointEntity("wp-03", "Waypoint_03", new Vector3(4.5f, 0.05f, 2.5f), 3)
        };
        foreach (var waypoint in waypoints)
        {
            RegisterEntity(waypoint);
        }

        Runtime.MissionController.SetWaypoints(waypoints);
        AddEvent(EventSeverity.Info, "Scenario", "LOAD", "Customization scenario loaded");
        SnapRenderState();
    }

    private SimulationEntity AddObstacle(string name, Vector3 position)
    {
        return AddObstacle(name, position, new Vector3(0.6f));
    }

    private SimulationEntity AddObstacle(string name, Vector3 position, Vector3 halfExtents)
    {
        var material = new PhysicsMaterial(0.8f, 0.05f);
        var body = new RigidBody(name, position)
        {
            IsStatic = true,
            Material = material,
            HalfExtents = halfExtents
        };
        Register(new BoxCollider(body, body.HalfExtents, material));
        var entity = new SimulationEntity(name.ToLowerInvariant(), name, EntityType.Obstacle) { PhysicsBody = body };
        RegisterEntity(entity);
        return entity;
    }

    private string UniqueName(string baseName)
    {
        var normalized = baseName.Trim();
        if (World.Colliders.All(collider => !string.Equals(collider.Body.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return normalized;
        }

        var index = 2;
        while (World.Colliders.Any(collider => string.Equals(collider.Body.Name, $"{normalized}_{index:00}", StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }

        return $"{normalized}_{index:00}";
    }

    private string UniqueEntityId(string name)
    {
        var normalized = new string(name
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray())
            .Trim('-');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "entity";
        }

        var id = normalized;
        var index = 2;
        while (Runtime.Entities.Any(entity => string.Equals(entity.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            id = $"{normalized}-{index:00}";
            index++;
        }

        return id;
    }

    private static Vector3 ClampHalfExtents(Vector3 halfExtents)
    {
        return new Vector3(
            Math.Max(0.05f, halfExtents.X),
            Math.Max(0.05f, halfExtents.Y),
            Math.Max(0.05f, halfExtents.Z));
    }

    private static Quaternion NormalizeQuaternion(Quaternion quaternion)
    {
        return quaternion.LengthSquared() <= 0.0001f ? Quaternion.Identity : Quaternion.Normalize(quaternion);
    }

    private Collider AddBox(Vector3 position, Vector3 halfExtents, PhysicsMaterial material, string? name = null)
    {
        var body = new RigidBody(name ?? $"Box_{++_boxCounter:000}", position)
        {
            Material = material,
            HalfExtents = halfExtents,
            Mass = 1.5f
        };
        return Register(new BoxCollider(body, halfExtents, material));
    }

    private Collider AddSphere(Vector3 position, PhysicsMaterial material, string? name = null)
    {
        var body = new RigidBody(name ?? $"Sphere_{++_sphereCounter:000}", position)
        {
            Material = material,
            Mass = 1.0f
        };
        return Register(new SphereCollider(body, 0.45f, material));
    }

    private Collider AddGround()
    {
        var material = new PhysicsMaterial(0.65f, 0.2f);
        var body = new RigidBody("Ground", Vector3.Zero)
        {
            IsStatic = true,
            Material = material
        };
        return Register(new PlaneCollider(body, Vector3.UnitY, material: material));
    }

    private Collider Register(Collider collider)
    {
        World.AddCollider(collider);
        return collider;
    }

    private void RegisterEntity(SimulationEntity entity)
    {
        Runtime.Entities.Add(entity);
    }

    private void ResetWorld()
    {
        IsRunning = false;
        _fixedStepRunner.Reset();
        World.Clear();
        _boxCounter = 0;
        _sphereCounter = 0;
        _customWaypointCounter = 0;
        SimulationTime = 0.0;
        Runtime.Reset();
        ReplayService.Clear();
        TelemetryRecorder.Clear();
        EventLogService.Clear();
        _importedObstacleEntityIds.Clear();
        ClearPath();
    }

    private void RemoveImportedObstacles()
    {
        if (_importedObstacleEntityIds.Count == 0)
        {
            return;
        }

        foreach (var entity in Runtime.Entities
            .Where(entity => _importedObstacleEntityIds.Contains(entity.Id))
            .ToList())
        {
            if (entity.PhysicsBody is not null)
            {
                World.RemoveBody(entity.PhysicsBody);
            }

            Runtime.Entities.Remove(entity);
        }

        _importedObstacleEntityIds.Clear();
        World.RefreshStats();
    }

    private IReadOnlyList<NavigationObstacle> BuildNavigationObstacles()
    {
        return Runtime.Entities
            .Where(entity => entity.EntityType == EntityType.Obstacle && entity.PhysicsBody is not null)
            .Select(entity => new NavigationObstacle(
                entity.Id,
                entity.Position.X,
                entity.Position.Z,
                entity.PhysicsBody!.HalfExtents.X * 2.0f,
                entity.PhysicsBody.HalfExtents.Z * 2.0f))
            .ToList();
    }

    private void ApplyPathAsMission(IReadOnlyList<PathPoint> points, float gridResolution)
    {
        var stride = Math.Max(1, (int)MathF.Round(1.0f / Math.Max(0.25f, gridResolution)));
        var reduced = points
            .Where((_, index) => index == points.Count - 1 || index % stride == 0)
            .Skip(1)
            .Select((point, index) => new WaypointEntity($"nav-wp-{index + 1:00}", $"Path_{index + 1:00}", new Vector3(point.X, 0.05f, point.Z), index + 1, 0.75f))
            .ToArray();

        foreach (var existing in Runtime.Entities.Where(entity => entity.EntityType == EntityType.Waypoint).ToList())
        {
            Runtime.Entities.Remove(existing);
        }

        foreach (var waypoint in reduced)
        {
            RegisterEntity(waypoint);
        }

        Runtime.MissionController.SetWaypoints(reduced);
    }

    private void AddEvent(EventSeverity severity, string source, string code, string message)
    {
        EventLogService.Add(SimulationTime, severity, source, code, message);
    }

    private static float CalculatePathLength(IReadOnlyList<PathPoint> points)
    {
        var length = 0.0f;
        for (var i = 1; i < points.Count; i++)
        {
            var dx = points[i].X - points[i - 1].X;
            var dz = points[i].Z - points[i - 1].Z;
            length += MathF.Sqrt((dx * dx) + (dz * dz));
        }

        return length;
    }

    private static IDataParser? SelectParser(string fileName, string content)
    {
        IDataParser[] parsers = [new CsvDataParser(), new JsonDataParser()];
        return parsers.FirstOrDefault(parser => parser.CanParse(fileName, content));
    }
}
