using System;
using Krill;
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
/// instant health and oxygen restoration plus an optional buff, then deducts one item. A full-health
/// item that grants no buff is not wasted. A per-item cooldown gates repeat use.
/// </summary>
public sealed class ConsumeItemInteractionHandler(
    IItemDatabase items,
    IBuffDatabase? buffs = null
) : IInteractionHandler
{
    private const float BENEFIT_EPSILON = 0.0005f;
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

        float healthMax = SanitizeMax(stats.GetFixed(StatisticIds.HEALTH_MAX, StatisticIds.DEFAULT_HEALTH_MAX), StatisticIds.DEFAULT_HEALTH_MAX);
        float oxygenMax = SanitizeMax(stats.GetFixed(StatisticIds.OXYGEN_MAX, StatisticIds.DEFAULT_OXYGEN_MAX), StatisticIds.DEFAULT_OXYGEN_MAX);
        float health = Math.Clamp(stats.GetFixed(StatisticIds.HEALTH, healthMax), 0f, healthMax);
        float oxygen = Math.Clamp(stats.GetFixed(StatisticIds.OXYGEN, oxygenMax), 0f, oxygenMax);

        bool hasBuff = TryResolveBuff(consumable.GrantBuffID, out Buff buff);
        bool canBenefit = (consumable.HealHp > 0f && health < healthMax - BENEFIT_EPSILON)
            || (consumable.RestoreO2 > 0f && oxygen < oxygenMax - BENEFIT_EPSILON)
            || hasBuff;
        if (!canBenefit)
        {
            return InteractionDecision.Continue;
        }

        InventoryComponent updated = inventory.Clone();
        if (!updated.Remove(in slot, 1).Success)
        {
            return InteractionDecision.Continue;
        }

        context.Store.AddOrUpdate(context.Player, updated);

        if (consumable.HealHp > 0f)
        {
            context.Store.SetStatisticFixed(
                context.Player,
                StatisticIds.HEALTH,
                Math.Clamp(health + consumable.HealHp, 0f, healthMax));
        }

        if (consumable.RestoreO2 > 0f)
        {
            context.Store.SetStatisticFixed(
                context.Player,
                StatisticIds.OXYGEN,
                Math.Clamp(oxygen + consumable.RestoreO2, 0f, oxygenMax));
        }

        if (hasBuff)
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
        return consumable.HealHp > 0f
            || consumable.RestoreO2 > 0f
            || !string.IsNullOrWhiteSpace(consumable.GrantBuffID);
    }

    private static float SanitizeMax(float value, float fallback)
    {
        return float.IsFinite(value) && value > 0f ? value : fallback;
    }
}
