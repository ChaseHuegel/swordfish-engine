using System.Collections.Generic;
using Krill;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Statistics;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// Validate stage, low priority: requires a timed hold before committing Adventure Mode brick breaks.
/// Duration is derived from <see cref="StatisticIds.HARDNESS"/> and <see cref="StatisticIds.MINING_SPEED"/>
/// evaluated through the extensible <see cref="IStatResolver"/> pipeline. Higher-priority validate handlers
/// can override or replace it.
/// </summary>
public sealed class AdventureBreakHoldHandler(IStatResolver statResolver) : IInteractionHandler
{
    /// <summary>Stable handler identifier used in <see cref="TimedInteractionKey.HandlerId"/>.</summary>
    public const string HANDLER_ID = "wb:adventure_break";

    /// <inheritdoc/>
    public InteractionStage Stage => InteractionStage.Validate;

    /// <inheritdoc/>
    public InteractionPriority Priority => InteractionPriority.Low;

    /// <inheritdoc/>
    public InteractionHandlerFilter Filter => new(GameMode: GameMode.Adventure);

    /// <inheritdoc/>
    public InteractionDecision Handle(ref InteractionContext context)
    {
        if (context.Hold.IsRequired || context.Resolution.Action != InteractionAction.Break)
        {
            return InteractionDecision.Continue;
        }

        ushort brickDataId = context.Resolution.Voxel.ID;
        float requiredDuration = ResolveDuration(in context, brickDataId);

        Uuid targetUuid = context.Hint.HasValue
            ? Uuid.FromValue(context.Hint.Value.TargetEntity)
            : context.Store.GetUuid(context.Resolution.Entity);

        var key = new TimedInteractionKey(
            HANDLER_ID,
            targetUuid,
            context.Resolution.Coordinate,
            brickDataId);

        context.Hold = TimedInteractionSpec.Require(
            key,
            requiredDuration,
            TimedInteractionInput.Primary,
            cancelOnEquipmentChange: true);

        return InteractionDecision.Continue;
    }

    private float ResolveDuration(in InteractionContext context, ushort brickDataId)
    {
        var hardnessQuery = new StatQuery(
            StatisticIds.HARDNESS,
            StatisticIds.DEFAULT_HARDNESS,
            store: context.Store,
            entity: context.Player,
            targetBrickDataId: brickDataId);

        float hardness = statResolver.Evaluate(in hardnessQuery);
        if (!float.IsFinite(hardness) || hardness < 0f)
        {
            hardness = StatisticIds.DEFAULT_HARDNESS;
        }

        if (hardness == 0f)
        {
            return 0f;
        }

        IReadOnlyDictionary<string, long>? skillXp = context.Store.TryGet(context.Player, out SkillStateComponent skillState)
            ? skillState.XPBySkill
            : null;

        var speedQuery = new StatQuery(
            StatisticIds.MINING_SPEED,
            StatisticIds.DEFAULT_MINING_SPEED,
            store: context.Store,
            entity: context.Player,
            heldItemId: context.HeldItemID,
            targetBrickDataId: brickDataId,
            skillXp: skillXp);

        float speed = statResolver.Evaluate(in speedQuery);
        if (!float.IsFinite(speed) || speed <= 0f)
        {
            speed = StatisticIds.DEFAULT_MINING_SPEED;
        }

        float duration = hardness / speed;
        return float.IsFinite(duration) && duration >= 0f ? duration : hardness;
    }
}
