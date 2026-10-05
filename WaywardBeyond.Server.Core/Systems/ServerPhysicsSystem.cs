using System;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Physics;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using WaywardBeyond.Server.Core;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// The per-world physics bundle: owns the world's <see cref="JoltPhysicsSystem"/> and positions it in
/// the world's tick order. Created directly from the shared physics config (never resolved by the
/// engine's concrete type, which the engine also registers as a root singleton) and exposed under the
/// game-owned <see cref="IServerWorldPhysics"/> identity for the shared simulation step.
/// </summary>
public sealed class ServerPhysicsSystem : IServerWorldSystem, IServerWorldPhysics
{
    public event EventHandler<EventArgs>? FixedUpdate;

    private readonly JoltPhysicsSystem _physics;

    public ServerPhysicsSystem(
        ILoggerFactory loggerFactory,
        in PhysicsSettings physicsSettings
    ) {
        _physics = new JoltPhysicsSystem(loggerFactory.CreateLogger<JoltPhysicsSystem>(), physicsSettings);
        _physics.FixedUpdate += OnFixedUpdate;
    }

    public void Tick(float delta, DataStore store)
    {
        _physics.Tick(delta, store);
    }

    public RaycastResult Raycast(in Ray ray)
    {
        return _physics.Raycast(ray);
    }

    public void SetGravity(System.Numerics.Vector3 gravity)
    {
        _physics.SetGravity(gravity);
    }

    private void OnFixedUpdate(object? sender, EventArgs e)
    {
        FixedUpdate?.Invoke(this, e);
    }
}