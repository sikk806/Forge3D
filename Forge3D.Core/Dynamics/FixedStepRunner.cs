namespace Forge3D.Core.Dynamics;

public sealed class FixedStepRunner
{
    private readonly PhysicsWorld _world;
    private float _accumulator;

    public FixedStepRunner(PhysicsWorld world)
    {
        _world = world;
    }

    public float InterpolationAlpha
    {
        get
        {
            var fixedDeltaTime = _world.Settings.FixedDeltaTime;
            return fixedDeltaTime <= 0.0f ? 1.0f : Math.Clamp(_accumulator / fixedDeltaTime, 0.0f, 1.0f);
        }
    }

    public int Step(float frameDeltaTime)
    {
        if (frameDeltaTime <= 0.0f)
        {
            return 0;
        }

        _accumulator += frameDeltaTime;

        var fixedDeltaTime = _world.Settings.FixedDeltaTime;
        var steps = 0;

        while (_accumulator >= fixedDeltaTime && steps < _world.Settings.MaxSubSteps)
        {
            _world.Step(fixedDeltaTime);
            _accumulator -= fixedDeltaTime;
            steps++;
        }

        if (steps == _world.Settings.MaxSubSteps)
        {
            _accumulator = 0.0f;
        }

        return steps;
    }

    public void Reset()
    {
        _accumulator = 0.0f;
        _world.SnapBodyPoses();
    }
}
