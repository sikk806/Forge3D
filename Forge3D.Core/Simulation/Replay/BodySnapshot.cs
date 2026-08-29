using System.Numerics;

namespace Forge3D.Core.Simulation.Replay;

public readonly record struct BodySnapshot(
    string Name,
    Vector3 Position,
    Quaternion Orientation,
    Vector3 LinearVelocity,
    Vector3 AngularVelocity);
