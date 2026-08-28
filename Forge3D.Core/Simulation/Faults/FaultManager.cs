using Forge3D.Core.Simulation.Sensors;
using Forge3D.Core.Simulation.Vehicle;

namespace Forge3D.Core.Simulation.Faults;

public sealed class FaultManager
{
    private readonly HashSet<FaultType> _activeFaults = [];

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
        return _activeFaults.Contains(faultType);
    }

    public void Clear(VehicleEntity vehicle, VehicleController controller, SensorEntity? sensor)
    {
        _activeFaults.Clear();
        Apply(vehicle, controller, sensor);
    }

    private void Apply(VehicleEntity vehicle, VehicleController controller, SensorEntity? sensor)
    {
        if (sensor is not null)
        {
            sensor.State = HasFault(FaultType.SensorFailure) ? SensorState.Fault : SensorState.Normal;
            sensor.IsEnabled = !HasFault(FaultType.SensorFailure);
        }

        if (vehicle.PhysicsBody is not null)
        {
            vehicle.PhysicsBody.Material.Friction = HasFault(FaultType.WheelSlip) ? 0.05f : 0.45f;
        }

        controller.MotorScale = HasFault(FaultType.MotorDegradation) ? 0.35f : 1.0f;
        controller.CommandsEnabled = !HasFault(FaultType.CommunicationLoss);
    }
}
