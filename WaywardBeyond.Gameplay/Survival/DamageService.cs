using System;
using Krill;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;

namespace WaywardBeyond.Gameplay.Survival;

/// <summary>
/// The single server-side health-damage path. Every hazard routes through
/// <see cref="ApplyDamage"/> so Creative immunity, resistance mitigation, clamping, and the fatal
/// source record cannot drift between systems.
/// </summary>
public static class DamageService
{
    /// <summary>
    /// Applies <paramref name="amount"/> health damage from <paramref name="source"/> to
    /// <paramref name="entity"/>. Creative characters and characters already at zero health ignore it.
    /// </summary>
    public static DamageResult ApplyDamage(
        DataStore store,
        int entity,
        float amount,
        DamageSource source,
        long simTick = 0L
    ) {
        if (source == DamageSource.None || !float.IsFinite(amount) || amount <= 0f)
        {
            return default;
        }

        if (store.TryGet(entity, out GameModeComponent mode) && mode.Mode == GameMode.Creative)
        {
            return default;
        }

        if (!store.TryGet(entity, out StatisticsComponent stats))
        {
            return default;
        }

        float healthMax = SanitizeMax(stats.GetFixed(StatisticIds.HEALTH_MAX, StatisticIds.DEFAULT_HEALTH_MAX));
        float health = Math.Clamp(stats.GetFixed(StatisticIds.HEALTH, healthMax), 0f, healthMax);
        if (health <= 0f)
        {
            return default;
        }

        float mitigated = Mitigate(in stats, amount, source);
        if (!float.IsFinite(mitigated) || mitigated <= 0f)
        {
            return default;
        }

        float applied = Math.Min(health, mitigated);
        float nextHealth = health - applied;
        store.SetStatisticFixed(entity, StatisticIds.HEALTH, nextHealth);

        var state = store.TryGet(entity, out DamageStateComponent existing) ? existing : default;
        state.LastSource = source;
        state.LastAmount = applied;
        state.LastTick = simTick;
        if (nextHealth <= 0f)
        {
            state.DeathCause = source;
            state.DeathTick = simTick;
        }

        store.AddOrUpdate(entity, state);
        return new DamageResult(applied, nextHealth <= 0f, source);
    }

    private static float Mitigate(in StatisticsComponent stats, float amount, DamageSource source)
    {
        if (source != DamageSource.KineticImpact)
        {
            return amount;
        }

        float resistance = stats.GetFixed(StatisticIds.KINETIC_RESIST, 0f);
        if (!float.IsFinite(resistance))
        {
            return amount;
        }

        resistance = Math.Clamp(resistance, 0f, 1f);
        return amount * (1f - resistance);
    }

    private static float SanitizeMax(float value)
    {
        return float.IsFinite(value) && value > 0f ? value : StatisticIds.DEFAULT_HEALTH_MAX;
    }
}
