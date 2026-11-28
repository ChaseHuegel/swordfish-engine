using System;
using System.Collections.Generic;
using Krill;
using Swordfish.Library.Util;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Buffs;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Statistics;
using WaywardBeyond.Items;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-authoritative stat aggregation. Recomputes derived player statistics on join, wearable or
/// augment slot changes, and active buff additions or expirations using deterministic layering
/// (<c>Base + S.P.A.C.E.R. + Wearables + Augments + Active Buffs</c>), and clamps current health and
/// oxygen when maximums drop.
/// </summary>
public sealed class ServerStatAggregationSystem(
    SimulationStep simulationStep,
    IItemDatabase? itemDatabase = null,
    IBuffDatabase? buffDatabase = null,
    GameplayConfig? gameplayConfig = null
) : IServerWorldSystem
{
    private const ulong FNV_OFFSET_BASIS = 14695981039346656037UL;
    private const ulong FNV_PRIME = 1099511628211UL;

    private readonly Dictionary<string, StatAccumulator> _derivedAccumulators = new(StringComparer.Ordinal);
    private readonly HashSet<string> _baseEntryKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeModifierKeys = new(StringComparer.Ordinal);
    private readonly List<string> _nextDerivedKeys = [];
    private readonly List<string> _orderedKeys = [];

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        AggregateAction action = new()
        {
            Owner = this,
            SimTick = simulationStep.CurrentSimTick,
        };

        store.QueryRef<StatisticsComponent, OwnedCharacterComponent, AggregateAction>(delta, ref action);
    }

    private float DefaultHealthMax => SanitizeMax(gameplayConfig?.Survival.MaxHealth.Get() ?? StatisticIds.DEFAULT_HEALTH_MAX, StatisticIds.DEFAULT_HEALTH_MAX);

    private float DefaultOxygenMax => SanitizeMax(gameplayConfig?.Survival.MaxOxygen.Get() ?? StatisticIds.DEFAULT_OXYGEN_MAX, StatisticIds.DEFAULT_OXYGEN_MAX);

    private static float MaxVitalUserUnits => StatisticIds.MAX_VITAL_FIXED / (float)StatisticIds.FIXED_POINT_SCALE;

    private void AggregatePlayer(DataStore store, int entity, ref Ref<StatisticsComponent> stats, long simTick)
    {
        bool hasBase = store.TryGet(entity, out BaseStatisticsComponent baseStats);
        if (!hasBase)
        {
            baseStats = CaptureInitialBase(in stats.Read);
        }

        bool hasInventory = store.TryGet(entity, out InventoryComponent inventory);
        bool invDirty = !hasBase || store.IsDirty<InventoryComponent>(entity);
        bool statsDirty = !hasBase || store.IsDirty<StatisticsComponent>(entity);
        bool buffTimerDue = hasBase && simTick >= baseStats.NextBuffExpiryTick;

        if (hasBase && !invDirty && !statsDirty && !buffTimerDue)
        {
            return;
        }

        ulong gearHash = invDirty
            ? (hasInventory ? ComputeGearHash(in inventory) : FNV_OFFSET_BASIS)
            : baseStats.GearHash;

        (ulong buffHash, long nextBuffExpiryTick) = statsDirty || buffTimerDue
            ? ComputeBuffState(in stats.Read, simTick)
            : (baseStats.BuffHash, baseStats.NextBuffExpiryTick);

        if (hasBase
            && gearHash == baseStats.GearHash
            && buffHash == baseStats.BuffHash
            && !buffTimerDue)
        {
            return;
        }

        Recompute(ref stats, in baseStats, hasInventory ? inventory : default, hasInventory, simTick);

        baseStats.GearHash = gearHash;
        baseStats.BuffHash = buffHash;
        baseStats.NextBuffExpiryTick = nextBuffExpiryTick;
        baseStats.DerivedKeys = _nextDerivedKeys.Count == 0 ? null : [.. _nextDerivedKeys];
        store.AddOrUpdate(entity, baseStats);
    }

    private void Recompute(
        ref Ref<StatisticsComponent> stats,
        in BaseStatisticsComponent baseStats,
        in InventoryComponent inventory,
        bool hasInventory,
        long simTick
    )
    {
        //  Pass 1: S.P.A.C.E.R. attributes (Base + Wearables + Augments + Active Buffs).
        Span<StatAccumulator> attrAccumulators = stackalloc StatAccumulator[StatisticIds.BaseAttributeIds.Length];
        for (var i = 0; i < StatisticIds.BaseAttributeIds.Length; i++)
        {
            string attrId = StatisticIds.BaseAttributeIds[i];
            long baseAttr = baseStats.TryGetValue(attrId, out long savedAttr)
                ? Math.Max(StatisticIds.MIN_ATTRIBUTE, savedAttr)
                : StatisticIds.MIN_ATTRIBUTE;
            attrAccumulators[i] = new StatAccumulator(baseAttr);
        }

        if (hasInventory)
        {
            ApplyEquipAttributes(in inventory, SlotGroups.WEARABLES, attrAccumulators);
            ApplyEquipAttributes(in inventory, SlotGroups.AUGMENTS, attrAccumulators);
        }

        ApplyBuffAttributes(in stats.Read, simTick, attrAccumulators);

        Span<long> resolvedAttributes = stackalloc long[StatisticIds.BaseAttributeIds.Length];
        for (var i = 0; i < StatisticIds.BaseAttributeIds.Length; i++)
        {
            long resolved = Math.Max(StatisticIds.MIN_ATTRIBUTE, (long)Math.Round(attrAccumulators[i].Resolve()));
            resolvedAttributes[i] = resolved;
            StatisticsComponentExtensions.SetValue(ref stats, StatisticIds.BaseAttributeIds[i], resolved);
        }

        //  Pass 2: Derived stats (Base + S.P.A.C.E.R. Derived Bonuses + Wearables + Augments + Active Buffs).
        _derivedAccumulators.Clear();
        _baseEntryKeys.Clear();
        _activeModifierKeys.Clear();
        _nextDerivedKeys.Clear();
        _orderedKeys.Clear();

        SeedDerivedBase(in baseStats);

        ApplySpacerDerivedBonuses(resolvedAttributes);

        if (hasInventory)
        {
            ApplyEquipDerived(in inventory, SlotGroups.WEARABLES);
            ApplyEquipDerived(in inventory, SlotGroups.AUGMENTS);
        }

        ApplyBuffDerived(in stats.Read, simTick);

        foreach (string key in _derivedAccumulators.Keys)
        {
            _orderedKeys.Add(key);
        }

        _orderedKeys.Sort(StringComparer.Ordinal);

        for (var i = 0; i < _orderedKeys.Count; i++)
        {
            string statId = _orderedKeys[i];
            float resolved = _derivedAccumulators[statId].Resolve();
            resolved = ClampResolvedStat(statId, resolved);

            bool isAlwaysPresent = IsCoreDerivedStat(statId) || _baseEntryKeys.Contains(statId);
            if (!isAlwaysPresent && !_activeModifierKeys.Contains(statId))
            {
                StatisticsComponentExtensions.Remove(ref stats, statId);
                continue;
            }

            if (StatisticIds.IsIntegerStat(statId.AsSpan()))
            {
                long intValue = float.IsFinite(resolved) ? (long)Math.Round(resolved) : 0L;
                StatisticsComponentExtensions.SetValue(ref stats, statId, intValue);
            }
            else
            {
                StatisticsComponentExtensions.SetFixed(ref stats, statId, resolved);
            }

            if (!isAlwaysPresent)
            {
                _nextDerivedKeys.Add(statId);
            }
        }

        //  Pass 3: Clamp current vitals to new maximums without granting free heals.
        float healthMax = SanitizeMax(stats.Read.GetFixed(StatisticIds.HEALTH_MAX, DefaultHealthMax), DefaultHealthMax);
        float oxygenMax = SanitizeMax(stats.Read.GetFixed(StatisticIds.OXYGEN_MAX, DefaultOxygenMax), DefaultOxygenMax);
        ClampVital(ref stats, StatisticIds.HEALTH, healthMax);
        ClampVital(ref stats, StatisticIds.OXYGEN, oxygenMax);
    }

    private void SeedDerivedBase(in BaseStatisticsComponent baseStats)
    {
        float baseHealthMax = baseStats.TryGetFixed(StatisticIds.HEALTH_MAX, out float hMax)
            ? SanitizeMax(hMax, DefaultHealthMax)
            : DefaultHealthMax;
        float baseOxygenMax = baseStats.TryGetFixed(StatisticIds.OXYGEN_MAX, out float oMax)
            ? SanitizeMax(oMax, DefaultOxygenMax)
            : DefaultOxygenMax;
        float baseKineticResist = baseStats.TryGetFixed(StatisticIds.KINETIC_RESIST, out float kRes) && float.IsFinite(kRes) ? kRes : 0f;
        float baseMoveSpeedPct = baseStats.TryGetFixed(StatisticIds.MOVE_SPEED_PCT, out float mPct) && float.IsFinite(mPct) ? mPct : 0f;
        float baseMiningSpeed = baseStats.TryGetFixed(StatisticIds.MINING_SPEED, out float mSpd) && float.IsFinite(mSpd) ? mSpd : 0f;

        _derivedAccumulators[StatisticIds.HEALTH_MAX] = new StatAccumulator(baseHealthMax);
        _derivedAccumulators[StatisticIds.OXYGEN_MAX] = new StatAccumulator(baseOxygenMax);
        _derivedAccumulators[StatisticIds.KINETIC_RESIST] = new StatAccumulator(baseKineticResist);
        _derivedAccumulators[StatisticIds.MOVE_SPEED_PCT] = new StatAccumulator(baseMoveSpeedPct);
        _derivedAccumulators[StatisticIds.MINING_SPEED] = new StatAccumulator(baseMiningSpeed);

        if (baseStats.Entries != null)
        {
            for (var i = 0; i < baseStats.Entries.Length; i++)
            {
                Statistic entry = baseStats.Entries[i];
                if (string.IsNullOrWhiteSpace(entry.ID)
                    || IsBaseAttribute(entry.ID)
                    || !StatisticIds.IsAggregatableStat(entry.ID.AsSpan()))
                {
                    continue;
                }

                _baseEntryKeys.Add(entry.ID);
                if (!_derivedAccumulators.ContainsKey(entry.ID))
                {
                    float value = StatisticIds.IsIntegerStat(entry.ID.AsSpan())
                        ? entry.Value
                        : StatisticIds.FromFixed(entry.Value);
                    _derivedAccumulators[entry.ID] = new StatAccumulator(value);
                }
            }
        }

        if (baseStats.DerivedKeys != null)
        {
            for (var i = 0; i < baseStats.DerivedKeys.Length; i++)
            {
                string key = baseStats.DerivedKeys[i];
                if (!string.IsNullOrWhiteSpace(key)
                    && !IsBaseAttribute(key)
                    && !_derivedAccumulators.ContainsKey(key))
                {
                    _derivedAccumulators[key] = new StatAccumulator(0f);
                }
            }
        }
    }

    private void ApplySpacerDerivedBonuses(ReadOnlySpan<long> attributes)
    {
        long strength = attributes[0];
        long precision = attributes[1];
        long awareness = attributes[2];
        long charisma = attributes[3];
        long education = attributes[4];
        long resolve = attributes[5];

        ApplySpacerBonus(StatisticIds.HEALTH_MAX, SpacerScaling.ComputeDerivedBonus(StatisticIds.HEALTH_MAX, strength, precision, awareness, charisma, education, resolve));
        ApplySpacerBonus(StatisticIds.OXYGEN_MAX, SpacerScaling.ComputeDerivedBonus(StatisticIds.OXYGEN_MAX, strength, precision, awareness, charisma, education, resolve));
        ApplySpacerBonus(StatisticIds.KINETIC_RESIST, SpacerScaling.ComputeDerivedBonus(StatisticIds.KINETIC_RESIST, strength, precision, awareness, charisma, education, resolve));
        ApplySpacerBonus(StatisticIds.MOVE_SPEED_PCT, SpacerScaling.ComputeDerivedBonus(StatisticIds.MOVE_SPEED_PCT, strength, precision, awareness, charisma, education, resolve));
        ApplySpacerBonus(StatisticIds.MINING_SPEED, SpacerScaling.ComputeDerivedBonus(StatisticIds.MINING_SPEED, strength, precision, awareness, charisma, education, resolve));
    }

    private void ApplySpacerBonus(string statId, float bonus)
    {
        if (bonus == 0f || !float.IsFinite(bonus))
        {
            return;
        }

        StatAccumulator acc = _derivedAccumulators[statId];
        acc.Apply(StatModifier.AddBase(bonus));
        _derivedAccumulators[statId] = acc;
        _activeModifierKeys.Add(statId);
    }

    private void ApplyEquipAttributes(in InventoryComponent inventory, string groupId, Span<StatAccumulator> attrAccumulators)
    {
        if (itemDatabase == null || !inventory.Layout.TryGetGroup(groupId, out SlotGroupRange range))
        {
            return;
        }

        for (var i = 0; i < range.Capacity; i++)
        {
            ItemData stack = inventory.Get(new SlotId(groupId, i));
            if (stack.IsEmpty())
            {
                continue;
            }

            Result<Item> itemResult = itemDatabase.Get(stack.ID);
            if (!itemResult.Success || itemResult.Value.Equip is not { StatModifiers: { Count: > 0 } modifiers })
            {
                continue;
            }

            for (var m = 0; m < modifiers.Count; m++)
            {
                EquipStatModifierDefinition mod = modifiers[m];
                if (string.IsNullOrWhiteSpace(mod.StatId))
                {
                    continue;
                }

                int attrIndex = IndexOfBaseAttribute(mod.StatId);
                if (attrIndex >= 0)
                {
                    ApplyDeltaToAccumulator(ref attrAccumulators[attrIndex], mod.FlatDelta, mod.PercentDelta);
                }
            }
        }
    }

    private void ApplyEquipDerived(in InventoryComponent inventory, string groupId)
    {
        if (itemDatabase == null || !inventory.Layout.TryGetGroup(groupId, out SlotGroupRange range))
        {
            return;
        }

        for (var i = 0; i < range.Capacity; i++)
        {
            ItemData stack = inventory.Get(new SlotId(groupId, i));
            if (stack.IsEmpty())
            {
                continue;
            }

            Result<Item> itemResult = itemDatabase.Get(stack.ID);
            if (!itemResult.Success || itemResult.Value.Equip is not { StatModifiers: { Count: > 0 } modifiers })
            {
                continue;
            }

            for (var m = 0; m < modifiers.Count; m++)
            {
                EquipStatModifierDefinition mod = modifiers[m];
                ApplyNonAttributeModifier(mod.StatId, mod.FlatDelta, mod.PercentDelta);
            }
        }
    }

    private void ApplyBuffAttributes(in StatisticsComponent stats, long simTick, Span<StatAccumulator> attrAccumulators)
    {
        if (buffDatabase == null)
        {
            return;
        }

        IReadOnlyList<Buff> buffs = buffDatabase.All;
        for (var b = 0; b < buffs.Count; b++)
        {
            Buff buff = buffs[b];
            if (buff.StatModifiers.Count == 0
                || !stats.TryGetBuffExpiresTick(buff.ID.AsSpan(), out long expiresTick)
                || expiresTick <= simTick)
            {
                continue;
            }

            for (var m = 0; m < buff.StatModifiers.Count; m++)
            {
                BuffStatModifier mod = buff.StatModifiers[m];
                int attrIndex = IndexOfBaseAttribute(mod.StatId);
                if (attrIndex >= 0)
                {
                    ApplyDeltaToAccumulator(ref attrAccumulators[attrIndex], mod.FlatDelta, mod.PercentDelta);
                }
            }
        }
    }

    private void ApplyBuffDerived(in StatisticsComponent stats, long simTick)
    {
        if (buffDatabase == null)
        {
            return;
        }

        IReadOnlyList<Buff> buffs = buffDatabase.All;
        for (var b = 0; b < buffs.Count; b++)
        {
            Buff buff = buffs[b];
            if (buff.StatModifiers.Count == 0
                || !stats.TryGetBuffExpiresTick(buff.ID.AsSpan(), out long expiresTick)
                || expiresTick <= simTick)
            {
                continue;
            }

            for (var m = 0; m < buff.StatModifiers.Count; m++)
            {
                BuffStatModifier mod = buff.StatModifiers[m];
                ApplyNonAttributeModifier(mod.StatId, mod.FlatDelta, mod.PercentDelta);
            }
        }
    }

    private void ApplyNonAttributeModifier(string? rawStatId, float? flatDelta, float? percentDelta)
    {
        if (string.IsNullOrWhiteSpace(rawStatId))
        {
            return;
        }

        string statId = rawStatId.Trim();
        if (IsBaseAttribute(statId) || !StatisticIds.IsAggregatableStat(statId.AsSpan()))
        {
            return;
        }

        if (!_derivedAccumulators.TryGetValue(statId, out StatAccumulator acc))
        {
            acc = new StatAccumulator(0f);
        }

        if (ApplyDeltaToAccumulator(ref acc, flatDelta, percentDelta))
        {
            _derivedAccumulators[statId] = acc;
            _activeModifierKeys.Add(statId);
        }
    }

    private static bool ApplyDeltaToAccumulator(ref StatAccumulator acc, float? flatDelta, float? percentDelta)
    {
        var applied = false;
        if (flatDelta.HasValue && float.IsFinite(flatDelta.Value) && flatDelta.Value != 0f)
        {
            acc.Apply(StatModifier.AddBase(flatDelta.Value));
            applied = true;
        }

        if (percentDelta.HasValue && float.IsFinite(percentDelta.Value) && percentDelta.Value != 0f)
        {
            acc.Apply(StatModifier.AddPercent(percentDelta.Value));
            applied = true;
        }

        return applied;
    }

    private BaseStatisticsComponent CaptureInitialBase(in StatisticsComponent stats)
    {
        var entries = new List<Statistic>((stats.Count) + 2 + StatisticIds.BaseAttributeIds.Length);
        if (stats.Entries != null)
        {
            for (var i = 0; i < stats.Entries.Length; i++)
            {
                Statistic entry = stats.Entries[i];
                if (!string.IsNullOrWhiteSpace(entry.ID) && StatisticIds.IsAggregatableStat(entry.ID.AsSpan()))
                {
                    StatisticsPersistence.UpsertEntry(entries, in entry);
                }
            }
        }

        EnsureDefaultEntry(entries, StatisticIds.HEALTH_MAX, StatisticIds.ToFixed(DefaultHealthMax));
        EnsureDefaultEntry(entries, StatisticIds.OXYGEN_MAX, StatisticIds.ToFixed(DefaultOxygenMax));
        for (var i = 0; i < StatisticIds.BaseAttributeIds.Length; i++)
        {
            EnsureDefaultEntry(entries, StatisticIds.BaseAttributeIds[i], StatisticIds.MIN_ATTRIBUTE);
        }

        return new BaseStatisticsComponent
        {
            Entries = [.. entries],
            NextBuffExpiryTick = long.MaxValue,
        };
    }

    private static void EnsureDefaultEntry(List<Statistic> entries, string statId, long defaultValue)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].ID, statId, StringComparison.Ordinal))
            {
                return;
            }
        }

        entries.Add(new Statistic(statId, defaultValue));
    }

    private static void ClampVital(ref Ref<StatisticsComponent> stats, string statId, float maxValue)
    {
        if (!stats.Read.TryGetFixed(statId, out float current))
        {
            return;
        }

        float clamped = Math.Clamp(current, 0f, maxValue);
        if (StatisticIds.ToFixed(clamped) != StatisticIds.ToFixed(current))
        {
            StatisticsComponentExtensions.SetFixed(ref stats, statId, clamped);
        }
    }

    private float ClampResolvedStat(string statId, float value)
    {
        return statId switch
        {
            StatisticIds.HEALTH_MAX => Math.Clamp(value, 1f, MaxVitalUserUnits),
            StatisticIds.OXYGEN_MAX => Math.Clamp(value, 1f, MaxVitalUserUnits),
            StatisticIds.KINETIC_RESIST => Math.Clamp(value, 0f, 1f),
            StatisticIds.MOVE_SPEED_PCT => Math.Clamp(
                value,
                CharacterDefaults.MIN_MOVE_SPEED_MULTIPLIER - 1f,
                CharacterDefaults.MAX_MOVE_SPEED_MULTIPLIER - 1f),
            _ => value,
        };
    }

    private static bool IsCoreDerivedStat(string statId)
    {
        return string.Equals(statId, StatisticIds.HEALTH_MAX, StringComparison.Ordinal)
            || string.Equals(statId, StatisticIds.OXYGEN_MAX, StringComparison.Ordinal)
            || string.Equals(statId, StatisticIds.KINETIC_RESIST, StringComparison.Ordinal)
            || string.Equals(statId, StatisticIds.MOVE_SPEED_PCT, StringComparison.Ordinal);
    }

    private static bool IsBaseAttribute(string statId)
    {
        return IndexOfBaseAttribute(statId) >= 0;
    }

    private static int IndexOfBaseAttribute(string statId)
    {
        for (var i = 0; i < StatisticIds.BaseAttributeIds.Length; i++)
        {
            if (string.Equals(StatisticIds.BaseAttributeIds[i], statId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static ulong ComputeGearHash(in InventoryComponent inventory)
    {
        ulong hash = FNV_OFFSET_BASIS;
        hash = HashSlotGroup(hash, in inventory, SlotGroups.WEARABLES);
        hash = HashSlotGroup(hash, in inventory, SlotGroups.AUGMENTS);
        return hash;
    }

    private static ulong HashSlotGroup(ulong hash, in InventoryComponent inventory, string groupId)
    {
        hash = HashString(hash, groupId);
        if (!inventory.Layout.TryGetGroup(groupId, out SlotGroupRange range))
        {
            return HashInt(hash, -1);
        }

        hash = HashInt(hash, range.Capacity);
        for (var i = 0; i < range.Capacity; i++)
        {
            ItemData stack = inventory.Get(new SlotId(groupId, i));
            hash = HashInt(hash, i);
            if (stack.IsEmpty())
            {
                hash = HashInt(hash, 0);
            }
            else
            {
                hash = HashInt(hash, 1);
                hash = HashString(hash, stack.ID);
            }
        }

        return hash;
    }

    private static (ulong BuffHash, long NextExpiryTick) ComputeBuffState(in StatisticsComponent stats, long simTick)
    {
        ulong hash = FNV_OFFSET_BASIS;
        long nextExpiry = long.MaxValue;
        Statistic[]? entries = stats.Entries;
        if (entries == null || entries.Length == 0)
        {
            return (hash, nextExpiry);
        }

        for (var i = 0; i < entries.Length; i++)
        {
            Statistic entry = entries[i];
            if (entry.ID == null || entry.Value <= simTick || !StatisticIds.IsBuffExpiresTickKey(entry.ID.AsSpan()))
            {
                continue;
            }

            hash = HashString(hash, entry.ID);
            if (entry.Value < nextExpiry)
            {
                nextExpiry = entry.Value;
            }
        }

        return (hash, nextExpiry);
    }

    private static ulong HashString(ulong hash, string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= FNV_PRIME;
        }

        hash ^= 0xFFUL;
        hash *= FNV_PRIME;
        return hash;
    }

    private static ulong HashInt(ulong hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= FNV_PRIME;
        }

        return hash;
    }

    private static float SanitizeMax(float value, float fallback)
    {
        return float.IsFinite(value) && value > 0f ? value : fallback;
    }

    private struct AggregateAction : IForEachRef<StatisticsComponent, OwnedCharacterComponent>
    {
        public ServerStatAggregationSystem Owner;
        public long SimTick;

        public void Execute(
            float delta,
            DataStore store,
            int entity,
            ref Ref<StatisticsComponent> stats,
            ref Ref<OwnedCharacterComponent> owned
        )
        {
            Owner.AggregatePlayer(store, entity, ref stats, SimTick);
        }
    }
}
