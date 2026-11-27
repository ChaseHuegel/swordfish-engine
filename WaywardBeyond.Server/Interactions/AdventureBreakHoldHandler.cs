using System.Collections.Generic;
using Krill;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Statistics;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// Validate stage, low priority: requires a timed hold before committing Adventure Mode brick breaks.
/// Duration is derived from brick hardness and the extensible <see cref="IStatResolver"/> pipeline for
/// <see cref="StatisticIds.MINING_SPEED"/>. Higher-priority validate handlers can override or replace it.
/// </summary>
public sealed class AdventureBreakHoldHandler(
    IBrickDatabase brickDatabase,
    IStatResolver statResolver
) : IInteractionHandler
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
        float hardness = ResolveBrickHardness(brickDataId);
        float requiredDuration = ResolveDuration(in context, brickDataId, hardness);

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

    private float ResolveBrickHardness(ushort brickDataId)
    {
        if (brickDataId == 0)
        {
            return Brick.DEFAULT_HARDNESS;
        }

        Result<Brick> result = brickDatabase.Get(brickDataId);
        return result.Success ? Brick.ResolveHardness(result.Value.Hardness) : Brick.DEFAULT_HARDNESS;
    }

    private float ResolveDuration(in InteractionContext context, ushort brickDataId, float hardness)
    {
        if (hardness == 0f)
        {
            return 0f;
        }

        IReadOnlyDictionary<string, long>? skillXp = context.Store.TryGet(context.Player, out SkillStateComponent skillState)
            ? skillState.XPBySkill
            : null;

        var query = new StatQuery(
            StatisticIds.MINING_SPEED,
            StatisticIds.DEFAULT_MINING_SPEED,
            store: context.Store,
            entity: context.Player,
            heldItemId: context.HeldItemID,
            targetBrickDataId: brickDataId,
            skillXp: skillXp);

        float speed = statResolver.Evaluate(in query);
        if (!float.IsFinite(speed) || speed <= 0f)
        {
            speed = StatisticIds.DEFAULT_MINING_SPEED;
        }

        float duration = hardness / speed;
        return float.IsFinite(duration) && duration >= 0f ? duration : hardness;
    }
}
