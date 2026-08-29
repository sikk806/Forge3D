using Forge3D.Core.Simulation.Sensors;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Core.Simulation.Faults;

public sealed class FaultManager
{
    private readonly HashSet<FaultType> _activeFaults = [];
    private float? _baseFriction;
    private float? _baseMotorScale;
    private bool? _baseCommandsEnabled;
    private bool? _baseSensorEnabled;
    private SensorState? _baseSensorState;

    public IReadOnlyCollection<FaultType> ActiveFaults => _activeFaults;

    public bool HasFault(FaultType faultType)
    {
        return _activeFaults.Contains(faultType);
    }

    public bool Toggle(FaultType faultType, VehicleEntity vehicle, VehicleController controller, SensorEntity? sensor)
    {
        if (_activeFaults.Contains(faultType))
        {
            _activeFaults.Remove(faultType);
        }
        else
        {
            _activeFaults.Add(faultType);
        }

        Apply(vehicle, controller, sensor);
        if (_activeFaults.Count == 0)
        {
            ReleaseBaseState();
        }

        return _activeFaults.Contains(faultType);
    }

    public void Clear(VehicleEntity vehicle, VehicleController controller, SensorEntity? sensor)
    {
        _activeFaults.Clear();
        Apply(vehicle, controller, sensor);
        ReleaseBaseState();
    }

    public void Reset()
    {
        _activeFaults.Clear();
        ReleaseBaseState();
    }

    private void Apply(VehicleEntity vehicle, VehicleController controller, SensorEntity? sensor)
    {
        CaptureBaseState(vehicle, controller, sensor);

        if (sensor is not null)
        {
            sensor.State = HasFault(FaultType.SensorFailure) ? SensorState.Fault : _baseSensorState ?? SensorState.Normal;
            sensor.IsEnabled = !HasFault(FaultType.SensorFailure) && (_baseSensorEnabled ?? true);
        }

        if (vehicle.PhysicsBody is not null)
        {
            var baseFriction = _baseFriction ?? vehicle.PhysicsBody.Material.Friction;
            vehicle.PhysicsBody.Material.Friction = HasFault(FaultType.WheelSlip) ? baseFriction * 0.12f : baseFriction;
        }

        var baseMotorScale = _baseMotorScale ?? controller.MotorScale;
        controller.MotorScale = HasFault(FaultType.MotorDegradation) ? baseMotorScale * 0.35f : baseMotorScale;
        controller.CommandsEnabled = !HasFault(FaultType.CommunicationLoss) && (_baseCommandsEnabled ?? true);
    }

    private void CaptureBaseState(VehicleEntity vehicle, VehicleController controller, SensorEntity? sensor)
    {
        _baseFriction ??= vehicle.PhysicsBody?.Material.Friction;
        _baseMotorScale ??= controller.MotorScale;
        _baseCommandsEnabled ??= controller.CommandsEnabled;

        if (sensor is not null)
        {
            _baseSensorEnabled ??= sensor.IsEnabled;
            _baseSensorState ??= sensor.State;
        }
    }

    private void ReleaseBaseState()
    {
        _baseFriction = null;
        _baseMotorScale = null;
        _baseCommandsEnabled = null;
        _baseSensorEnabled = null;
        _baseSensorState = null;
    }
}
