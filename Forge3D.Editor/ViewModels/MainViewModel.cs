using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using System.Windows.Input;
using System.Windows.Threading;
using Forge3D.Core;
using Forge3D.Core.Collision;
using Forge3D.Core.Diagnostics;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Simulation;
using Forge3D.Core.Simulation.Events;
using Forge3D.Core.Simulation.Faults;
using Forge3D.Core.Simulation.Mission;
using Forge3D.Core.Simulation.Safety;
using Forge3D.Core.Simulation.Sensors;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Editor.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private const int MaxGraphSamples = 360;
    private const int MaxReplayFrames = 1800;

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _frameClock = Stopwatch.StartNew();
    private readonly FixedStepRunner _fixedStepRunner;
    private readonly List<ReplayFrame> _replayFrames = [];
    private readonly List<SimulationEntity> _entities = [];
    private readonly VehicleController _vehicleController = new();
    private readonly MissionController _missionController = new();
    private readonly SafetyEvaluator _safetyEvaluator = new();
    private readonly FaultManager _faultManager = new();
    private double _elapsedSimulationTime;
    private double _graphSampleAccumulator;
    private SceneObjectViewModel? _selectedObject;
    private EntityViewModel? _selectedEntity;
    private VehicleEntity? _vehicle;
    private SensorEntity? _sensor;
    private SafetyResult _safetyResult = SafetyResult.Safe;
    private Core.Simulation.Safety.SafetyState _lastSafetyState = Core.Simulation.Safety.SafetyState.Safe;
    private bool _syncingSelection;
    private bool _sensorFovDebug = true;
    private bool _isRunning;
    private bool _isRecording;
    private bool _isReplayMode;
    private bool _showVelocityDebug;
    private bool _showContactDebug;
    private bool _showNormalDebug;
    private bool _showBoundsDebug;
    private string _status = "Ready";
    private double _fps;
    private double _timelineValue;
    private double _replaySpeed = 1.0;
    private Vector3 _forceInput = new(0.0f, 50.0f, 0.0f);
    private Vector3 _torqueInput = new(0.0f, 0.0f, 5.0f);
    private Vector3 _impulseInput = new(0.0f, 7.0f, 0.0f);
    private Vector3 _impulsePointInput = new(0.5f, 0.5f, 0.0f);
    private int _boxCounter;
    private int _sphereCounter;

    public MainViewModel()
    {
        World = new PhysicsWorld();
        _fixedStepRunner = new FixedStepRunner(World);

        RunCommand = new RelayCommand(() => IsRunning = true);
        PauseCommand = new RelayCommand(() => IsRunning = false);
        StepCommand = new RelayCommand(StepOnce);
        ResetCommand = new RelayCommand(LoadDropDemo);
        AddBoxCommand = new RelayCommand(AddBox);
        AddSphereCommand = new RelayCommand(AddSphere);
        ApplyForceCommand = new RelayCommand(() => SelectedObject?.Body.ApplyForce(ForceInput));
        ApplyTorqueCommand = new RelayCommand(() => SelectedObject?.Body.ApplyTorque(TorqueInput));
        ApplyImpulseCommand = new RelayCommand(() => SelectedObject?.Body.ApplyImpulse(ImpulseInput));
        ApplyImpulseAtPointCommand = new RelayCommand(() => SelectedObject?.Body.ApplyImpulseAtPoint(ImpulseInput, ImpulsePointInput));
        ClearReplayCommand = new RelayCommand(ClearReplay);
        DropDemoCommand = new RelayCommand(LoadDropDemo);
        BounceDemoCommand = new RelayCommand(LoadBounceDemo);
        FrictionDemoCommand = new RelayCommand(LoadFrictionDemo);
        StackDemoCommand = new RelayCommand(LoadStackDemo);
        Stress100Command = new RelayCommand(() => LoadStressDemo(100));
        Stress300Command = new RelayCommand(() => LoadStressDemo(300));
        Stress500Command = new RelayCommand(() => LoadStressDemo(500));
        EngineeringScenarioCommand = new RelayCommand(LoadEngineeringScenario);
        StartMissionCommand = new RelayCommand(StartMission);
        PauseMissionCommand = new RelayCommand(() => _missionController.Pause());
        ResumeMissionCommand = new RelayCommand(() => _missionController.Resume());
        AbortMissionCommand = new RelayCommand(() => _missionController.Abort());
        ResetMissionCommand = new RelayCommand(ResetMission);
        StopVehicleCommand = new RelayCommand(() => { if (_vehicle is not null) _vehicleController.Stop(_vehicle); });
        EmergencyStopCommand = new RelayCommand(ManualEmergencyStop);
        SensorFaultCommand = new RelayCommand(() => ToggleFault(FaultType.SensorFailure));
        WheelSlipCommand = new RelayCommand(() => ToggleFault(FaultType.WheelSlip));
        MotorDegradationCommand = new RelayCommand(() => ToggleFault(FaultType.MotorDegradation));
        CommunicationLossCommand = new RelayCommand(() => ToggleFault(FaultType.CommunicationLoss));
        ClearFaultsCommand = new RelayCommand(ClearFaults);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();

        LoadDropDemo();
    }

    public PhysicsWorld World { get; }

    public ObservableCollection<SceneObjectViewModel> Objects { get; } = [];

    public ObservableCollection<EntityViewModel> Entities { get; } = [];

    public ObservableCollection<string> DetectionDetails { get; } = [];

    public ObservableCollection<string> EventLog { get; } = [];

    public ICommand RunCommand { get; }

    public ICommand PauseCommand { get; }

    public ICommand StepCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand AddBoxCommand { get; }

    public ICommand AddSphereCommand { get; }

    public ICommand ApplyForceCommand { get; }

    public ICommand ApplyTorqueCommand { get; }

    public ICommand ApplyImpulseCommand { get; }

    public ICommand ApplyImpulseAtPointCommand { get; }

    public ICommand ClearReplayCommand { get; }

    public ICommand DropDemoCommand { get; }

    public ICommand BounceDemoCommand { get; }

    public ICommand FrictionDemoCommand { get; }

    public ICommand StackDemoCommand { get; }

    public ICommand Stress100Command { get; }

    public ICommand Stress300Command { get; }

    public ICommand Stress500Command { get; }

    public ICommand EngineeringScenarioCommand { get; }

    public ICommand StartMissionCommand { get; }

    public ICommand PauseMissionCommand { get; }

    public ICommand ResumeMissionCommand { get; }

    public ICommand AbortMissionCommand { get; }

    public ICommand ResetMissionCommand { get; }

    public ICommand StopVehicleCommand { get; }

    public ICommand EmergencyStopCommand { get; }

    public ICommand SensorFaultCommand { get; }

    public ICommand WheelSlipCommand { get; }

    public ICommand MotorDegradationCommand { get; }

    public ICommand CommunicationLossCommand { get; }

    public ICommand ClearFaultsCommand { get; }

    public SceneObjectViewModel? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (SetProperty(ref _selectedObject, value) && !_syncingSelection && value is not null)
            {
                _syncingSelection = true;
                SelectedEntity = Entities.FirstOrDefault(item => ReferenceEquals(item.Entity.PhysicsBody, value.Body));
                _syncingSelection = false;
            }
        }
    }

    public EntityViewModel? SelectedEntity
    {
        get => _selectedEntity;
        set
        {
            if (SetProperty(ref _selectedEntity, value) && !_syncingSelection && value?.Entity.PhysicsBody is not null)
            {
                _syncingSelection = true;
                SelectedObject = Objects.FirstOrDefault(item => ReferenceEquals(item.Body, value.Entity.PhysicsBody));
                _syncingSelection = false;
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                Status = value ? "Running" : "Paused";
            }
        }
    }

    public bool ShowVelocityDebug
    {
        get => _showVelocityDebug;
        set => SetProperty(ref _showVelocityDebug, value);
    }

    public bool ShowContactDebug
    {
        get => _showContactDebug;
        set => SetProperty(ref _showContactDebug, value);
    }

    public bool ShowNormalDebug
    {
        get => _showNormalDebug;
        set => SetProperty(ref _showNormalDebug, value);
    }

    public bool ShowBoundsDebug
    {
        get => _showBoundsDebug;
        set => SetProperty(ref _showBoundsDebug, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public double Fps
    {
        get => _fps;
        private set => SetProperty(ref _fps, value);
    }

    public PhysicsStepStats Stats => World.LastStepStats;

    public IReadOnlyList<SimulationEntity> SimulationEntities => _entities;

    public IReadOnlyList<WaypointEntity> Waypoints => _missionController.Waypoints;

    public VehicleEntity? Vehicle => _vehicle;

    public SensorEntity? Sensor => _sensor;

    public string MissionState => _missionController.State.ToString();

    public string CurrentWaypointName => _missionController.CurrentWaypoint?.Name ?? "--";

    public float MissionProgress => _missionController.Progress * 100.0f;

    public string DistanceToWaypoint
    {
        get
        {
            if (_vehicle is null || _missionController.CurrentWaypoint is null)
            {
                return "--";
            }

            return $"{(_missionController.CurrentWaypoint.Position - _vehicle.Position).Length():F2} m";
        }
    }

    public string SafetyState => _safetyResult.State.ToString();

    public string SafetyTarget => string.IsNullOrWhiteSpace(_safetyResult.TargetName) ? "--" : _safetyResult.TargetName;

    public string SafetyDistance => _safetyResult.State == Core.Simulation.Safety.SafetyState.Safe ? "--" : $"{_safetyResult.Distance:F2} m";

    public string TimeToCollision => _safetyResult.TimeToCollisionSeconds is { } value ? $"{value:F2} s" : "--";

    public float TargetSpeed
    {
        get => _vehicle?.TargetSpeed ?? 0.0f;
        set
        {
            if (_vehicle is not null)
            {
                _vehicle.TargetSpeed = value;
                OnPropertyChanged();
            }
        }
    }

    public float TargetHeading
    {
        get => _vehicle?.TargetHeadingDegrees ?? 0.0f;
        set
        {
            if (_vehicle is not null)
            {
                _vehicle.TargetHeadingDegrees = value;
                OnPropertyChanged();
            }
        }
    }

    public bool SensorFovDebug
    {
        get => _sensorFovDebug;
        set => SetProperty(ref _sensorFovDebug, value);
    }

    public ObservableCollection<GraphSample> GraphSamples { get; } = [];

    public ObservableCollection<string> ContactDetails { get; } = [];

    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            if (SetProperty(ref _isRecording, value) && value)
            {
                IsReplayMode = false;
            }
        }
    }

    public bool IsReplayMode
    {
        get => _isReplayMode;
        set
        {
            if (SetProperty(ref _isReplayMode, value))
            {
                if (value)
                {
                    IsRunning = false;
                    IsRecording = false;
                    ApplyReplayFrame((int)Math.Round(TimelineValue));
                }
            }
        }
    }

    public double TimelineValue
    {
        get => _timelineValue;
        set
        {
            var clamped = Math.Clamp(value, 0.0, Math.Max(0, _replayFrames.Count - 1));
            if (SetProperty(ref _timelineValue, clamped) && IsReplayMode)
            {
                ApplyReplayFrame((int)Math.Round(clamped));
            }
        }
    }

    public int ReplayFrameCount => _replayFrames.Count;

    public double ReplaySpeed
    {
        get => _replaySpeed;
        set => SetProperty(ref _replaySpeed, Math.Clamp(value, 0.5, 2.0));
    }

    public Vector3 ForceInput
    {
        get => _forceInput;
        set => SetProperty(ref _forceInput, value);
    }

    public float ForceX { get => ForceInput.X; set => ForceInput = new Vector3(value, ForceInput.Y, ForceInput.Z); }

    public float ForceY { get => ForceInput.Y; set => ForceInput = new Vector3(ForceInput.X, value, ForceInput.Z); }

    public float ForceZ { get => ForceInput.Z; set => ForceInput = new Vector3(ForceInput.X, ForceInput.Y, value); }

    public Vector3 TorqueInput
    {
        get => _torqueInput;
        set => SetProperty(ref _torqueInput, value);
    }

    public float TorqueX { get => TorqueInput.X; set => TorqueInput = new Vector3(value, TorqueInput.Y, TorqueInput.Z); }

    public float TorqueY { get => TorqueInput.Y; set => TorqueInput = new Vector3(TorqueInput.X, value, TorqueInput.Z); }

    public float TorqueZ { get => TorqueInput.Z; set => TorqueInput = new Vector3(TorqueInput.X, TorqueInput.Y, value); }

    public Vector3 ImpulseInput
    {
        get => _impulseInput;
        set => SetProperty(ref _impulseInput, value);
    }

    public float ImpulseX { get => ImpulseInput.X; set => ImpulseInput = new Vector3(value, ImpulseInput.Y, ImpulseInput.Z); }

    public float ImpulseY { get => ImpulseInput.Y; set => ImpulseInput = new Vector3(ImpulseInput.X, value, ImpulseInput.Z); }

    public float ImpulseZ { get => ImpulseInput.Z; set => ImpulseInput = new Vector3(ImpulseInput.X, ImpulseInput.Y, value); }

    public Vector3 ImpulsePointInput
    {
        get => _impulsePointInput;
        set => SetProperty(ref _impulsePointInput, value);
    }

    public float PointX { get => ImpulsePointInput.X; set => ImpulsePointInput = new Vector3(value, ImpulsePointInput.Y, ImpulsePointInput.Z); }

    public float PointY { get => ImpulsePointInput.Y; set => ImpulsePointInput = new Vector3(ImpulsePointInput.X, value, ImpulsePointInput.Z); }

    public float PointZ { get => ImpulsePointInput.Z; set => ImpulsePointInput = new Vector3(ImpulsePointInput.X, ImpulsePointInput.Y, value); }

    public event EventHandler? SceneChanged;

    public event EventHandler? SimulationAdvanced;

    public event EventHandler? EngineeringSceneChanged;

    public void SelectByColliderId(int colliderId)
    {
        SelectedObject = Objects.FirstOrDefault(item => item.Id == colliderId);
        if (SelectedObject is not null)
        {
            SelectedEntity = Entities.FirstOrDefault(item => ReferenceEquals(item.Entity.PhysicsBody, SelectedObject.Body));
        }
        RefreshContactDetails();
        ResetGraphSamples();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var elapsed = _frameClock.Elapsed.TotalSeconds;
        _frameClock.Restart();

        if (elapsed > 0.0)
        {
            Fps = 1.0 / elapsed;
        }

        if (IsReplayMode)
        {
            AdvanceReplay((float)Math.Min(elapsed, 0.1));
            RefreshAfterSimulation();
        }
        else if (IsRunning)
        {
            UpdateEngineeringSystems(World.Settings.FixedDeltaTime);
            var steps = _fixedStepRunner.Step((float)Math.Min(elapsed, 0.1));
            _elapsedSimulationTime += steps * World.Settings.FixedDeltaTime;
            RefreshAfterSimulation();
        }
    }

    private void StepOnce()
    {
        IsReplayMode = false;
        World.Step(World.Settings.FixedDeltaTime);
        _elapsedSimulationTime += World.Settings.FixedDeltaTime;
        RefreshAfterSimulation();
        Status = "Stepped";
    }

    private void RefreshAfterSimulation()
    {
        SelectedObject?.Refresh();
        SelectedEntity?.Refresh();
        RefreshEngineeringTelemetry();
        RefreshContactDetails();
        SampleGraph();

        if (IsRecording && !IsReplayMode)
        {
            CaptureReplayFrame();
        }

        OnPropertyChanged(nameof(Stats));
        OnPropertyChanged(nameof(ReplayFrameCount));
        SimulationAdvanced?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateEngineeringSystems(float deltaTime)
    {
        if (_vehicle is null)
        {
            return;
        }

        _missionController.Update(_vehicle);
        _vehicleController.Update(_vehicle);

        if (_sensor is not null)
        {
            _sensor.Update(_entities, deltaTime, _elapsedSimulationTime);
            RefreshDetections();
            _safetyResult = _safetyEvaluator.Evaluate(_vehicle, _sensor);

            if (_safetyResult.State == Core.Simulation.Safety.SafetyState.Critical
                && _safetyEvaluator.AutomaticEmergencyStopEnabled
                && _vehicle.MotionState != MotionState.EmergencyStop)
            {
                _vehicleController.EmergencyStop(_vehicle);
                _missionController.EmergencyStop();
                AddEvent(EventSeverity.Critical, "Safety", "AUTO_ESTOP", $"Automatic emergency stop: {_safetyResult.TargetName} at {_safetyResult.Distance:F2} m");
            }
            else if (_safetyResult.State != _lastSafetyState && _safetyResult.State == Core.Simulation.Safety.SafetyState.Warning)
            {
                AddEvent(EventSeverity.Warning, "Safety", "WARNING_ZONE", $"{_safetyResult.TargetName} inside warning zone ({_safetyResult.Distance:F2} m)");
            }

            _lastSafetyState = _safetyResult.State;
        }

        _vehicle.CollisionCount = World.Contacts.Count(contact => ReferenceEquals(contact.BodyA, _vehicle.PhysicsBody) || ReferenceEquals(contact.BodyB, _vehicle.PhysicsBody));
    }

    private void LoadDropDemo()
    {
        ResetWorld();
        AddGround();
        AddSphere(new Vector3(-1.2f, 5.0f, 0.0f), PhysicsMaterial.Rubber);
        AddBox(new Vector3(1.1f, 4.0f, 0.0f), new Vector3(0.6f, 0.6f, 0.6f), PhysicsMaterial.Steel);
        IsRunning = false;
        Status = "Drop Demo";
        NotifySceneChanged();
    }

    private void LoadBounceDemo()
    {
        ResetWorld();
        AddGround();
        AddSphere(new Vector3(-2.0f, 5.0f, 0.0f), new PhysicsMaterial(0.4f, 0.1f), "Restitution 0.1");
        AddSphere(new Vector3(0.0f, 5.0f, 0.0f), new PhysicsMaterial(0.4f, 0.5f), "Restitution 0.5");
        AddSphere(new Vector3(2.0f, 5.0f, 0.0f), new PhysicsMaterial(0.4f, 0.9f), "Restitution 0.9");
        IsRunning = false;
        Status = "Bounce Demo";
        NotifySceneChanged();
    }

    private void LoadFrictionDemo()
    {
        ResetWorld();
        AddGround();
        AddBox(new Vector3(-2.0f, 1.2f, 0.0f), new Vector3(0.5f), PhysicsMaterial.Ice, "Ice Box").Body.LinearVelocity = new Vector3(5.0f, 0.0f, 0.0f);
        AddBox(new Vector3(0.0f, 1.2f, 0.0f), new Vector3(0.5f), PhysicsMaterial.Steel, "Steel Box").Body.LinearVelocity = new Vector3(5.0f, 0.0f, 0.0f);
        AddBox(new Vector3(2.0f, 1.2f, 0.0f), new Vector3(0.5f), PhysicsMaterial.Rubber, "Rubber Box").Body.LinearVelocity = new Vector3(5.0f, 0.0f, 0.0f);
        IsRunning = false;
        Status = "Friction Demo";
        NotifySceneChanged();
    }

    private void LoadStackDemo()
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

        IsRunning = false;
        Status = "Stack Demo";
        NotifySceneChanged();
    }

    private void LoadStressDemo(int count)
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

        IsRunning = false;
        Status = $"Stress {count}";
        NotifySceneChanged();
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
            AngularDamping = 1.2f
        };
        var vehicleCollider = new BoxCollider(vehicleBody, vehicleBody.HalfExtents, vehicleMaterial);
        var vehicleObject = Register(vehicleCollider);
        _vehicle = new VehicleEntity("vehicle-01", "Vehicle_01", vehicleBody);
        RegisterEntity(_vehicle);

        _sensor = new SensorEntity("sensor-01", "Sensor_01", _vehicle)
        {
            Range = 8.0f,
            FieldOfViewDegrees = 75.0f,
            UpdateRateHz = 12.0f
        };
        RegisterEntity(_sensor);

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

        _missionController.SetWaypoints(waypoints);
        SelectedObject = vehicleObject;
        SelectedEntity = Entities.FirstOrDefault(item => ReferenceEquals(item.Entity, _vehicle));
        Status = "Autonomous Vehicle Safety Test";
        AddEvent(EventSeverity.Info, "Scenario", "LOAD", "Engineering scenario loaded");
        NotifySceneChanged();
        EngineeringSceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddObstacle(string name, Vector3 position)
    {
        var material = new PhysicsMaterial(0.8f, 0.05f);
        var body = new RigidBody(name, position)
        {
            IsStatic = true,
            Material = material,
            HalfExtents = new Vector3(0.6f)
        };
        Register(new BoxCollider(body, body.HalfExtents, material));
        RegisterEntity(new SimulationEntity(name.ToLowerInvariant(), name, EntityType.Obstacle) { PhysicsBody = body });
    }

    private void AddBox()
    {
        AddBox(new Vector3(0.0f, 5.0f, 0.0f), new Vector3(0.5f), PhysicsMaterial.Steel);
        NotifySceneChanged();
    }

    private SceneObjectViewModel AddBox(Vector3 position, Vector3 halfExtents, PhysicsMaterial material, string? name = null)
    {
        var body = new RigidBody(name ?? $"Box_{++_boxCounter:000}", position)
        {
            Material = material,
            HalfExtents = halfExtents,
            Mass = 1.5f
        };
        var collider = new BoxCollider(body, halfExtents, material);
        return Register(collider);
    }

    private void AddSphere()
    {
        AddSphere(new Vector3(0.0f, 5.0f, 0.0f), PhysicsMaterial.Rubber);
        NotifySceneChanged();
    }

    private SceneObjectViewModel AddSphere(Vector3 position, PhysicsMaterial material, string? name = null)
    {
        var body = new RigidBody(name ?? $"Sphere_{++_sphereCounter:000}", position)
        {
            Material = material,
            Mass = 1.0f
        };
        var collider = new SphereCollider(body, 0.45f, material);
        return Register(collider);
    }

    private SceneObjectViewModel AddGround()
    {
        var material = new PhysicsMaterial(0.65f, 0.2f);
        var body = new RigidBody("Ground", Vector3.Zero)
        {
            IsStatic = true,
            Material = material
        };
        return Register(new PlaneCollider(body, Vector3.UnitY, material: material));
    }

    private SceneObjectViewModel Register(Collider collider)
    {
        World.AddCollider(collider);
        var item = new SceneObjectViewModel(collider);
        Objects.Add(item);
        SelectedObject ??= item;
        return item;
    }

    private void ResetWorld()
    {
        IsRunning = false;
        IsReplayMode = false;
        _fixedStepRunner.Reset();
        World.Clear();
        Objects.Clear();
        SelectedObject = null;
        _boxCounter = 0;
        _sphereCounter = 0;
        _elapsedSimulationTime = 0.0;
        _graphSampleAccumulator = 0.0;
        _entities.Clear();
        Entities.Clear();
        DetectionDetails.Clear();
        _vehicle = null;
        _sensor = null;
        _safetyResult = SafetyResult.Safe;
        _lastSafetyState = Core.Simulation.Safety.SafetyState.Safe;
        _missionController.SetWaypoints([]);
        ClearReplay();
        GraphSamples.Clear();
        ContactDetails.Clear();
    }

    private void NotifySceneChanged()
    {
        World.RefreshStats();
        SceneChanged?.Invoke(this, EventArgs.Empty);
        RefreshAfterSimulation();
    }

    private void RegisterEntity(SimulationEntity entity)
    {
        _entities.Add(entity);
        Entities.Add(new EntityViewModel(entity));
    }

    private void StartMission()
    {
        if (_vehicle is null)
        {
            LoadEngineeringScenario();
        }

        _missionController.Start();
        AddEvent(EventSeverity.Info, "Mission", "START", "Mission started");
        IsRunning = true;
        RefreshEngineeringTelemetry();
    }

    private void ResetMission()
    {
        _missionController.Reset();
        if (_vehicle is not null)
        {
            _vehicle.TargetSpeed = 0.0f;
            _vehicle.MotionState = MotionState.Idle;
        }

        AddEvent(EventSeverity.Info, "Mission", "RESET", "Mission reset");
        RefreshEngineeringTelemetry();
    }

    private void ManualEmergencyStop()
    {
        if (_vehicle is null)
        {
            return;
        }

        _vehicleController.EmergencyStop(_vehicle);
        _missionController.EmergencyStop();
        AddEvent(EventSeverity.Critical, "Vehicle", "MANUAL_ESTOP", "Manual emergency stop applied");
        RefreshEngineeringTelemetry();
    }

    private void ToggleFault(FaultType faultType)
    {
        if (_vehicle is null)
        {
            LoadEngineeringScenario();
        }

        if (_vehicle is null)
        {
            return;
        }

        var enabled = _faultManager.Toggle(faultType, _vehicle, _vehicleController, _sensor);
        AddEvent(enabled ? EventSeverity.Warning : EventSeverity.Info, "Fault", faultType.ToString(), enabled ? $"{faultType} injected" : $"{faultType} cleared");
        RefreshEngineeringTelemetry();
    }

    private void ClearFaults()
    {
        if (_vehicle is null)
        {
            return;
        }

        _faultManager.Clear(_vehicle, _vehicleController, _sensor);
        AddEvent(EventSeverity.Info, "Fault", "CLEAR", "All faults cleared");
        RefreshEngineeringTelemetry();
    }

    private void RefreshDetections()
    {
        DetectionDetails.Clear();

        if (_sensor is null)
        {
            return;
        }

        foreach (var detection in _sensor.Detections)
        {
            DetectionDetails.Add($"{detection.TargetName} | {detection.TargetType} | {detection.Distance:F2} m | {detection.RelativeBearingDegrees:F1} deg");
        }
    }

    private void RefreshEngineeringTelemetry()
    {
        OnPropertyChanged(nameof(MissionState));
        OnPropertyChanged(nameof(CurrentWaypointName));
        OnPropertyChanged(nameof(MissionProgress));
        OnPropertyChanged(nameof(DistanceToWaypoint));
        OnPropertyChanged(nameof(SafetyState));
        OnPropertyChanged(nameof(SafetyTarget));
        OnPropertyChanged(nameof(SafetyDistance));
        OnPropertyChanged(nameof(TimeToCollision));
        OnPropertyChanged(nameof(TargetSpeed));
        OnPropertyChanged(nameof(TargetHeading));
        EngineeringSceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddEvent(EventSeverity severity, string source, string code, string message)
    {
        EventLog.Insert(0, $"{_elapsedSimulationTime,6:F2}s  {severity,-8}  {source}  {code}  {message}");

        while (EventLog.Count > 120)
        {
            EventLog.RemoveAt(EventLog.Count - 1);
        }
    }

    private void RefreshContactDetails()
    {
        ContactDetails.Clear();

        if (SelectedObject is null)
        {
            return;
        }

        var body = SelectedObject.Body;
        var contacts = World.Contacts
            .Where(contact => ReferenceEquals(contact.BodyA, body) || ReferenceEquals(contact.BodyB, body))
            .Take(8)
            .ToList();

        for (var i = 0; i < contacts.Count; i++)
        {
            var contact = contacts[i];
            var other = ReferenceEquals(contact.BodyA, body) ? contact.BodyB : contact.BodyA;
            ContactDetails.Add(
                $"Contact #{i + 1} | Other: {other.Name} | Point: {FormatVector(contact.Point)} | Normal: {FormatVector(contact.Normal)} | Pen: {contact.Penetration:F3} | RelVel: {contact.RelativeVelocity.Length():F2} | Impulse: {contact.AppliedImpulse:F2}");
        }
    }

    private void SampleGraph()
    {
        if (SelectedObject is null)
        {
            return;
        }

        _graphSampleAccumulator += World.Settings.FixedDeltaTime;
        if (_graphSampleAccumulator < 1.0 / 20.0)
        {
            return;
        }

        _graphSampleAccumulator = 0.0;
        var body = SelectedObject.Body;
        GraphSamples.Add(new GraphSample(
            _elapsedSimulationTime,
            body.Position.Y,
            body.LinearVelocity.Length(),
            body.AngularVelocity.Length(),
            body.IsStatic ? 0.0f : 0.5f * body.Mass * body.LinearVelocity.LengthSquared()));

        while (GraphSamples.Count > MaxGraphSamples)
        {
            GraphSamples.RemoveAt(0);
        }
    }

    private void ResetGraphSamples()
    {
        GraphSamples.Clear();
        _graphSampleAccumulator = 0.0;
    }

    private void CaptureReplayFrame()
    {
        if (World.Bodies.Count == 0)
        {
            return;
        }

        _replayFrames.Add(new ReplayFrame(
            _elapsedSimulationTime,
            World.Bodies.Select(body => new BodySnapshot(
                body.Name,
                body.Position,
                body.Orientation,
                body.LinearVelocity,
                body.AngularVelocity)).ToArray()));

        while (_replayFrames.Count > MaxReplayFrames)
        {
            _replayFrames.RemoveAt(0);
        }

        TimelineValue = _replayFrames.Count - 1;
    }

    private void ApplyReplayFrame(int index)
    {
        if (index < 0 || index >= _replayFrames.Count)
        {
            return;
        }

        var frame = _replayFrames[index];
        foreach (var snapshot in frame.Bodies)
        {
            var body = World.Bodies.FirstOrDefault(candidate => candidate.Name == snapshot.Name);
            if (body is null)
            {
                continue;
            }

            body.Position = snapshot.Position;
            body.Orientation = snapshot.Orientation;
            body.LinearVelocity = snapshot.LinearVelocity;
            body.AngularVelocity = snapshot.AngularVelocity;
        }

        _elapsedSimulationTime = frame.Time;
    }

    private void AdvanceReplay(float frameDeltaTime)
    {
        if (_replayFrames.Count == 0)
        {
            return;
        }

        TimelineValue = Math.Min(_replayFrames.Count - 1, TimelineValue + (frameDeltaTime * 60.0 * ReplaySpeed));
    }

    private void ClearReplay()
    {
        _replayFrames.Clear();
        TimelineValue = 0.0;
        IsReplayMode = false;
        OnPropertyChanged(nameof(ReplayFrameCount));
    }

    private static string FormatVector(Vector3 value)
    {
        return $"{value.X:F2}, {value.Y:F2}, {value.Z:F2}";
    }

    public readonly record struct GraphSample(double Time, float PositionY, float Speed, float AngularSpeed, float KineticEnergy);

    private readonly record struct ReplayFrame(double Time, BodySnapshot[] Bodies);

    private readonly record struct BodySnapshot(
        string Name,
        Vector3 Position,
        Quaternion Orientation,
        Vector3 LinearVelocity,
        Vector3 AngularVelocity);
}
