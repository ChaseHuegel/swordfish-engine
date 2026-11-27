using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.IO;
using Swordfish.Physics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Statistics;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.Interactions;

/// <summary>
/// Client timed hold provider for Adventure Mode brick breaking with a mining tool. Resolves expected hold
/// duration through the shared <see cref="IStatResolver"/> pipeline for <see cref="StatisticIds.HARDNESS"/>
/// and <see cref="StatisticIds.MINING_SPEED"/>.
/// </summary>
internal sealed class AdventureBreakTimedInteraction(
    IPhysics physics,
    IBrickRegistry brickRegistry,
    IStatResolver statResolver,
    ActiveCharacterSave? activeCharacterSave = null
) : IClientTimedInteraction
{
    /// <summary>Stable handler identifier matching the server's Adventure break hold handler.</summary>
    public const string HANDLER_ID = "wb:adventure_break";

    /// <inheritdoc/>
    public MouseButton Button => MouseButton.Left;

    /// <inheritdoc/>
    public bool AppliesTo(in ClientTimedInteractionContext context)
    {
        return context.GameMode == GameMode.Adventure &&
            context.HeldPlaceable == null &&
            context.HeldItem?.Tool?.Type == ToolType.Mining;
    }

    /// <inheritdoc/>
    public bool TryResolveTarget(in ClientTimedInteractionContext context, out ClientTimedTarget target)
    {
        target = default;
        if (!context.TryRaycastBrick(
            offset: false,
            reachAround: true,
            out _,
            out Voxel targetVoxel,
            out _,
            out BrickInteraction hint))
        {
            return false;
        }

        var world = new ClientVoxelInteractionWorld(context.Store, physics);
        Vector3 origin = context.Store.TryGet(context.PlayerEntity, out TransformComponent playerTransform)
            ? playerTransform.Position
            : Vector3.Zero;

        var request = new InteractionRequest(
            origin,
            hint,
            InteractionKind.PrimaryPressed,
            null,
            context.GameMode,
            CharacterDefaults.DEFAULT_REACH);
        InteractionResolution resolution = InteractionResolver.Resolve(in request, world, brickRegistry);
        if (resolution.Action != InteractionAction.Break || targetVoxel.ID == 0)
        {
            return false;
        }

        Uuid targetUuid = context.Store.GetUuid(resolution.Entity);
        ushort brickDataId = resolution.Voxel.ID;
        float requiredDuration = ResolveDuration(in context, brickDataId);

        var key = new TimedInteractionKey(HANDLER_ID, targetUuid, resolution.Coordinate, brickDataId);
        target = new ClientTimedTarget(
            key,
            requiredDuration,
            InteractionKind.PrimaryPressed,
            InteractionKind.PrimaryReleased,
            brickHint: hint);
        return true;
    }

    private float ResolveDuration(in ClientTimedInteractionContext context, ushort brickDataId)
    {
        var hardnessQuery = new StatQuery(
            StatisticIds.HARDNESS,
            StatisticIds.DEFAULT_HARDNESS,
            store: context.Store,
            entity: context.PlayerEntity,
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

        Statistic[]? characterStatistics = activeCharacterSave?.ActiveSave?.Statistics;
        var speedQuery = new StatQuery(
            StatisticIds.MINING_SPEED,
            StatisticIds.DEFAULT_MINING_SPEED,
            store: context.Store,
            entity: context.PlayerEntity,
            heldItem: context.HeldItem,
            heldItemId: context.HeldItemId,
            targetBrickDataId: brickDataId,
            characterStatistics: characterStatistics);

        float speed = statResolver.Evaluate(in speedQuery);
        if (!float.IsFinite(speed) || speed <= 0f)
        {
            speed = StatisticIds.DEFAULT_MINING_SPEED;
        }

        float duration = hardness / speed;
        return float.IsFinite(duration) && duration >= 0f ? duration : hardness;
    }
}
