using System;
using System.Collections.Generic;
using Krill;
using WaywardBeyond.Data.Buffs;

namespace WaywardBeyond.Data.Components;

/// <summary>Change-gated write and persistence helpers over <see cref="StatisticsComponent"/>.</summary>
public static class StatisticsComponentExtensions
{
    [ThreadStatic]
    private static bool _lastActionChanged;

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
        return StatIds.TryToFixed(value, out long fixedValue) && SetValue(ref stats, id, fixedValue);
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
    public static bool ApplyBuff(ref Ref<StatisticsComponent> stats, in BuffDefinition buff, long currentSimTick, float fixedDt)
    {
        if (string.IsNullOrWhiteSpace(buff.ExpiryStatId))
        {
            return false;
        }

        ref readonly StatisticsComponent current = ref stats.Read;
        long existingExpiry = current.TryGetValue(buff.ExpiryStatId, out long found) ? found : 0L;
        if (!buff.TryComputeNextExpiresTick(existingExpiry, currentSimTick, fixedDt, out long nextExpiry))
        {
            return false;
        }

        return SetValue(ref stats, buff.ExpiryStatId, nextExpiry);
    }

    /// <summary>Change-gated removal of <paramref name="id"/> from <paramref name="stats"/>.</summary>
    public static bool Remove(ref Ref<StatisticsComponent> stats, string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        if (!stats.Read.TryGetValue(id, out _))
        {
            return false;
        }

        return stats.Write.Remove(id);
    }

    /// <summary>Change-gated removal of <paramref name="buffId"/> from <paramref name="stats"/> without allocating on miss.</summary>
    public static bool RemoveBuff(ref Ref<StatisticsComponent> stats, ReadOnlySpan<char> buffId)
    {
        if (!stats.Read.TryGetBuffExpiresTick(buffId, out _))
        {
            return false;
        }

        return stats.Write.RemoveBuff(buffId);
    }

    /// <summary>Change-gated pruning of expired buffs (<c>&lt;= currentSimTick</c>) from <paramref name="stats"/>.</summary>
    public static bool PruneExpiredBuffs(ref Ref<StatisticsComponent> stats, long currentSimTick)
    {
        Statistic[]? entries = stats.Read.Entries;
        if (entries == null || entries.Length == 0)
        {
            return false;
        }

        var hasExpired = false;
        for (var i = 0; i < entries.Length; i++)
        {
            string? entryId = entries[i].ID;
            if (entryId != null && StatIds.IsBuffExpiresTickKey(entryId.AsSpan()) && entries[i].Value <= currentSimTick)
            {
                hasExpired = true;
                break;
            }
        }

        return hasExpired && stats.Write.PruneExpiredBuffs(currentSimTick);
    }

    /// <summary>Change-gated write of a raw stat onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool SetStatistic(this DataStore store, int entity, string id, long value)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (!store.TryGet(entity, out StatisticsComponent _))
        {
            var created = new StatisticsComponent();
            created.SetValue(id, value);
            store.AddOrUpdate(entity, created);
            return true;
        }

