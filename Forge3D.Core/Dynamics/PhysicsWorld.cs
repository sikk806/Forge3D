using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using Forge3D.Core.Collision;
using Forge3D.Core.Diagnostics;
using Forge3D.Core.Solver;

namespace Forge3D.Core.Dynamics;

public sealed class PhysicsWorld
{
    private readonly List<RigidBody> _bodies = [];
    private readonly List<Collider> _colliders = [];
    private readonly List<Contact> _contacts = [];
    private readonly CollisionDispatcher _collisionDispatcher = new();
    private readonly IBroadPhase _broadPhase;
    private readonly IContactSolver _contactSolver;

    public PhysicsWorld()
        : this(new PhysicsSettings())
    {
    }

    public PhysicsWorld(PhysicsSettings settings)
        : this(settings, new ImpulseContactSolver())
    {
    }

    public PhysicsWorld(PhysicsSettings settings, IContactSolver contactSolver)
        : this(settings, contactSolver, new AabbBroadPhase())
    {
    }

    public PhysicsWorld(PhysicsSettings settings, IContactSolver contactSolver, IBroadPhase broadPhase)
    {
        Settings = settings;
        _contactSolver = contactSolver;
        _broadPhase = broadPhase;
    }

    public PhysicsSettings Settings { get; }

    public Vector3 Gravity
    {
        get => Settings.Gravity;
        set => Settings.Gravity = value;
    }

    public ReadOnlyCollection<RigidBody> Bodies => _bodies.AsReadOnly();

    public ReadOnlyCollection<Collider> Colliders => _colliders.AsReadOnly();

    public ReadOnlyCollection<Contact> Contacts => _contacts.AsReadOnly();

    public PhysicsStepStats LastStepStats { get; private set; }

    public void AddBody(RigidBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (!_bodies.Contains(body))
        {
            _bodies.Add(body);
        }
    }

    public bool RemoveBody(RigidBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        _colliders.RemoveAll(collider => ReferenceEquals(collider.Body, body));
        return _bodies.Remove(body);
    }

    public void AddCollider(Collider collider)
    {
        ArgumentNullException.ThrowIfNull(collider);
        AddBody(collider.Body);

        if (!_colliders.Contains(collider))
        {
            _colliders.Add(collider);
        }
    }

    public bool RemoveCollider(Collider collider)
    {
        ArgumentNullException.ThrowIfNull(collider);
        return _colliders.Remove(collider);
    }

    public void Clear()
    {
        _bodies.Clear();
        _colliders.Clear();
        _contacts.Clear();
        LastStepStats = default;
    }

    public PhysicsStepStats RefreshStats()
    {
        LastStepStats = new PhysicsStepStats(
            _bodies.Count,
            TimeSpan.Zero,
            _colliders.Count,
            0,
            0,
            _contacts.Count);
        return LastStepStats;
    }

    public PhysicsStepStats Step(float deltaTime)
    {
        if (deltaTime <= 0.0f)
        {
            LastStepStats = new PhysicsStepStats(_bodies.Count, TimeSpan.Zero, _colliders.Count);
            return LastStepStats;
        }

        var totalStopwatch = Stopwatch.StartNew();

        foreach (var body in _bodies)
        {
            body.IntegrateForces(Gravity, deltaTime);
        }

        var collisionStopwatch = Stopwatch.StartNew();
        var pairStats = GenerateContacts();
        collisionStopwatch.Stop();

        var solverStopwatch = Stopwatch.StartNew();
        _contactSolver.Solve(_contacts, deltaTime);
        solverStopwatch.Stop();

        foreach (var body in _bodies)
        {
            body.IntegrateTransform(deltaTime);
            body.ClearForces();
        }

        totalStopwatch.Stop();
        LastStepStats = new PhysicsStepStats(
            _bodies.Count,
            totalStopwatch.Elapsed,
            _colliders.Count,
            pairStats.PotentialPairs,
            pairStats.CandidatePairs,
            _contacts.Count,
            pairStats.BroadPhaseTime,
            pairStats.NarrowPhaseTime,
            collisionStopwatch.Elapsed,
            solverStopwatch.Elapsed);
        return LastStepStats;
    }

    private CollisionPairStats GenerateContacts()
    {
        _contacts.Clear();
        var broadPhaseResult = _broadPhase.FindPairs(_colliders);
        var narrowPhaseTime = TimeSpan.Zero;

        foreach (var pair in broadPhaseResult.CandidatePairs)
        {
            var narrowStopwatch = Stopwatch.StartNew();
            if (_collisionDispatcher.TryGenerateContact(pair.ColliderA, pair.ColliderB, out var contact))
            {
                _contacts.Add(contact);
            }
            narrowStopwatch.Stop();
            narrowPhaseTime += narrowStopwatch.Elapsed;
        }

        return new CollisionPairStats(
            broadPhaseResult.PotentialPairCount,
            broadPhaseResult.CandidatePairs.Count,
            broadPhaseResult.Elapsed,
            narrowPhaseTime);
    }

    private readonly record struct CollisionPairStats(
        int PotentialPairs,
        int CandidatePairs,
        TimeSpan BroadPhaseTime,
        TimeSpan NarrowPhaseTime);
}
