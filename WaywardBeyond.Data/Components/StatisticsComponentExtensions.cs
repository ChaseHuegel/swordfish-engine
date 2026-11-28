using System;
using Krill;
using WaywardBeyond.Data.Buffs;
using WaywardBeyond.Data.Statistics;

namespace WaywardBeyond.Data.Components;

/// <summary>Change-gated ECS mutation helpers over <see cref="StatisticsComponent"/>.</summary>
public static class StatisticsComponentExtensions
{
    [ThreadStatic]
    private static bool _lastActionChanged;

    [ThreadStatic]
    private static bool _lastActionExecuted;

    /// <summary>Change-gated write of a raw stat value onto <paramref name="stats"/>.</summary>
    public static bool SetValue(ref Ref<StatisticsComponent> stats, string id, long value)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        ref readonly StatisticsComponent current = ref stats.Read;
        if (current.TryGetValue(id, out long existing) && existing == value)
        {
            return false;
        }

        return stats.Write.SetValue(id, value);
    }

    /// <summary>Change-gated write of a fixed-point vital onto <paramref name="stats"/>.</summary>
    public static bool SetFixed(ref Ref<StatisticsComponent> stats, string id, float value)
    {
        return StatisticIds.TryToFixed(value, out long fixedValue) && SetValue(ref stats, id, fixedValue);
    }

    /// <summary>Change-gated write of a boolean flag onto <paramref name="stats"/>.</summary>
    public static bool SetFlag(ref Ref<StatisticsComponent> stats, string id, bool enabled)
    {
        return SetValue(ref stats, id, enabled ? 1L : 0L);
    }

    /// <summary>Change-gated write of a buff expiry tick onto <paramref name="stats"/>.</summary>
    public static bool SetBuffExpiresTick(ref Ref<StatisticsComponent> stats, string buffId, long expiresTick)
    {
        if (string.IsNullOrWhiteSpace(buffId))
        {
            return false;
        }

        ref readonly StatisticsComponent current = ref stats.Read;
        if (current.TryGetBuffExpiresTick(buffId.AsSpan(), out long existing) && existing == expiresTick)
        {
            return false;
        }

        return stats.Write.SetBuffExpiresTick(buffId, expiresTick);
    }

    /// <summary>Change-gated application of <paramref name="buff"/> onto <paramref name="stats"/>.</summary>
    public static bool ApplyBuff(ref Ref<StatisticsComponent> stats, in Buff buff, long currentSimTick, float fixedDt)
    {
        if (!stats.Read.TryComputeBuffExpiry(in buff, currentSimTick, fixedDt, out long nextExpiry, out string? expiryStatId, out ReadOnlySpan<char> fallbackBuffId))
        {
            return false;
        }

        return expiryStatId != null
            ? SetValue(ref stats, expiryStatId, nextExpiry)
            : SetBuffExpiresTick(ref stats, fallbackBuffId.ToString(), nextExpiry);
    }

    /// <summary>Change-gated removal of <paramref name="id"/> from <paramref name="stats"/>.</summary>
    public static bool Remove(ref Ref<StatisticsComponent> stats, string? id)
    {
        if (string.IsNullOrEmpty(id) || !stats.Read.TryGetValue(id, out _))
        {
            return false;
        }

        return stats.Write.Remove(id);
    }

    /// <summary>Change-gated removal of <paramref name="buffId"/> from <paramref name="stats"/> without allocating on miss.</summary>
    public static bool RemoveBuff(ref Ref<StatisticsComponent> stats, ReadOnlySpan<char> buffId)
    {
        return stats.Read.TryGetBuffExpiresTick(buffId, out _) && stats.Write.RemoveBuff(buffId);
    }

    /// <summary>Change-gated pruning of expired buffs (<c>&lt;= currentSimTick</c>) from <paramref name="stats"/>.</summary>
    public static bool PruneExpiredBuffs(ref Ref<StatisticsComponent> stats, long currentSimTick)
    {
        return stats.Read.HasExpiredBuffs(currentSimTick) && stats.Write.PruneExpiredBuffs(currentSimTick);
    }

    /// <summary>Change-gated pruning of ready consumable cooldowns (<c>&lt;= currentSimTick</c>) from <paramref name="stats"/>.</summary>
    public static bool PruneExpiredCooldowns(ref Ref<StatisticsComponent> stats, long currentSimTick)
    {
        return stats.Read.HasExpiredCooldowns(currentSimTick) && stats.Write.PruneExpiredCooldowns(currentSimTick);
    }

    /// <summary>Change-gated write of a raw stat onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool SetStatistic(this DataStore store, int entity, string id, long value)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var action = new SetValueAction(id, value);
        if (RunSingleEntityAction(store, entity, ref action, out bool changed))
        {
            return changed;
        }

        var created = new StatisticsComponent();
        created.SetValue(id, value);
        store.AddOrUpdate(entity, created);
        return true;
    }

    /// <summary>Change-gated write of a fixed-point vital onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool SetStatisticFixed(this DataStore store, int entity, string id, float value)
    {
        return StatisticIds.TryToFixed(value, out long fixedValue) && store.SetStatistic(entity, id, fixedValue);
    }

    /// <summary>Change-gated write of a boolean flag onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool SetStatisticFlag(this DataStore store, int entity, string id, bool enabled)
    {
        return store.SetStatistic(entity, id, enabled ? 1L : 0L);
    }

    /// <summary>Change-gated write of a buff expiry tick onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool SetBuffExpiresTick(this DataStore store, int entity, string buffId, long expiresTick)
    {
        if (string.IsNullOrWhiteSpace(buffId))
        {
            return false;
        }

        var action = new SetBuffAction(buffId, expiresTick);
        if (RunSingleEntityAction(store, entity, ref action, out bool changed))
        {
            return changed;
        }

        var created = new StatisticsComponent();
        created.SetBuffExpiresTick(buffId, expiresTick);
        store.AddOrUpdate(entity, created);
        return true;
    }

    /// <summary>Change-gated application of <paramref name="buff"/> onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool ApplyBuff(this DataStore store, int entity, in Buff buff, long currentSimTick, float fixedDt)
    {
        if (string.IsNullOrWhiteSpace(buff.ExpiryStatId) && string.IsNullOrWhiteSpace(buff.ID))
        {
            return false;
        }

        var action = new ApplyBuffAction(in buff, currentSimTick, fixedDt);
        if (RunSingleEntityAction(store, entity, ref action, out bool changed))
        {
            return changed;
        }

        var created = new StatisticsComponent();
        if (!created.ApplyBuff(in buff, currentSimTick, fixedDt))
        {
            return false;
        }

        store.AddOrUpdate(entity, created);
        return true;
    }

    /// <summary>Change-gated removal of <paramref name="id"/> from <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool RemoveStatistic(this DataStore store, int entity, string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        var action = new RemoveValueAction(id);
        return RunSingleEntityAction(store, entity, ref action, out bool changed) && changed;
    }

    /// <summary>Change-gated removal of <paramref name="buffId"/> from <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool RemoveBuff(this DataStore store, int entity, string buffId)
    {
        if (string.IsNullOrWhiteSpace(buffId))
        {
            return false;
        }

        var action = new RemoveBuffAction(buffId);
        return RunSingleEntityAction(store, entity, ref action, out bool changed) && changed;
    }

    /// <summary>Change-gated pruning of expired buffs on <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool PruneExpiredBuffs(this DataStore store, int entity, long currentSimTick)
    {
        var action = new PruneBuffsAction(currentSimTick);
        return RunSingleEntityAction(store, entity, ref action, out bool changed) && changed;
    }

    //  Krill's single-entity QueryRef copies struct actions by value into a local, so mutations report back through TLS.
    private static bool RunSingleEntityAction<TAction>(DataStore store, int entity, ref TAction action, out bool changed)
        where TAction : struct, IForEachRef<StatisticsComponent>
    {
        _lastActionExecuted = false;
        _lastActionChanged = false;
        store.QueryRef<StatisticsComponent, TAction>(entity, 0f, ref action);
        changed = _lastActionChanged;
        return _lastActionExecuted;
    }

    private readonly struct SetValueAction(string id, long value) : IForEachRef<StatisticsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> component)
        {
            _lastActionExecuted = true;
            _lastActionChanged = SetValue(ref component, id, value);
        }
    }

    private readonly struct SetBuffAction(string buffId, long expiresTick) : IForEachRef<StatisticsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> component)
        {
            _lastActionExecuted = true;
            _lastActionChanged = SetBuffExpiresTick(ref component, buffId, expiresTick);
        }
    }

    private readonly struct ApplyBuffAction(in Buff buff, long currentSimTick, float fixedDt) : IForEachRef<StatisticsComponent>
    {
        private readonly Buff _buff = buff;

        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> component)
        {
            _lastActionExecuted = true;
            _lastActionChanged = ApplyBuff(ref component, in _buff, currentSimTick, fixedDt);
        }
    }

    private readonly struct RemoveValueAction(string id) : IForEachRef<StatisticsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> component)
        {
            _lastActionExecuted = true;
            _lastActionChanged = Remove(ref component, id);
        }
    }

    private readonly struct RemoveBuffAction(string buffId) : IForEachRef<StatisticsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> component)
        {
            _lastActionExecuted = true;
            _lastActionChanged = RemoveBuff(ref component, buffId.AsSpan());
        }
    }

    private readonly struct PruneBuffsAction(long currentSimTick) : IForEachRef<StatisticsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> component)
        {
            _lastActionExecuted = true;
            _lastActionChanged = PruneExpiredBuffs(ref component, currentSimTick);
        }
    }
}