        var action = new SetValueAction(id, value);
        return RunSingleEntityAction(store, entity, ref action);
    }

    /// <summary>Change-gated write of a fixed-point vital onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool SetStatisticFixed(this DataStore store, int entity, string id, float value)
    {
        return StatIds.TryToFixed(value, out long fixedValue) && store.SetStatistic(entity, id, fixedValue);
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

        if (!store.TryGet(entity, out StatisticsComponent _))
        {
            var created = new StatisticsComponent();
            created.SetBuffExpiresTick(buffId, expiresTick);
            store.AddOrUpdate(entity, created);
            return true;
        }

        var action = new SetBuffAction(buffId, expiresTick);
        return RunSingleEntityAction(store, entity, ref action);
    }

    /// <summary>Change-gated application of <paramref name="buff"/> onto <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool ApplyBuff(this DataStore store, int entity, in BuffDefinition buff, long currentSimTick, float fixedDt)
    {
        if (string.IsNullOrWhiteSpace(buff.ExpiryStatId))
        {
            return false;
        }

        if (!store.Has(typeof(StatisticsComponent), entity))
        {
            var created = new StatisticsComponent();
            if (!created.ApplyBuff(in buff, currentSimTick, fixedDt))
            {
                return false;
            }

            store.AddOrUpdate(entity, created);
            return true;
        }

        var action = new ApplyBuffAction(in buff, currentSimTick, fixedDt);
        return RunSingleEntityAction(store, entity, ref action);
    }

    /// <summary>Change-gated removal of <paramref name="id"/> from <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool RemoveStatistic(this DataStore store, int entity, string id)
    {
        if (string.IsNullOrEmpty(id) || !store.Has(typeof(StatisticsComponent), entity))
        {
            return false;
        }

        var action = new RemoveValueAction(id);
        return RunSingleEntityAction(store, entity, ref action);
    }

    /// <summary>Change-gated removal of <paramref name="buffId"/> from <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool RemoveBuff(this DataStore store, int entity, string buffId)
    {
        if (string.IsNullOrWhiteSpace(buffId) || !store.Has(typeof(StatisticsComponent), entity))
        {
            return false;
        }

        var action = new RemoveBuffAction(buffId);
        return RunSingleEntityAction(store, entity, ref action);
    }

    /// <summary>Change-gated pruning of expired buffs on <paramref name="entity"/> in <paramref name="store"/>.</summary>
    public static bool PruneExpiredBuffs(this DataStore store, int entity, long currentSimTick)
    {
        if (!store.Has(typeof(StatisticsComponent), entity))
        {
            return false;
        }

        var action = new PruneBuffsAction(currentSimTick);
        return RunSingleEntityAction(store, entity, ref action);
    }

    // Krill's QueryRef copies struct actions by value into a local, so mutations report back through TLS.
    private static bool RunSingleEntityAction<TAction>(DataStore store, int entity, ref TAction action)
        where TAction : struct, IForEachRef<StatisticsComponent>
    {
        _lastActionChanged = false;
        store.QueryRef<StatisticsComponent, TAction>(entity, 0f, ref action);
        return _lastActionChanged;
    }

    /// <summary>Captures surviving server-persisted stats from <paramref name="stats"/> for per-level save storage.</summary>
    public static Statistic[]? CapturePersistedServerStatistics(in StatisticsComponent stats, long currentSimTick)
    {
        Statistic[]? entries = stats.Entries;
        if (entries == null || entries.Length == 0)
        {
            return null;
        }

        var count = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            if (IsServerPersistedEntry(in entries[i], currentSimTick))
            {
                count++;
            }
        }

        if (count == 0)
        {
            return null;
        }

        var result = new Statistic[count];
        var writeIndex = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            if (IsServerPersistedEntry(in entries[i], currentSimTick))
            {
                result[writeIndex++] = entries[i];
            }
        }

        return result;
    }

    /// <summary>Merges durable replicated stats from <paramref name="stats"/> into a character's saved statistics array.</summary>
    public static Statistic[]? MergeClientSavedStatistics(Statistic[]? existing, in StatisticsComponent stats)
    {
        Statistic[]? source = stats.Entries;
        if (source == null || source.Length == 0)
        {
            return StripTransientCharacterEntries(existing);
        }

        var merged = new List<Statistic>(existing != null ? existing.Length + source.Length : source.Length);
        if (existing != null)
        {
            for (var i = 0; i < existing.Length; i++)
            {
                Statistic entry = existing[i];
                if (string.IsNullOrWhiteSpace(entry.ID) || IsTransientClientStat(entry.ID))
                {
                    continue;
                }

                merged.Add(entry);
            }
        }

        for (var i = 0; i < source.Length; i++)
        {
            Statistic entry = source[i];
            if (string.IsNullOrWhiteSpace(entry.ID)
                || !entry.ID.StartsWith(StatIds.NAMESPACE_PREFIX, StringComparison.Ordinal)
                || IsTransientClientStat(entry.ID))
            {
                continue;
            }

            SetInList(merged, entry);
        }

        return merged.Count == 0 ? null : [.. merged];
    }

    /// <summary>Seeds S.P.A.C.E.R. attributes into a join-request statistic array when not already present.</summary>
    public static Statistic[]? WithCharacterAttributes(in Character character)
    {
        Statistic[]? existing = character.Statistics;
        var merged = new List<Statistic>(existing != null ? existing.Length + 6 : 6);
        if (existing != null)
        {
            for (var i = 0; i < existing.Length; i++)
            {
                Statistic entry = existing[i];
                if (string.IsNullOrWhiteSpace(entry.ID) || IsTransientClientStat(entry.ID))
                {
                    continue;
                }

                merged.Add(entry);
            }
        }

        AddAttributeIfMissing(merged, StatIds.ATTR_STRENGTH, character.Strength);
        AddAttributeIfMissing(merged, StatIds.ATTR_PERCEPTION, character.Precision);
        AddAttributeIfMissing(merged, StatIds.ATTR_AGILITY, character.Awareness);
        AddAttributeIfMissing(merged, StatIds.ATTR_CONSTITUTION, character.Charisma);
        AddAttributeIfMissing(merged, StatIds.ATTR_ENGINEERING, character.Education);
        AddAttributeIfMissing(merged, StatIds.ATTR_RESILIENCE, character.Resolve);

        return merged.Count == 0 ? null : [.. merged];
    }

    private static bool IsServerPersistedEntry(in Statistic entry, long currentSimTick)
    {
        if (string.IsNullOrWhiteSpace(entry.ID))
        {
            return false;
        }

        if (string.Equals(entry.ID, StatIds.PRESSURIZED, StringComparison.Ordinal))
        {
            return false;
        }

        if (StatIds.IsBuffExpiresTickKey(entry.ID.AsSpan()))
        {
            return entry.Value > currentSimTick;
        }

        return true;
    }

    private static bool IsTransientClientStat(string id)
    {
        return string.Equals(id, StatIds.PRESSURIZED, StringComparison.Ordinal)
            || StatIds.IsBuffExpiresTickKey(id.AsSpan());
    }

    private static Statistic[]? StripTransientCharacterEntries(Statistic[]? existing)
    {
        if (existing == null || existing.Length == 0)
        {
            return existing;
        }

        var hasTransient = false;
        for (var i = 0; i < existing.Length; i++)
        {
            string? id = existing[i].ID;
            if (id != null && IsTransientClientStat(id))
            {
                hasTransient = true;
                break;
            }
        }

        if (!hasTransient)
        {
            return existing;
        }

        var filtered = new List<Statistic>(existing.Length);
        for (var i = 0; i < existing.Length; i++)
        {
            string? id = existing[i].ID;
            if (id != null && !IsTransientClientStat(id))
            {
                filtered.Add(existing[i]);
            }
        }

        return filtered.Count == 0 ? null : [.. filtered];
    }

    private static void AddAttributeIfMissing(List<Statistic> list, string id, int value)
    {
        if (value <= 0)
        {
            return;
        }

        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].ID, id, StringComparison.Ordinal))
            {
                return;
            }
        }

        list.Add(new Statistic(id, value));
    }

    private static void SetInList(List<Statistic> list, in Statistic entry)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].ID, entry.ID, StringComparison.Ordinal))
            {
                list[i] = entry;
                return;
            }
        }

        list.Add(entry);
    }

    private readonly struct SetValueAction(string id, long value) : IForEachRef<StatisticsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> component)
        {
            _lastActionChanged = SetValue(ref component, id, value);
        }
    }

    private readonly struct SetBuffAction(string buffId, long expiresTick) : IForEachRef<StatisticsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> component)
        {
            _lastActionChanged = SetBuffExpiresTick(ref component, buffId, expiresTick);
        }
    }

    private readonly struct ApplyBuffAction(in BuffDefinition buff, long currentSimTick, float fixedDt) : IForEachRef<StatisticsComponent>
    {
        private readonly BuffDefinition _buff = buff;

        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> component)
        {
            _lastActionChanged = ApplyBuff(ref component, in _buff, currentSimTick, fixedDt);
        }
    }

    private readonly struct RemoveValueAction(string id) : IForEachRef<StatisticsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> component)
        {
            _lastActionChanged = Remove(ref component, id);
        }
    }

    private readonly struct RemoveBuffAction(string buffId) : IForEachRef<StatisticsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> component)
        {
            _lastActionChanged = RemoveBuff(ref component, buffId.AsSpan());
        }
    }

    private readonly struct PruneBuffsAction(long currentSimTick) : IForEachRef<StatisticsComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<StatisticsComponent> component)
        {
            _lastActionChanged = PruneExpiredBuffs(ref component, currentSimTick);
        }
    }
}
