using System;
using System.Collections.Generic;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Buffs;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// React stage: uses one data-driven consumable from the active hotbar slot in Adventure Mode. Applies
/// the item's one-shot statistic modifiers and optional buff, then deducts one item. An item that would
/// change nothing is not wasted. A per-item cooldown gates repeat use.
/// </summary>
public sealed class ConsumeItemInteractionHandler(
    IItemDatabase items,
    IBuffDatabase? buffs = null
) : IInteractionHandler
{
    private const float DEFAULT_FIXED_DELTA = 1f / 60f;

    /// <inheritdoc/>
    public InteractionStage Stage => InteractionStage.React;

    /// <inheritdoc/>
    public InteractionPriority Priority => InteractionPriority.Normal;

    /// <inheritdoc/>
    public InteractionHandlerFilter Filter => new(Kind: InteractionKind.SecondaryPressed, GameMode: GameMode.Adventure);

    /// <inheritdoc/>
    public InteractionDecision Handle(ref InteractionContext context)
    {
        if (context.GameMode != GameMode.Adventure || string.IsNullOrEmpty(context.HeldItemID))
        {
            return InteractionDecision.Continue;
        }

        Result<Item> itemResult = items.Get(context.HeldItemID);
        if (!itemResult.Success || itemResult.Value.Consumable is not { } consumable || !HasEffect(in consumable))
        {
            return InteractionDecision.Continue;
        }

        if (!context.Store.TryGet(context.Player, out EquipmentComponent equipment)
            || !context.Store.TryGet(context.Player, out InventoryComponent inventory)
            || !context.Store.TryGet(context.Player, out StatisticsComponent stats))
        {
            return InteractionDecision.Continue;
        }

        var slot = new SlotId(SlotGroups.HOTBAR, equipment.ActiveInventorySlot);
        ItemData stack = inventory.Get(in slot);
        if (stack.IsEmpty() || !string.Equals(stack.ID, context.HeldItemID, StringComparison.Ordinal))
        {
            return InteractionDecision.Continue;
        }

        float fixedDelta = float.IsFinite(context.FixedDelta) && context.FixedDelta > 0f
            ? context.FixedDelta
            : DEFAULT_FIXED_DELTA;
        long simTick = Math.Max(0L, context.SimTick);
        string? cooldownKey = null;

        if (consumable.CooldownSeconds > 0f && float.IsFinite(consumable.CooldownSeconds))
        {
            cooldownKey = StatisticIds.Cooldown(context.HeldItemID.AsSpan());
            if (stats.TryGetValue(cooldownKey, out long readyTick) && readyTick > simTick)
            {
                return InteractionDecision.Continue;
            }
        }

        Statistic[]? currentEntries = stats.Entries;
        var projected = currentEntries != null
            ? new StatisticsComponent([.. currentEntries])
            : new StatisticsComponent();
        bool modifiersChanged = ApplyModifiers(ref projected, consumable.Modifiers);
        bool grantsBuff = TryResolveBuff(consumable.BuffID, out Buff buff)
            && WouldChangeBuff(in projected, in buff, simTick, fixedDelta);

        if (!modifiersChanged && !grantsBuff)
        {
            return InteractionDecision.Continue;
        }

        InventoryComponent updated = inventory.Clone();
        if (!updated.Remove(in slot, 1).Success)
        {
            return InteractionDecision.Continue;
        }

        context.Store.AddOrUpdate(context.Player, updated);

        if (modifiersChanged)
        {
            context.Store.AddOrUpdate(context.Player, projected);
        }

        if (grantsBuff)
        {
            context.Store.ApplyBuff(context.Player, in buff, simTick, fixedDelta);
        }

        if (cooldownKey != null)
        {
            long readyTick = StatisticIds.ComputeExpiresTick(simTick, consumable.CooldownSeconds, fixedDelta);
            context.Store.SetStatistic(context.Player, cooldownKey, readyTick);
        }

        return InteractionDecision.Continue;
    }

    private static bool ApplyModifiers(ref StatisticsComponent stats, List<ConsumableStatModifierDefinition>? modifiers)
    {
        if (modifiers == null || modifiers.Count == 0)
        {
            return false;
        }

        var changed = false;
        for (var i = 0; i < modifiers.Count; i++)
        {
            ConsumableStatModifierDefinition modifier = modifiers[i];
            if (!TryNormalizeModifier(in modifier, out string statId, out float flatDelta, out float percentDelta))
            {
                continue;
            }

            changed |= ApplyModifier(ref stats, statId, flatDelta, percentDelta);
        }

        return changed;
    }

    private static bool ApplyModifier(ref StatisticsComponent stats, string statId, float flatDelta, float percentDelta)
    {
        if (StatisticIds.IsIntegerStat(statId.AsSpan()))
        {
            long current = stats.GetValue(statId);
            if (!TryComputeLong(current, flatDelta, percentDelta, out long next))
            {
                return false;
            }

            return stats.SetValue(statId, next);
        }

        float currentValue = stats.GetFixed(statId);
        float nextValue = (currentValue + flatDelta) * (1f + percentDelta);
        if (!float.IsFinite(nextValue))
        {
            return false;
        }

        nextValue = ClampVital(in stats, statId, nextValue);
        return stats.SetFixed(statId, nextValue);
    }

    private static bool TryComputeLong(long current, float flatDelta, float percentDelta, out long next)
    {
        double value = ((double)current + flatDelta) * (1.0 + percentDelta);
        if (!double.IsFinite(value))
        {
            next = current;
            return false;
        }

        if (value >= long.MaxValue)
        {
            next = long.MaxValue;
        }
        else if (value <= long.MinValue)
        {
            next = long.MinValue;
        }
        else
        {
            next = (long)Math.Round(value);
        }

        return true;
    }

    private static float ClampVital(in StatisticsComponent stats, string statId, float value)
    {
        if (string.Equals(statId, StatisticIds.HEALTH, StringComparison.Ordinal))
        {
            float max = SanitizeMax(stats.GetFixed(StatisticIds.HEALTH_MAX, StatisticIds.DEFAULT_HEALTH_MAX), StatisticIds.DEFAULT_HEALTH_MAX);
            return Math.Clamp(value, 0f, max);
        }

        if (string.Equals(statId, StatisticIds.OXYGEN, StringComparison.Ordinal))
        {
            float max = SanitizeMax(stats.GetFixed(StatisticIds.OXYGEN_MAX, StatisticIds.DEFAULT_OXYGEN_MAX), StatisticIds.DEFAULT_OXYGEN_MAX);
            return Math.Clamp(value, 0f, max);
        }

        return value;
    }

    private static bool TryNormalizeModifier(
        in ConsumableStatModifierDefinition modifier,
        out string statId,
        out float flatDelta,
        out float percentDelta
    ) {
        statId = modifier.StatId?.Trim() ?? string.Empty;
        flatDelta = modifier.FlatDelta ?? 0f;
        percentDelta = modifier.PercentDelta ?? 0f;

        if (statId.Length == 0 || !float.IsFinite(flatDelta) || !float.IsFinite(percentDelta))
        {
            return false;
        }

        return flatDelta != 0f || percentDelta != 0f;
    }

    private static bool WouldChangeBuff(in StatisticsComponent stats, in Buff buff, long simTick, float fixedDelta)
    {
        long existingExpiry = stats.TryGetBuffExpiresTick(buff.ID.AsSpan(), out long expiry) ? expiry : 0L;
        return buff.TryComputeNextExpiresTick(existingExpiry, simTick, fixedDelta, out _);
    }

    private bool TryResolveBuff(string? buffId, out Buff buff)
    {
        buff = default;
        if (string.IsNullOrWhiteSpace(buffId) || buffs == null)
        {
            return false;
        }

        Result<Buff> result = buffs.Get(buffId.Trim());
        if (!result.Success)
        {
            return false;
        }

        buff = result.Value;
        return true;
    }

    private static bool HasEffect(in ConsumableDefinition consumable)
    {
        return consumable.Modifiers is { Count: > 0 }
            || !string.IsNullOrWhiteSpace(consumable.BuffID);
    }

    private static float SanitizeMax(float value, float fallback)
    {
        return float.IsFinite(value) && value > 0f ? value : fallback;
    }
}
