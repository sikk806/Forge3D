using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Windows.Input;
using System.Windows.Threading;
using Forge3D.Core;
using Forge3D.Core.Constraints;
using Forge3D.Core.Collision;
using Forge3D.Core.Data;
using Forge3D.Core.Data.Capability;
using Forge3D.Core.Data.Environment;
using Forge3D.Core.Data.Parsing;
using Forge3D.Core.Data.Schema;
using Forge3D.Core.Data.Validation;
using Forge3D.Core.Diagnostics;
using Forge3D.Core.Dynamics;
using Forge3D.Core.Navigation;
using Forge3D.Core.Navigation.Mobility;
using Forge3D.Core.Simulation;
using Forge3D.Core.Simulation.Events;
using Forge3D.Core.Simulation.Faults;
using Forge3D.Core.Simulation.Mission;
using Forge3D.Core.Simulation.Replay;
using Forge3D.Core.Simulation.Safety;
using Forge3D.Core.Simulation.Sensors;
using Forge3D.Core.Simulation.Telemetry;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Editor.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _frameClock = Stopwatch.StartNew();
    private readonly FixedStepRunner _fixedStepRunner;
    private readonly SimulationRuntime _runtime;
    private readonly ReplayService _replayService = new();
    private readonly TelemetryRecorder _telemetryRecorder = new();
    private readonly EventLogService _eventLogService = new();
    private readonly PathPlannerSelector _pathPlannerSelector = new();
    private readonly SchemaDetector _schemaDetector = new();
    private readonly DataValidator _dataValidator = new();
    private readonly CapabilityDetector _capabilityDetector = new();
    private readonly EnvironmentBuilder _environmentBuilder = new();
    private double _elapsedSimulationTime;
    private SceneObjectViewModel? _selectedObject;
    private EntityViewModel? _selectedEntity;
    private bool _syncingSelection;
    private bool _sensorFovDebug = true;
    private bool _isRunning;
    private bool _isRecording;
    private bool _isReplayMode;
    private bool _showVelocityDebug;
    private bool _showContactDebug;
    private bool _showNormalDebug;
    private bool _showBoundsDebug;
    private bool _showNavigationPathDebug = true;
    private bool _isHierarchyVisible = true;
    private bool _isInspectorVisible = true;
    private bool _isTelemetryVisible = true;
    private bool _isGraphVisible = true;
    private bool _isEventPanelVisible = true;
    private bool _isReplayVisible = true;
    private bool _isProfilerVisible = true;
    private bool _isSimulationToolbarVisible = true;
    private bool _isDebugToolbarVisible = true;
    private string _status = "준비";
    private string _selectedWorkspace = "시스템 시뮬레이션";
    private string _selectedLanguage = "한국어";
    private string _selectedMobilityModel = "차량형";
    private string _selectedPlanner = "자동";
    private string _dataImportPath = string.Empty;
    private string _dataFormat = "--";
    private string _validationSummary = "--";
    private string _capabilitySummary = "--";
    private float _navigationStartX = -6.0f;
    private float _navigationStartZ = -4.0f;
    private float _navigationGoalX = 4.5f;
    private float _navigationGoalZ = 2.5f;
    private float _gridResolution = 0.5f;
    private float _pathLength;
    private int _expandedNodes;
    private double _planningMilliseconds;
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
        _runtime = new SimulationRuntime(World);

        RunCommand = new RelayCommand(RunSimulation);
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
        ExitCommand = new RelayCommand(() => Environment.Exit(0));
        DropDemoCommand = new RelayCommand(LoadDropDemo);
        BounceDemoCommand = new RelayCommand(LoadBounceDemo);
        FrictionDemoCommand = new RelayCommand(LoadFrictionDemo);
        StackDemoCommand = new RelayCommand(LoadStackDemo);
        Stress100Command = new RelayCommand(() => LoadStressDemo(100));
        Stress300Command = new RelayCommand(() => LoadStressDemo(300));
        Stress500Command = new RelayCommand(() => LoadStressDemo(500));
        EngineeringScenarioCommand = new RelayCommand(LoadEngineeringScenario);
        StartMissionCommand = new RelayCommand(StartMission);
        PauseMissionCommand = new RelayCommand(() => _runtime.MissionController.Pause());
        ResumeMissionCommand = new RelayCommand(() => _runtime.MissionController.Resume());
        AbortMissionCommand = new RelayCommand(() => _runtime.MissionController.Abort());
        ResetMissionCommand = new RelayCommand(ResetMission);
        StopVehicleCommand = new RelayCommand(() => { if (_runtime.Vehicle is not null) _runtime.VehicleController.Stop(_runtime.Vehicle); });
        EmergencyStopCommand = new RelayCommand(ManualEmergencyStop);
        SensorFaultCommand = new RelayCommand(() => ToggleFault(FaultType.SensorFailure));
        WheelSlipCommand = new RelayCommand(() => ToggleFault(FaultType.WheelSlip));
        MotorDegradationCommand = new RelayCommand(() => ToggleFault(FaultType.MotorDegradation));
        CommunicationLossCommand = new RelayCommand(() => ToggleFault(FaultType.CommunicationLoss));
        ClearFaultsCommand = new RelayCommand(ClearFaults);
        PlanPathCommand = new RelayCommand(PlanPath);
        ClearPathCommand = new RelayCommand(ClearPath);
        ImportDataCommand = new RelayCommand(ImportData);
        SetKoreanCommand = new RelayCommand(() => SelectedLanguage = "한국어");
        SetEnglishCommand = new RelayCommand(() => SelectedLanguage = "English");
        RefreshLocalizedOptions();

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

    public ObservableCollection<PathPoint> NavigationPath { get; } = [];

    public ObservableCollection<string> DetectedFields { get; } = [];

    public ObservableCollection<string> Workspaces { get; } = [];

    public ObservableCollection<string> Languages { get; } = ["한국어", "English"];

    public ObservableCollection<string> MobilityModels { get; } = [];

    public ObservableCollection<string> Planners { get; } = [];

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

    public ICommand ExitCommand { get; }

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

    public ICommand PlanPathCommand { get; }

    public ICommand ClearPathCommand { get; }

    public ICommand ImportDataCommand { get; }

    public ICommand SetKoreanCommand { get; }

    public ICommand SetEnglishCommand { get; }

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
                NotifySelectedObjectDisplayChanged();
            }
            else
            {
                NotifySelectedObjectDisplayChanged();
            }
        }
    }

    public string SelectedObjectDisplayName => SelectedObject?.Name ?? LabelNoSelection;

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
                Status = value ? Text("실행 중", "Running") : Text("일시정지", "Paused");
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

    public bool ShowNavigationPathDebug
    {
        get => _showNavigationPathDebug;
        set => SetProperty(ref _showNavigationPathDebug, value);
    }

    public bool IsHierarchyVisible
    {
        get => _isHierarchyVisible;
        set => SetProperty(ref _isHierarchyVisible, value);
    }

    public bool IsInspectorVisible
    {
        get => _isInspectorVisible;
        set => SetProperty(ref _isInspectorVisible, value);
    }

    public bool IsTelemetryVisible
    {
        get => _isTelemetryVisible;
        set => SetProperty(ref _isTelemetryVisible, value);
    }

    public bool IsGraphVisible
    {
        get => _isGraphVisible;
        set => SetProperty(ref _isGraphVisible, value);
    }

    public bool IsEventPanelVisible
    {
        get => _isEventPanelVisible;
        set => SetProperty(ref _isEventPanelVisible, value);
    }

    public bool IsReplayVisible
    {
        get => _isReplayVisible;
        set => SetProperty(ref _isReplayVisible, value);
    }

    public bool IsProfilerVisible
    {
        get => _isProfilerVisible;
        set => SetProperty(ref _isProfilerVisible, value);
    }

    public bool IsSimulationToolbarVisible
    {
        get => _isSimulationToolbarVisible;
        set => SetProperty(ref _isSimulationToolbarVisible, value);
    }

    public bool IsDebugToolbarVisible
    {
        get => _isDebugToolbarVisible;
        set => SetProperty(ref _isDebugToolbarVisible, value);
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusDisplay));
            }
        }
    }

    public string SelectedWorkspace
    {
        get => _selectedWorkspace;
        set
        {
            if (SetProperty(ref _selectedWorkspace, value))
            {
                ApplyWorkspaceVisibility();
            }
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                OnLocalizationChanged();
            }
        }
    }

    public bool IsKorean => SelectedLanguage == "한국어";

    public bool IsEnglish => SelectedLanguage == "English";

    public string MenuFile => Text("파일", "File");

    public string MenuEdit => Text("편집", "Edit");

    public string MenuView => Text("보기", "View");

    public string MenuSimulation => Text("시뮬레이션", "Simulation");

    public string MenuCreate => Text("생성", "Create");

    public string MenuDebug => Text("디버그", "Debug");

    public string MenuTools => Text("도구", "Tools");

    public string MenuHelp => Text("도움말", "Help");

    public string MenuImportData => Text("데이터 가져오기", "Import Data");

    public string MenuExit => Text("종료", "Exit");

    public string MenuLanguage => Text("언어", "Language");

    public string MenuSettings => Text("설정", "Settings");

    public string ButtonClose => Text("닫기", "Close");

    public string HeaderGeneral => Text("일반", "General");

    public string HeaderSettingsSimulation => Text("시뮬레이션", "Simulation");

    public string LabelLanguage => Text("언어", "Language");

    public string LabelDefaultWorkspace => Text("기본 작업공간", "Default Workspace");

    public string LabelFixedTimestep => Text("고정 시간 간격", "Fixed Timestep");

    public string LabelGravityY => Text("중력 Y", "Gravity Y");

    public string LabelDefaultMobility => Text("기본 이동 모델", "Default Mobility");

    public string LabelDefaultPlanner => Text("기본 플래너", "Default Planner");

    public string LabelWorkspace => Text("작업공간", "Workspace");

    public string LabelSimulation => Text("시뮬레이션", "Simulation");

    public string LabelCreate => Text("생성", "Create");

    public string LabelDebug => Text("디버그", "Debug");

    public string LabelNavigation => Text("내비게이션", "Navigation");

    public string LabelScenes => Text("장면", "Scenes");

    public string LabelStress => Text("스트레스", "Stress");

    public string ButtonRun => Text("실행 (F5)", "Run (F5)");

    public string ButtonPause => Text("일시정지 (F6)", "Pause (F6)");

    public string ButtonMissionPause => Text("미션 일시정지", "Pause");

    public string ButtonStep => Text("한 단계 (F10)", "Step (F10)");

    public string ButtonReset => Text("초기화 (Ctrl+R)", "Reset (Ctrl+R)");

    public string ButtonMissionReset => Text("미션 초기화", "Reset");

    public string ButtonAddBox => Text("박스 추가", "Add Box");

    public string ButtonAddSphere => Text("구 추가", "Add Sphere");

    public string ButtonPlanPath => Text("경로 계획 (Ctrl+P)", "Plan Path (Ctrl+P)");

    public string ButtonClearPath => Text("경로 지우기", "Clear Path");

    public string ButtonClear => Text("지우기", "Clear");

    public string ButtonStartMission => Text("미션 시작", "Start Mission");

    public string ButtonStop => Text("정지", "Stop");

    public string ButtonEmergency => Text("비상정지", "Emergency");

    public string ButtonResume => Text("재개", "Resume");

    public string ButtonAbort => Text("중단", "Abort");

    public string ButtonApplyForce => Text("힘 적용", "Apply Force");

    public string ButtonApplyTorque => Text("토크 적용", "Apply Torque");

    public string ButtonApplyImpulse => Text("충격량 적용", "Apply Impulse");

    public string ButtonApplyImpulseAtPoint => Text("지점 충격량 적용", "Apply Impulse At Point");

    public string ButtonImportData => Text("데이터 가져오기", "Import Data");

    public string HeaderSceneHierarchy => Text("장면 계층", "Scene Hierarchy");

    public string HeaderPhysicsBodies => Text("물리 바디", "Physics Bodies");

    public string HeaderSimulationEntities => Text("시뮬레이션 엔티티", "Simulation Entities");

    public string HeaderInspector => Text("인스펙터", "Inspector");

    public string HeaderTransform => Text("변환", "Transform");

    public string HeaderRotation => Text("회전", "Rotation");

    public string HeaderPhysics => Text("물리", "Physics");

    public string HeaderVelocity => Text("속도", "Velocity");

    public string HeaderDiagnostics => Text("진단", "Diagnostics");

    public string HeaderActions => Text("동작", "Actions");

    public string HeaderContacts => Text("접촉", "Contacts");

    public string HeaderVehicleControl => Text("차량 제어", "Vehicle Control");

    public string HeaderTelemetrySafety => Text("텔레메트리 / 안전", "Telemetry / Safety");

    public string HeaderDetections => Text("감지 결과", "Detections");

    public string HeaderFaultInjection => Text("고장 주입", "Fault Injection");

    public string HeaderDataAnalysis => Text("데이터 분석", "Data Analysis");

    public string HeaderEventsAlarms => Text("이벤트 / 알람", "Events / Alarms");

    public string HeaderReplayTimeline => Text("리플레이 / 타임라인", "Replay / Timeline");

    public string LabelNoSelection => Text("선택 없음", "No selection");

    public string LabelTargetSpeed => Text("목표 속도", "Target Speed");

    public string LabelTargetHeading => Text("목표 방향", "Target Heading");

    public string LabelStartXZ => Text("시작 X / Z", "Start X / Z");

    public string LabelGoalXZ => Text("목표 X / Z", "Goal X / Z");

    public string LabelGridResolution => Text("그리드 해상도", "Grid Resolution");

    public string LabelImportPath => Text("가져올 파일 경로", "Import Path");

    public string LabelDetectedFields => Text("감지된 필드", "Detected Fields");

    public string LabelForce => Text("힘", "Force");

    public string LabelTorque => Text("토크", "Torque");

    public string LabelImpulse => Text("충격량", "Impulse");

    public string LabelApplicationPoint => Text("적용 지점", "Application Point");

    public string LabelSpeed => Text("속도", "Speed");

    public string LabelRecord => Text("기록", "Record");

    public string LabelReplay => Text("재생", "Replay");

    public string MenuSceneHierarchy => Text("장면 계층", "Scene Hierarchy");

    public string MenuInspector => Text("인스펙터", "Inspector");

    public string MenuTelemetry => Text("텔레메트리", "Telemetry");

    public string MenuGraph => Text("그래프", "Graph");

    public string MenuEvents => Text("이벤트", "Events");

    public string MenuReplay => Text("리플레이", "Replay");

    public string MenuProfiler => Text("프로파일러", "Profiler");

    public string MenuSimulationToolbar => Text("시뮬레이션 툴바", "Simulation Toolbar");

    public string MenuDebugToolbar => Text("디버그 툴바", "Debug Toolbar");

    public string MenuBox => Text("박스", "Box");

    public string MenuSphere => Text("구", "Sphere");

    public string DebugVelocity => Text("속도", "Velocity");

    public string DebugContact => Text("접촉", "Contact");

    public string DebugNormal => Text("법선", "Normal");

    public string DebugBounds => Text("경계", "Bounds");

    public string DebugSensorFov => Text("센서 시야", "Sensor FOV");

    public string DebugNavigationPath => Text("경로", "Navigation Path");

    public string ButtonDrop => Text("낙하", "Drop");

    public string ButtonBounce => Text("반발", "Bounce");

    public string ButtonFriction => Text("마찰", "Friction");

    public string ButtonStack => Text("쌓기", "Stack");

    public string ButtonEngineering => Text("엔지니어링", "Engineering");

    public string LabelStatic => Text("고정", "Static");

    public string LabelMass => Text("질량", "Mass");

    public string LabelFriction => Text("마찰", "Friction");

    public string LabelRestitution => Text("반발", "Restitution");

    public string LabelLinearDamping => Text("선형 감쇠", "Linear Damping");

    public string LabelAngularDamping => Text("각 감쇠", "Angular Damping");

    public string ButtonSensorFailure => Text("센서 고장", "Sensor Failure");

    public string ButtonWheelSlip => Text("휠 미끄럼", "Wheel Slip");

    public string ButtonMotorDegrade => Text("모터 저하", "Motor Degrade");

    public string ButtonCommLoss => Text("통신 두절", "Comm Loss");

    public string ButtonClearFaults => Text("고장 해제", "Clear Faults");

    public string StatusDisplay => Text($"상태: {Status}", $"Status: {Status}");

    public string FpsDisplay => Text($"FPS: {Fps:F0}", $"FPS: {Fps:F0}");

    public string BodyCountDisplay => Text($"바디: {Stats.BodyCount}", $"Bodies: {Stats.BodyCount}");

    public string ColliderCountDisplay => Text($"콜라이더: {Stats.ColliderCount}", $"Colliders: {Stats.ColliderCount}");

    public string PotentialPairCountDisplay => Text($"전체 쌍: {Stats.PotentialPairCount}", $"Total Pairs: {Stats.PotentialPairCount}");

    public string CandidatePairCountDisplay => Text($"후보: {Stats.CandidatePairCount}", $"Candidates: {Stats.CandidatePairCount}");

    public string ContactCountDisplay => Text($"접촉: {Stats.ContactCount}", $"Contacts: {Stats.ContactCount}");

    public string BroadPhaseDisplay => Text($"브로드: {Stats.BroadPhaseTime.TotalMilliseconds:F3} ms", $"Broad: {Stats.BroadPhaseTime.TotalMilliseconds:F3} ms");

    public string NarrowPhaseDisplay => Text($"내로우: {Stats.NarrowPhaseTime.TotalMilliseconds:F3} ms", $"Narrow: {Stats.NarrowPhaseTime.TotalMilliseconds:F3} ms");

    public string SolverDisplay => Text($"솔버: {Stats.SolverTime.TotalMilliseconds:F3} ms", $"Solver: {Stats.SolverTime.TotalMilliseconds:F3} ms");

    public string PhysicsTimeDisplay => Text($"물리: {Stats.TotalPhysicsTime.TotalMilliseconds:F3} ms", $"Physics: {Stats.TotalPhysicsTime.TotalMilliseconds:F3} ms");

    public string CenterOfMassDisplay => Text($"중심: {SelectedObject?.CenterOfMass ?? "--"}", $"Center: {SelectedObject?.CenterOfMass ?? "--"}");

    public string SleepStateDisplay => Text($"상태: {SelectedObject?.SleepState ?? "--"}", $"State: {SelectedObject?.SleepState ?? "--"}");

    public string CurrentSpeedDisplay => Text($"속도: {SelectedObject?.CurrentSpeed ?? 0.0f:F2}", $"Speed: {SelectedObject?.CurrentSpeed ?? 0.0f:F2}");

    public string AngularSpeedDisplay => Text($"각속도: {SelectedObject?.AngularSpeed ?? 0.0f:F2}", $"Angular: {SelectedObject?.AngularSpeed ?? 0.0f:F2}");

    public string KineticEnergyDisplay => Text($"에너지: {SelectedObject?.KineticEnergy ?? 0.0f:F2}", $"Energy: {SelectedObject?.KineticEnergy ?? 0.0f:F2}");

    public string ExpandedNodesDisplay => Text($"확장 노드: {ExpandedNodes}", $"Expanded: {ExpandedNodes}");

    public string PathLengthDisplay => Text($"경로 길이: {PathLength:F2} m", $"Length: {PathLength:F2} m");

    public string PlanningTimeDisplay => Text($"계획 시간: {PlanningMilliseconds:F2} ms", $"Planning: {PlanningMilliseconds:F2} ms");

    public string MissionStateDisplay => Text($"미션: {MissionState}", $"Mission: {MissionState}");

    public string CurrentWaypointDisplay => Text($"웨이포인트: {CurrentWaypointName}", $"Waypoint: {CurrentWaypointName}");

    public string MissionProgressDisplay => Text($"진행률: {MissionProgress:F0}%", $"Progress: {MissionProgress:F0}%");

    public string DistanceToWaypointDisplay => Text($"거리: {DistanceToWaypoint}", $"Distance: {DistanceToWaypoint}");

    public string SafetyStateDisplay => Text($"안전: {SafetyState}", $"Safety: {SafetyState}");

    public string SafetyTargetDisplay => Text($"대상: {SafetyTarget}", $"Target: {SafetyTarget}");

    public string SafetyDistanceDisplay => Text($"장애물 거리: {SafetyDistance}", $"Obstacle Distance: {SafetyDistance}");

    public string TimeToCollisionDisplay => Text($"TTC: {TimeToCollision}", $"TTC: {TimeToCollision}");

    public string DataFormatDisplay => Text($"형식: {DataFormat}", $"Format: {DataFormat}");

    public string ValidationSummaryDisplay => Text($"검증: {ValidationSummary}", $"Validation: {ValidationSummary}");

    public string CapabilitySummaryDisplay => Text($"기능: {CapabilitySummary}", $"Capabilities: {CapabilitySummary}");

    public double Fps
    {
        get => _fps;
        private set
        {
            if (SetProperty(ref _fps, value))
            {
                OnPropertyChanged(nameof(FpsDisplay));
            }
        }
    }

    public PhysicsStepStats Stats => World.LastStepStats;

    public IReadOnlyList<SimulationEntity> SimulationEntities => _runtime.Entities.ToList();

    public IReadOnlyList<WaypointEntity> Waypoints => _runtime.MissionController.Waypoints;

    public VehicleEntity? Vehicle => _runtime.Vehicle;

    public SensorEntity? Sensor => _runtime.Sensor;

    public string MissionState => _runtime.MissionController.State.ToString();

    public string CurrentWaypointName => _runtime.MissionController.CurrentWaypoint?.Name ?? "--";

    public float MissionProgress => _runtime.MissionController.Progress * 100.0f;

    public string DistanceToWaypoint
    {
        get
        {
            if (_runtime.Vehicle is null || _runtime.MissionController.CurrentWaypoint is null)
            {
                return "--";
            }

            return $"{(_runtime.MissionController.CurrentWaypoint.Position - _runtime.Vehicle.Position).Length():F2} m";
        }
    }

    public string SafetyState => _runtime.SafetyResult.State.ToString();

    public string SafetyTarget => string.IsNullOrWhiteSpace(_runtime.SafetyResult.TargetName) ? "--" : _runtime.SafetyResult.TargetName;

    public string SafetyDistance => _runtime.SafetyResult.State == Core.Simulation.Safety.SafetyState.Safe ? "--" : $"{_runtime.SafetyResult.Distance:F2} m";

    public string TimeToCollision => _runtime.SafetyResult.TimeToCollisionSeconds is { } value ? $"{value:F2} s" : "--";

    public float TargetSpeed
    {
        get => _runtime.Vehicle?.TargetSpeed ?? 0.0f;
        set
        {
            if (_runtime.Vehicle is not null)
            {
                _runtime.Vehicle.TargetSpeed = value;
                OnPropertyChanged();
            }
        }
    }

    public float TargetHeading
    {
        get => _runtime.Vehicle?.TargetHeadingDegrees ?? 0.0f;
        set
        {
            if (_runtime.Vehicle is not null)
            {
                _runtime.Vehicle.TargetHeadingDegrees = value;
                OnPropertyChanged();
            }
        }
    }

    public bool SensorFovDebug
    {
        get => _sensorFovDebug;
        set => SetProperty(ref _sensorFovDebug, value);
    }

    public ObservableCollection<TelemetrySample> GraphSamples { get; } = [];

    public ObservableCollection<string> ContactDetails { get; } = [];

    public string SelectedMobilityModel
    {
        get => _selectedMobilityModel;
        set => SetProperty(ref _selectedMobilityModel, value);
    }

    public string SelectedPlanner
    {
        get => _selectedPlanner;
        set => SetProperty(ref _selectedPlanner, value);
    }

    public float NavigationStartX
    {
        get => _navigationStartX;
        set => SetProperty(ref _navigationStartX, value);
    }

    public float NavigationStartZ
    {
        get => _navigationStartZ;
        set => SetProperty(ref _navigationStartZ, value);
    }

    public float NavigationGoalX
    {
        get => _navigationGoalX;
        set => SetProperty(ref _navigationGoalX, value);
    }

    public float NavigationGoalZ
    {
        get => _navigationGoalZ;
        set => SetProperty(ref _navigationGoalZ, value);
    }

    public float GridResolution
    {
        get => _gridResolution;
        set => SetProperty(ref _gridResolution, Math.Clamp(value, 0.25f, 2.0f));
    }

    public float FixedTimestep
    {
        get => World.Settings.FixedDeltaTime;
        set
        {
            World.Settings.FixedDeltaTime = Math.Clamp(value, 1.0f / 240.0f, 1.0f / 15.0f);
            OnPropertyChanged();
        }
    }

    public float GravityY
    {
        get => World.Gravity.Y;
        set
        {
            World.Gravity = new Vector3(World.Gravity.X, value, World.Gravity.Z);
            OnPropertyChanged();
        }
    }

    public float PathLength
    {
        get => _pathLength;
        private set
        {
            if (SetProperty(ref _pathLength, value))
            {
                OnPropertyChanged(nameof(PathLengthDisplay));
            }
        }
    }

    public int ExpandedNodes
    {
        get => _expandedNodes;
        private set
        {
            if (SetProperty(ref _expandedNodes, value))
            {
                OnPropertyChanged(nameof(ExpandedNodesDisplay));
            }
        }
    }

    public double PlanningMilliseconds
    {
        get => _planningMilliseconds;
        private set
        {
            if (SetProperty(ref _planningMilliseconds, value))
            {
                OnPropertyChanged(nameof(PlanningTimeDisplay));
            }
        }
    }

    public string DataImportPath
    {
        get => _dataImportPath;
        set => SetProperty(ref _dataImportPath, value);
    }

    public string DataFormat
    {
        get => _dataFormat;
        private set
        {
            if (SetProperty(ref _dataFormat, value))
            {
                OnPropertyChanged(nameof(DataFormatDisplay));
            }
        }
    }

    public string ValidationSummary
    {
        get => _validationSummary;
        private set
        {
            if (SetProperty(ref _validationSummary, value))
            {
                OnPropertyChanged(nameof(ValidationSummaryDisplay));
            }
        }
    }

    public string CapabilitySummary
    {
        get => _capabilitySummary;
        private set
        {
            if (SetProperty(ref _capabilitySummary, value))
            {
                OnPropertyChanged(nameof(CapabilitySummaryDisplay));
            }
        }
    }

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
            var clamped = Math.Clamp(value, 0.0, Math.Max(0, _replayService.FrameCount - 1));
            if (SetProperty(ref _timelineValue, clamped) && IsReplayMode)
            {
                ApplyReplayFrame((int)Math.Round(clamped));
            }
        }
    }

    public int ReplayFrameCount => _replayService.FrameCount;

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
        Status = Text("한 단계 실행", "Stepped");
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
        NotifyProfilerDisplaysChanged();
        NotifySelectedObjectDiagnosticsChanged();
        OnPropertyChanged(nameof(ReplayFrameCount));
        SimulationAdvanced?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateEngineeringSystems(float deltaTime)
    {
        foreach (var item in _runtime.UpdateEngineeringSystems(deltaTime, _elapsedSimulationTime))
        {
            AddEvent(item.Severity, item.Source, item.Code, item.Message);
        }

        RefreshDetections();
    }

    private void LoadDropDemo()
    {
        ResetWorld();
        AddGround();
        AddSphere(new Vector3(-1.2f, 5.0f, 0.0f), PhysicsMaterial.Rubber);
        AddBox(new Vector3(1.1f, 4.0f, 0.0f), new Vector3(0.6f, 0.6f, 0.6f), PhysicsMaterial.Steel);
        IsRunning = false;
        Status = Text("낙하 데모", "Drop Demo");
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
        Status = Text("반발 데모", "Bounce Demo");
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
        Status = Text("마찰 데모", "Friction Demo");
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
        Status = Text("쌓기 데모", "Stack Demo");
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
        Status = Text($"스트레스 {count}", $"Stress {count}");
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
            AngularDamping = 2.5f,
            Constraints = MotionConstraints.PlanarXZ
        };
        var vehicleCollider = new BoxCollider(vehicleBody, vehicleBody.HalfExtents, vehicleMaterial);
        var vehicleObject = Register(vehicleCollider);
        _runtime.Vehicle = new VehicleEntity("vehicle-01", "Vehicle_01", vehicleBody);
        RegisterEntity(_runtime.Vehicle);

        _runtime.Sensor = new SensorEntity("sensor-01", "Sensor_01", _runtime.Vehicle)
        {
            Range = 8.0f,
            FieldOfViewDegrees = 75.0f,
            UpdateRateHz = 12.0f
        };
        RegisterEntity(_runtime.Sensor);

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

        _runtime.MissionController.SetWaypoints(waypoints);
        SelectedObject = vehicleObject;
        SelectedEntity = Entities.FirstOrDefault(item => ReferenceEquals(item.Entity, _runtime.Vehicle));
        Status = Text("자율 주행 안전 테스트", "Autonomous Vehicle Safety Test");
        AddEvent(EventSeverity.Info, "Scenario", "LOAD", "Engineering scenario loaded");
        NotifySceneChanged();
        EngineeringSceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddObstacle(string name, Vector3 position)
    {
        AddObstacle(name, position, new Vector3(0.6f));
    }

    private void AddObstacle(string name, Vector3 position, Vector3 halfExtents)
    {
        var material = new PhysicsMaterial(0.8f, 0.05f);
        var body = new RigidBody(name, position)
        {
            IsStatic = true,
            Material = material,
            HalfExtents = halfExtents
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
        _runtime.Reset();
        Entities.Clear();
        DetectionDetails.Clear();
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
        _runtime.Entities.Add(entity);
        Entities.Add(new EntityViewModel(entity));
    }

    private void RunSimulation()
    {
        if (_runtime.Vehicle is not null
            && _runtime.MissionController.State is Core.Simulation.Mission.MissionState.Ready or Core.Simulation.Mission.MissionState.Paused)
        {
            StartMission();
            return;
        }

        IsRunning = true;
    }

    private void ApplyWorkspaceVisibility()
    {
        var workspace = NormalizeWorkspace(SelectedWorkspace);
        IsTelemetryVisible = workspace != "PhysicsLab";
        IsEventPanelVisible = workspace != "PhysicsLab";
        IsReplayVisible = true;
        IsGraphVisible = true;
    }

    private void PlanPath()
    {
        if (_runtime.Vehicle is null)
        {
            LoadEngineeringScenario();
        }

        if (_runtime.Vehicle is null)
        {
            return;
        }

        NavigationStartX = _runtime.Vehicle.Position.X;
        NavigationStartZ = _runtime.Vehicle.Position.Z;
        var mobility = NormalizeMobility(SelectedMobilityModel) == "CarLike" ? MobilityModelType.CarLike : MobilityModelType.Holonomic;
        var plannerSelection = NormalizePlanner(SelectedPlanner) switch
        {
            "GridAStar" => PlannerSelection.GridAStar,
            "HybridAStar" => PlannerSelection.HybridAStar,
            _ => PlannerSelection.Auto
        };

        var planner = _pathPlannerSelector.Select(mobility, plannerSelection);
        var result = planner.Plan(new PathRequest
        {
            Start = new NavigationPose(NavigationStartX, NavigationStartZ, _runtime.Vehicle.HeadingDegrees),
            Goal = new NavigationPose(NavigationGoalX, NavigationGoalZ, TargetHeading),
            GridResolution = GridResolution,
            Vehicle = new VehicleNavigationProfile
            {
                Width = _runtime.Vehicle.PhysicsBody?.HalfExtents.X * 2.0f ?? 1.1f,
                Length = _runtime.Vehicle.PhysicsBody?.HalfExtents.Z * 2.0f ?? 1.7f,
                Wheelbase = 1.2f,
                MaxSteeringAngleDegrees = 30.0f
            },
            Obstacles = BuildNavigationObstacles()
        });

        NavigationPath.Clear();
        foreach (var point in result.Points)
        {
            NavigationPath.Add(point);
        }

        PathLength = result.PathLength;
        ExpandedNodes = result.ExpandedNodes;
        PlanningMilliseconds = result.PlanningTime.TotalMilliseconds;
        Status = result.Succeeded
            ? Text($"경로 계획 완료: {result.Points.Count}개 지점", $"Path planned: {result.Points.Count} points")
            : Text($"경로 계획 실패: {result.Message}", $"Path failed: {result.Message}");

        if (result.Succeeded)
        {
            ApplyPathAsMission(result.Points);
        }

        EngineeringSceneChanged?.Invoke(this, EventArgs.Empty);
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<NavigationObstacle> BuildNavigationObstacles()
    {
        return _runtime.Entities
            .Where(entity => entity.EntityType == EntityType.Obstacle && entity.PhysicsBody is not null)
            .Select(entity => new NavigationObstacle(
                entity.Id,
                entity.Position.X,
                entity.Position.Z,
                entity.PhysicsBody!.HalfExtents.X * 2.0f,
                entity.PhysicsBody.HalfExtents.Z * 2.0f))
            .ToList();
    }

    private void ApplyPathAsMission(IReadOnlyList<PathPoint> points)
    {
        var stride = Math.Max(1, (int)MathF.Round(1.0f / Math.Max(0.25f, GridResolution)));
        var reduced = points
            .Where((_, index) => index == points.Count - 1 || index % stride == 0)
            .Skip(1)
            .Select((point, index) => new WaypointEntity($"nav-wp-{index + 1:00}", $"Path_{index + 1:00}", new Vector3(point.X, 0.05f, point.Z), index + 1, 0.75f))
            .ToArray();

        foreach (var existing in _runtime.Entities.Where(entity => entity.EntityType == EntityType.Waypoint).ToList())
        {
            _runtime.Entities.Remove(existing);
            var viewModel = Entities.FirstOrDefault(item => ReferenceEquals(item.Entity, existing));
            if (viewModel is not null)
            {
                Entities.Remove(viewModel);
            }
        }

        foreach (var waypoint in reduced)
        {
            RegisterEntity(waypoint);
        }

        _runtime.MissionController.SetWaypoints(reduced);
        RefreshEngineeringTelemetry();
    }

    private void ClearPath()
    {
        NavigationPath.Clear();
        PathLength = 0.0f;
        ExpandedNodes = 0;
        PlanningMilliseconds = 0.0;
        Status = Text("경로 지움", "Path cleared");
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ImportData()
    {
        if (string.IsNullOrWhiteSpace(DataImportPath) || !File.Exists(DataImportPath))
        {
            Status = Text("데이터 가져오기 실패: 파일을 찾을 수 없음", "Data import failed: file not found");
            return;
        }

        var content = File.ReadAllText(DataImportPath);
        var parser = SelectParser(DataImportPath, content);
        if (parser is null)
        {
            Status = Text("데이터 가져오기 실패: 지원하지 않는 형식", "Data import failed: unsupported format");
            return;
        }

        var dataSet = parser.Parse(content);
        var mapping = _schemaDetector.RecommendMapping(dataSet);
        var validation = _dataValidator.Validate(dataSet, mapping, [ForgeField.PositionX, ForgeField.PositionY, ForgeField.PositionZ]);
        var capabilities = _capabilityDetector.Detect(mapping);
        var importedObstacles = _environmentBuilder.BuildObstacles(dataSet);

        DetectedFields.Clear();
        foreach (var field in _schemaDetector.DetectFields(dataSet))
        {
            DetectedFields.Add(field);
        }

        DataFormat = dataSet.Format;
        ValidationSummary = Text(
            $"전체 {validation.TotalRecords}, 유효 {validation.ValidRecords}, 오류 {validation.InvalidRecords}",
            $"Records {validation.TotalRecords}, Valid {validation.ValidRecords}, Invalid {validation.InvalidRecords}");
        CapabilitySummary = capabilities.Count == 0 ? "--" : string.Join(", ", capabilities.OrderBy(item => item.ToString()));
        foreach (var obstacle in importedObstacles)
        {
            AddObstacle(obstacle.Id, new Vector3(obstacle.X, 0.5f, obstacle.Z), new Vector3(obstacle.Width * 0.5f, 0.5f, obstacle.Depth * 0.5f));
        }

        if (importedObstacles.Count > 0)
        {
            NotifySceneChanged();
        }

        Status = importedObstacles.Count > 0
            ? Text($"{dataSet.Format} 레코드 {dataSet.Records.Count}개 가져옴, 장애물 {importedObstacles.Count}개 추가", $"Imported {dataSet.Records.Count} {dataSet.Format} records, added {importedObstacles.Count} obstacles")
            : Text($"{dataSet.Format} 레코드 {dataSet.Records.Count}개 가져옴", $"Imported {dataSet.Records.Count} {dataSet.Format} records");
    }

    private static IDataParser? SelectParser(string fileName, string content)
    {
        IDataParser[] parsers = [new CsvDataParser(), new JsonDataParser()];
        return parsers.FirstOrDefault(parser => parser.CanParse(fileName, content));
    }

    private string Text(string korean, string english)
    {
        return SelectedLanguage == "English" ? english : korean;
    }

    private void OnLocalizationChanged()
    {
        RefreshLocalizedOptions();
        RefreshDetections();
        RefreshEventLog();
        RefreshContactDetails();
        OnPropertyChanged(string.Empty);
    }

    private void RefreshLocalizedOptions()
    {
        var workspace = NormalizeWorkspace(SelectedWorkspace);
        var mobility = NormalizeMobility(SelectedMobilityModel);
        var planner = NormalizePlanner(SelectedPlanner);

        ReplaceOptions(Workspaces, WorkspaceOptions());
        ReplaceOptions(MobilityModels, MobilityOptions());
        ReplaceOptions(Planners, PlannerOptions());

        _selectedWorkspace = WorkspaceText(workspace);
        _selectedMobilityModel = MobilityText(mobility);
        _selectedPlanner = PlannerText(planner);

        OnPropertyChanged(nameof(SelectedWorkspace));
        OnPropertyChanged(nameof(SelectedMobilityModel));
        OnPropertyChanged(nameof(SelectedPlanner));
        ApplyWorkspaceVisibility();
    }

    private IReadOnlyList<string> WorkspaceOptions()
    {
        return SelectedLanguage == "English"
            ? ["Physics Lab", "System Simulation", "Data Analysis"]
            : ["물리 실험실", "시스템 시뮬레이션", "데이터 분석"];
    }

    private IReadOnlyList<string> MobilityOptions()
    {
        return SelectedLanguage == "English"
            ? ["Car-like", "Holonomic"]
            : ["차량형", "전방향"];
    }

    private IReadOnlyList<string> PlannerOptions()
    {
        return SelectedLanguage == "English"
            ? ["Auto", "Grid A*", "Hybrid A*"]
            : ["자동", "그리드 A*", "하이브리드 A*"];
    }

    private string WorkspaceText(string key)
    {
        return key switch
        {
            "PhysicsLab" => Text("물리 실험실", "Physics Lab"),
            "DataAnalysis" => Text("데이터 분석", "Data Analysis"),
            _ => Text("시스템 시뮬레이션", "System Simulation")
        };
    }

    private string MobilityText(string key)
    {
        return key == "CarLike" ? Text("차량형", "Car-like") : Text("전방향", "Holonomic");
    }

    private string PlannerText(string key)
    {
        return key switch
        {
            "GridAStar" => Text("그리드 A*", "Grid A*"),
            "HybridAStar" => Text("하이브리드 A*", "Hybrid A*"),
            _ => Text("자동", "Auto")
        };
    }

    private static string NormalizeWorkspace(string value)
    {
        return value switch
        {
            "Physics Lab" or "물리 실험실" => "PhysicsLab",
            "Data Analysis" or "데이터 분석" => "DataAnalysis",
            _ => "SystemSimulation"
        };
    }

    private static string NormalizeMobility(string value)
    {
        return value switch
        {
            "Car-like" or "차량형" => "CarLike",
            _ => "Holonomic"
        };
    }

    private static string NormalizePlanner(string value)
    {
        return value switch
        {
            "Grid A*" or "그리드 A*" => "GridAStar",
            "Hybrid A*" or "하이브리드 A*" => "HybridAStar",
            _ => "Auto"
        };
    }

    private static void ReplaceOptions(ObservableCollection<string> target, IReadOnlyList<string> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private void NotifySelectedObjectDisplayChanged()
    {
        OnPropertyChanged(nameof(SelectedObjectDisplayName));
        NotifySelectedObjectDiagnosticsChanged();
    }

    private void NotifySelectedObjectDiagnosticsChanged()
    {
        OnPropertyChanged(nameof(CenterOfMassDisplay));
        OnPropertyChanged(nameof(SleepStateDisplay));
        OnPropertyChanged(nameof(CurrentSpeedDisplay));
        OnPropertyChanged(nameof(AngularSpeedDisplay));
        OnPropertyChanged(nameof(KineticEnergyDisplay));
    }

    private void NotifyProfilerDisplaysChanged()
    {
        OnPropertyChanged(nameof(BodyCountDisplay));
        OnPropertyChanged(nameof(ColliderCountDisplay));
        OnPropertyChanged(nameof(PotentialPairCountDisplay));
        OnPropertyChanged(nameof(CandidatePairCountDisplay));
        OnPropertyChanged(nameof(ContactCountDisplay));
        OnPropertyChanged(nameof(BroadPhaseDisplay));
        OnPropertyChanged(nameof(NarrowPhaseDisplay));
        OnPropertyChanged(nameof(SolverDisplay));
        OnPropertyChanged(nameof(PhysicsTimeDisplay));
    }

    private string EventSeverityText(EventSeverity severity)
    {
        return severity switch
        {
            EventSeverity.Info => Text("정보", "Info"),
            EventSeverity.Warning => Text("경고", "Warning"),
            EventSeverity.Critical => Text("치명", "Critical"),
            _ => severity.ToString()
        };
    }

    private string EventSourceText(string source)
    {
        return source switch
        {
            "Scenario" => Text("시나리오", "Scenario"),
            "Mission" => Text("미션", "Mission"),
            "Vehicle" => Text("차량", "Vehicle"),
            "Fault" => Text("고장", "Fault"),
            "Sensor" => Text("센서", "Sensor"),
            _ => source
        };
    }

    private string EventMessageText(string message)
    {
        return message switch
        {
            "Engineering scenario loaded" => Text("엔지니어링 시나리오 로드됨", message),
            "Mission started" => Text("미션 시작됨", message),
            "Mission reset" => Text("미션 초기화됨", message),
            "Manual emergency stop applied" => Text("수동 비상정지 적용됨", message),
            "All faults cleared" => Text("모든 고장 해제됨", message),
            _ => message
        };
    }

    private void StartMission()
    {
        if (_runtime.Vehicle is null)
        {
            LoadEngineeringScenario();
        }

        _runtime.MissionController.Start();
        AddEvent(EventSeverity.Info, "Mission", "START", "Mission started");
        IsRunning = true;
        RefreshEngineeringTelemetry();
    }

    private void ResetMission()
    {
        _runtime.MissionController.Reset();
        if (_runtime.Vehicle is not null)
        {
            _runtime.Vehicle.TargetSpeed = 0.0f;
            _runtime.Vehicle.MotionState = MotionState.Idle;
        }

        AddEvent(EventSeverity.Info, "Mission", "RESET", "Mission reset");
        RefreshEngineeringTelemetry();
    }

    private void ManualEmergencyStop()
    {
        if (_runtime.Vehicle is null)
        {
            return;
        }

        _runtime.VehicleController.EmergencyStop(_runtime.Vehicle);
        _runtime.MissionController.EmergencyStop();
        AddEvent(EventSeverity.Critical, "Vehicle", "MANUAL_ESTOP", "Manual emergency stop applied");
        RefreshEngineeringTelemetry();
    }

    private void ToggleFault(FaultType faultType)
    {
        if (_runtime.Vehicle is null)
        {
            LoadEngineeringScenario();
        }

        if (_runtime.Vehicle is null)
        {
            return;
        }

        var enabled = _runtime.FaultManager.Toggle(faultType, _runtime.Vehicle, _runtime.VehicleController, _runtime.Sensor);
        AddEvent(enabled ? EventSeverity.Warning : EventSeverity.Info, "Fault", faultType.ToString(), enabled ? $"{faultType} injected" : $"{faultType} cleared");
        RefreshEngineeringTelemetry();
    }

    private void ClearFaults()
    {
        if (_runtime.Vehicle is null)
        {
            return;
        }

        _runtime.FaultManager.Clear(_runtime.Vehicle, _runtime.VehicleController, _runtime.Sensor);
        AddEvent(EventSeverity.Info, "Fault", "CLEAR", "All faults cleared");
        RefreshEngineeringTelemetry();
    }

    private void RefreshDetections()
    {
        DetectionDetails.Clear();

        if (_runtime.Sensor is null)
        {
            return;
        }

        foreach (var detection in _runtime.Sensor.Detections)
        {
            DetectionDetails.Add(Text(
                $"{detection.TargetName} | {detection.TargetType} | 거리 {detection.Distance:F2} m | 상대각 {detection.RelativeBearingDegrees:F1}도",
                $"{detection.TargetName} | {detection.TargetType} | {detection.Distance:F2} m | {detection.RelativeBearingDegrees:F1} deg"));
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
        OnPropertyChanged(nameof(MissionStateDisplay));
        OnPropertyChanged(nameof(CurrentWaypointDisplay));
        OnPropertyChanged(nameof(MissionProgressDisplay));
        OnPropertyChanged(nameof(DistanceToWaypointDisplay));
        OnPropertyChanged(nameof(SafetyStateDisplay));
        OnPropertyChanged(nameof(SafetyTargetDisplay));
        OnPropertyChanged(nameof(SafetyDistanceDisplay));
        OnPropertyChanged(nameof(TimeToCollisionDisplay));
        OnPropertyChanged(nameof(TargetSpeed));
        OnPropertyChanged(nameof(TargetHeading));
        EngineeringSceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddEvent(EventSeverity severity, string source, string code, string message)
    {
        _eventLogService.Add(_elapsedSimulationTime, severity, source, code, message);
        RefreshEventLog();
    }

    private void RefreshEventLog()
    {
        EventLog.Clear();
        foreach (var item in _eventLogService.Events)
        {
            EventLog.Add($"{item.Timestamp,6:F2}s  {EventSeverityText(item.Severity),-8}  {EventSourceText(item.Source)}  {item.Code}  {EventMessageText(item.Message)}");
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
            ContactDetails.Add(Text(
                $"접촉 #{i + 1} | 상대: {other.Name} | 지점: {FormatVector(contact.Point)} | 법선: {FormatVector(contact.Normal)} | 침투: {contact.Penetration:F3} | 상대속도: {contact.RelativeVelocity.Length():F2} | 충격량: {contact.AppliedImpulse:F2}",
                $"Contact #{i + 1} | Other: {other.Name} | Point: {FormatVector(contact.Point)} | Normal: {FormatVector(contact.Normal)} | Pen: {contact.Penetration:F3} | RelVel: {contact.RelativeVelocity.Length():F2} | Impulse: {contact.AppliedImpulse:F2}"));
        }
    }

    private void SampleGraph()
    {
        if (!_telemetryRecorder.TrySample(SelectedObject?.Body, _elapsedSimulationTime, World.Settings.FixedDeltaTime, out var sample))
        {
            return;
        }

        GraphSamples.Add(sample);

        while (GraphSamples.Count > _telemetryRecorder.MaxSamples)
        {
            GraphSamples.RemoveAt(0);
        }
    }

    private void ResetGraphSamples()
    {
        _telemetryRecorder.Clear();
        GraphSamples.Clear();
    }

    private void CaptureReplayFrame()
    {
        _replayService.Capture(_elapsedSimulationTime, World.Bodies);
        TimelineValue = _replayService.FrameCount - 1;
    }

    private void ApplyReplayFrame(int index)
    {
        if (_replayService.TryApply(index, World.Bodies, out var time))
        {
            _elapsedSimulationTime = time;
        }
    }

    private void AdvanceReplay(float frameDeltaTime)
    {
        if (_replayService.FrameCount == 0)
        {
            return;
        }

        TimelineValue = Math.Min(_replayService.FrameCount - 1, TimelineValue + (frameDeltaTime * 60.0 * ReplaySpeed));
    }

    private void ClearReplay()
    {
        _replayService.Clear();
        TimelineValue = 0.0;
        IsReplayMode = false;
        OnPropertyChanged(nameof(ReplayFrameCount));
    }

    private static string FormatVector(Vector3 value)
    {
        return $"{value.X:F2}, {value.Y:F2}, {value.Z:F2}";
    }

}
