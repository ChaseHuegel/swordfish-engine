using System;
using System.Collections.Generic;
using Krill;
using Swordfish.Physics;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Survival;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Applies server-authoritative kinetic damage when a player character closes on a physics body faster
/// than <see cref="GameplayConfig.CollisionSafeSpeed"/>. Contacts dispatched by the world's physics slot
/// are queued and drained in tick order, so a compound voxel manifold that reports several points for one
/// slam is debounced by <see cref="GameplayConfig.CollisionCooldownSeconds"/> instead of stacking hits.
/// </summary>
public sealed class ServerCollisionDamageSystem : IServerWorldSystem
{
    private const float DEFAULT_SAFE_SPEED = 6f;
    private const float DEFAULT_DAMAGE_PER_SPEED = 5f;
    private const float DEFAULT_MAX_DAMAGE = 100f;
    private const float DEFAULT_COOLDOWN_SECONDS = 0.25f;
    private const int PRUNE_INTERVAL_TICKS = 600;

    private readonly SimulationStep _simulationStep;
    private readonly GameplayConfig _gameplayConfig;
    private readonly List<PhysicsContactEvent> _pending = new(64);
    private readonly Dictionary<Uuid, float> _invulnerableUntil = [];
    private float _clockSeconds;
    private int _ticksSincePrune;

    public ServerCollisionDamageSystem(
        SimulationStep simulationStep,
        GameplayConfig gameplayConfig,
        IServerWorldPhysics physics
    ) {
        _simulationStep = simulationStep;
        _gameplayConfig = gameplayConfig;
        physics.ContactResolved += OnContactResolved;
    }

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        if (!float.IsFinite(delta) || delta <= 0f)
        {
            return;
        }

        _clockSeconds += delta;

        if (_pending.Count > 0)
        {
            ProcessContacts(store);
            _pending.Clear();
        }

        if (++_ticksSincePrune >= PRUNE_INTERVAL_TICKS)
        {
            _ticksSincePrune = 0;
            PruneInvulnerable(store);
        }
    }

    private void OnContactResolved(PhysicsContactEvent contact)
    {
        //  Physics dispatches on the world tick thread after the solver lock is released, so queuing
        //  here stays lock-free with the store work in Tick.
        _pending.Add(contact);
    }

    private void ProcessContacts(DataStore store)
    {
        float safeSpeed = SanitizeSpeed(_gameplayConfig.CollisionSafeSpeed.Get());
        float damagePerSpeed = SanitizeDamage(_gameplayConfig.CollisionDamagePerSpeed.Get());
        float maxDamage = SanitizeMaxDamage(_gameplayConfig.CollisionMaxDamage.Get());
        float cooldown = SanitizeCooldown(_gameplayConfig.CollisionCooldownSeconds.Get());

        if (damagePerSpeed <= 0f || maxDamage <= 0f)
        {
            return;
        }

        long simTick = _simulationStep.CurrentSimTick;
        for (var i = 0; i < _pending.Count; i++)
        {
            PhysicsContactEvent contact = _pending[i];
            if (contact.Kind != PhysicsContactKind.Added)
            {
                continue;
            }

            ApplyToCharacter(store, contact.EntityA, contact.ImpactSpeed, safeSpeed, damagePerSpeed, maxDamage, cooldown, simTick);
            if (contact.EntityB != contact.EntityA)
            {
                ApplyToCharacter(store, contact.EntityB, contact.ImpactSpeed, safeSpeed, damagePerSpeed, maxDamage, cooldown, simTick);
            }
        }
    }

    private void ApplyToCharacter(
        DataStore store,
        int entity,
        float impactSpeed,
        float safeSpeed,
        float damagePerSpeed,
        float maxDamage,
        float cooldown,
        long simTick
    ) {
        if (impactSpeed <= safeSpeed)
        {
            return;
        }

        if (!store.TryGet(entity, out OwnedCharacterComponent _))
        {
            return;
        }

        if (!store.TryGet(entity, out GameModeComponent gameMode) || gameMode.Mode != GameMode.Adventure)
        {
            return;
        }

        Uuid owner = store.GetUuid(entity);
        if (owner != Uuid.Null && _invulnerableUntil.TryGetValue(owner, out float until) && _clockSeconds < until)
        {
            return;
        }

        float amount = MathF.Min((impactSpeed - safeSpeed) * damagePerSpeed, maxDamage);
        DamageResult result = DamageService.ApplyDamage(store, entity, amount, DamageSource.KineticImpact, simTick);
        if (result.Applied <= 0f)
        {
            return;
        }

        if (owner != Uuid.Null && cooldown > 0f)
        {
            _invulnerableUntil[owner] = _clockSeconds + cooldown;
        }
    }

    private void PruneInvulnerable(DataStore store)
    {
        if (_invulnerableUntil.Count == 0)
        {
            return;
        }

        List<Uuid>? stale = null;
        foreach (Uuid owner in _invulnerableUntil.Keys)
        {
            if (_invulnerableUntil[owner] <= _clockSeconds || !store.TryGet(owner, out _))
            {
                (stale ??= []).Add(owner);
            }
        }

        if (stale == null)
        {
            return;
        }

        for (var i = 0; i < stale.Count; i++)
        {
            _invulnerableUntil.Remove(stale[i]);
        }
    }

    private static float SanitizeSpeed(float value)
    {
        return float.IsFinite(value) && value >= 0f ? value : DEFAULT_SAFE_SPEED;
    }

    private static float SanitizeDamage(float value)
    {
        return float.IsFinite(value) && value >= 0f ? value : DEFAULT_DAMAGE_PER_SPEED;
    }

    private static float SanitizeMaxDamage(float value)
    {
        return float.IsFinite(value) && value >= 0f ? value : DEFAULT_MAX_DAMAGE;
    }

    private static float SanitizeCooldown(float value)
    {
        return float.IsFinite(value) && value >= 0f ? value : DEFAULT_COOLDOWN_SECONDS;
    }
}
