using System;
using System.Collections.Generic;
using Krill;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Buffs;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Survival;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-authoritative Adventure Mode survival. Drains oxygen in vacuum, refills it in a pressurized
/// room, applies suffocation damage at zero oxygen, runs active buff regen, prunes expired buffs, and
/// clamps vitals. Creative Mode bypasses the simulation and keeps vitals full. Continuous vital and
/// regen writes run at <see cref="SurvivalConfig.UpdateIntervalSeconds"/> so tiny float deltas do not
/// replicate every tick.
/// </summary>
public sealed class ServerSurvivalSystem(
    SimulationStep simulationStep,
    GameplayConfig gameplayConfig,
    IBuffDatabase? buffDatabase = null
) : IServerWorldSystem
{
    private const int PRUNE_INTERVAL_TICKS = 600;
    private const float MIN_UPDATE_INTERVAL_SECONDS = 0.05f;

    private readonly Dictionary<Uuid, float> _pendingSeconds = [];
    private int _ticksSincePrune;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        float validDelta = float.IsFinite(delta) && delta > 0f ? delta : 0f;
        if (validDelta <= 0f)
        {
            return;
        }

        SurvivalAction action = new()
        {
            Owner = this,
            Delta = validDelta,
            SimTick = simulationStep.CurrentSimTick,
        };

        store.QueryRef<StatisticsComponent, GameModeComponent, OwnedCharacterComponent, SurvivalAction>(validDelta, ref action);

        if (++_ticksSincePrune >= PRUNE_INTERVAL_TICKS)
        {
            _ticksSincePrune = 0;
            PrunePending(store);
        }
    }

    private float UpdateIntervalSeconds
    {
        get
        {
            float interval = gameplayConfig.Survival.UpdateIntervalSeconds.Get();
            return float.IsFinite(interval) && interval > MIN_UPDATE_INTERVAL_SECONDS
                ? interval
                : MIN_UPDATE_INTERVAL_SECONDS;
        }
    }

    private float DefaultHealthMax => SanitizeMax(gameplayConfig.Survival.MaxHealth.Get(), StatisticIds.DEFAULT_HEALTH_MAX);

    private float DefaultOxygenMax => SanitizeMax(gameplayConfig.Survival.MaxOxygen.Get(), StatisticIds.DEFAULT_OXYGEN_MAX);

    private void UpdatePlayer(
        DataStore store,
        int entity,
        ref Ref<StatisticsComponent> stats,
        GameMode mode,
        float delta,
        long simTick
    ) {
        Uuid owner = store.GetUuid(entity);

        //  Expiry bookkeeping is cheap and must land on the exact sim tick, so it is not cadence-gated.
        StatisticsComponentExtensions.PruneExpiredBuffs(ref stats, simTick);
        StatisticsComponentExtensions.PruneExpiredCooldowns(ref stats, simTick);

        if (mode == GameMode.Creative)
        {
            if (owner != Uuid.Null)
            {
                _pendingSeconds.Remove(owner);
            }

            RestoreCreativeVitals(ref stats);
            return;
        }

        float interval = UpdateIntervalSeconds;
        float pending = delta;
        if (owner != Uuid.Null)
        {
            _pendingSeconds.TryGetValue(owner, out float accumulated);
            pending += accumulated;
            if (pending < interval)
            {
                _pendingSeconds[owner] = pending;
                return;
            }

            _pendingSeconds[owner] = 0f;
        }

        Simulate(store, entity, ref stats, pending, simTick);
    }

    private void Simulate(DataStore store, int entity, ref Ref<StatisticsComponent> stats, float elapsed, long simTick)
    {
        float healthMax = SanitizeMax(stats.Read.GetFixed(StatisticIds.HEALTH_MAX, DefaultHealthMax), DefaultHealthMax);
        float oxygenMax = SanitizeMax(stats.Read.GetFixed(StatisticIds.OXYGEN_MAX, DefaultOxygenMax), DefaultOxygenMax);

        float health = Math.Clamp(stats.Read.GetFixed(StatisticIds.HEALTH, healthMax), 0f, healthMax);
        float oxygen = Math.Clamp(stats.Read.GetFixed(StatisticIds.OXYGEN, oxygenMax), 0f, oxygenMax);

        if (ToFixed(health) != ToFixed(stats.Read.GetFixed(StatisticIds.HEALTH, healthMax)))
        {
            StatisticsComponentExtensions.SetFixed(ref stats, StatisticIds.HEALTH, health);
        }

        if (ToFixed(oxygen) != ToFixed(stats.Read.GetFixed(StatisticIds.OXYGEN, oxygenMax)))
        {
            StatisticsComponentExtensions.SetFixed(ref stats, StatisticIds.OXYGEN, oxygen);
        }

        bool pressurized = stats.Read.GetFlag(StatisticIds.PRESSURIZED);
        float oxygenRate = pressurized
            ? SanitizeRate(gameplayConfig.Survival.OxygenRefillPerSecond.Get(), 20f)
            : -SanitizeRate(gameplayConfig.Survival.OxygenDrainPerSecond.Get(), 2f);

        float nextOxygen = Math.Clamp(oxygen + (oxygenRate * elapsed), 0f, oxygenMax);

        //  Expired buffs were pruned at the tick boundary, so regen sees only active entries.
        nextOxygen = ApplyBuffRegen(ref stats, simTick, elapsed, nextOxygen, oxygenMax, healthMax);

        if (ToFixed(nextOxygen) != ToFixed(stats.Read.GetFixed(StatisticIds.OXYGEN, oxygenMax)))
        {
            StatisticsComponentExtensions.SetFixed(ref stats, StatisticIds.OXYGEN, nextOxygen);
        }

        if (nextOxygen <= 0f)
        {
            float damage = SanitizeRate(gameplayConfig.Survival.SuffocationDamagePerSecond.Get(), 5f) * elapsed;
            DamageService.ApplyDamage(store, entity, damage, DamageSource.Suffocation, simTick);
        }
    }

    private float ApplyBuffRegen(
        ref Ref<StatisticsComponent> stats,
        long simTick,
        float elapsed,
        float oxygen,
        float oxygenMax,
        float healthMax
    ) {
        if (buffDatabase == null)
        {
            return oxygen;
        }

        IReadOnlyList<Buff> buffs = buffDatabase.All;
        for (var buffIndex = 0; buffIndex < buffs.Count; buffIndex++)
        {
            Buff buff = buffs[buffIndex];
            if (!stats.Read.TryGetBuffExpiresTick(buff.ID.AsSpan(), out long expiresTick) || expiresTick <= simTick)
            {
                continue;
            }

            IReadOnlyList<BuffRegenRate> rates = buff.RegenRates;
            for (var rateIndex = 0; rateIndex < rates.Count; rateIndex++)
            {
                BuffRegenRate rate = rates[rateIndex];
                float delta = rate.PerSecond * elapsed;
                if (!float.IsFinite(delta) || delta == 0f)
                {
                    continue;
                }

                if (string.Equals(rate.StatId, StatisticIds.HEALTH, StringComparison.Ordinal))
                {
                    float health = Math.Clamp(stats.Read.GetFixed(StatisticIds.HEALTH, healthMax), 0f, healthMax);
                    if (health > 0f)
                    {
                        StatisticsComponentExtensions.SetFixed(ref stats, StatisticIds.HEALTH, Math.Clamp(health + delta, 0f, healthMax));
                    }
                }
                else if (string.Equals(rate.StatId, StatisticIds.OXYGEN, StringComparison.Ordinal))
                {
                    oxygen = Math.Clamp(oxygen + delta, 0f, oxygenMax);
                }
                else
                {
                    ApplyStatDelta(ref stats, rate.StatId, delta);
                }
            }
        }

        return oxygen;
    }

    private static void ApplyStatDelta(ref Ref<StatisticsComponent> stats, string statId, float delta)
    {
        if (StatisticIds.IsIntegerStat(statId.AsSpan()))
        {
            long current = stats.Read.GetValue(statId);
            StatisticsComponentExtensions.SetValue(ref stats, statId, current + (long)Math.Round(delta));
            return;
        }

        float value = stats.Read.GetFixed(statId);
        StatisticsComponentExtensions.SetFixed(ref stats, statId, value + delta);
    }

    private void RestoreCreativeVitals(ref Ref<StatisticsComponent> stats)
    {
        float healthMax = SanitizeMax(stats.Read.GetFixed(StatisticIds.HEALTH_MAX, DefaultHealthMax), DefaultHealthMax);
        float oxygenMax = SanitizeMax(stats.Read.GetFixed(StatisticIds.OXYGEN_MAX, DefaultOxygenMax), DefaultOxygenMax);
        float health = stats.Read.GetFixed(StatisticIds.HEALTH, healthMax);
        float oxygen = stats.Read.GetFixed(StatisticIds.OXYGEN, oxygenMax);

        if (ToFixed(health) != ToFixed(healthMax))
        {
            StatisticsComponentExtensions.SetFixed(ref stats, StatisticIds.HEALTH, healthMax);
        }

        if (ToFixed(oxygen) != ToFixed(oxygenMax))
        {
            StatisticsComponentExtensions.SetFixed(ref stats, StatisticIds.OXYGEN, oxygenMax);
        }
    }

    private void PrunePending(DataStore store)
    {
        if (_pendingSeconds.Count == 0)
        {
            return;
        }

        List<Uuid>? stale = null;
        foreach (Uuid owner in _pendingSeconds.Keys)
        {
            if (!store.TryGet(owner, out _))
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
            _pendingSeconds.Remove(stale[i]);
        }
    }

    private static long ToFixed(float value)
    {
        return StatisticIds.ToFixed(value);
    }

    private static float SanitizeMax(float value, float fallback)
    {
        return float.IsFinite(value) && value > 0f ? value : fallback;
    }

    private static float SanitizeRate(float value, float fallback)
    {
        return float.IsFinite(value) && value >= 0f ? value : fallback;
    }

    private struct SurvivalAction : IForEachRef<StatisticsComponent, GameModeComponent, OwnedCharacterComponent>
    {
        public ServerSurvivalSystem Owner;
        public float Delta;
        public long SimTick;

        public void Execute(
            float delta,
            DataStore store,
            int entity,
            ref Ref<StatisticsComponent> stats,
            ref Ref<GameModeComponent> mode,
            ref Ref<OwnedCharacterComponent> owned
        ) {
            Owner.UpdatePlayer(store, entity, ref stats, mode.Read.Mode, Delta, SimTick);
        }
    }
}
