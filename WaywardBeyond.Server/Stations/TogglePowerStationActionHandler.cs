using Krill;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Stations;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Stations;

/// <summary>Built-in station action handler that toggles <see cref="MachineStateComponent.PowerEnabled"/> on a reachable toggleable brick-entity.</summary>
public sealed class TogglePowerStationActionHandler(IBrickDatabase brickDatabase) : IStationActionHandler
{
    /// <inheritdoc/>
    public InteractionPriority Priority => InteractionPriority.Normal;

    /// <inheritdoc/>
    public StationActionFilter Filter => new(Action: StationActionKind.TogglePower);

    /// <inheritdoc/>
    public StationActionResult Handle(in StationActionContext context)
    {
        if (context.Stations == null)
        {
            return StationActionResult.Reject("Station session service is unavailable.");
        }

        if (context.Op.TargetEntity == 0UL)
        {
            return StationActionResult.Reject("TogglePower requires a target brick-entity.");
        }

        Result<int> authorization = context.Stations.AuthorizeBrickEntity(
            context.Store,
            context.Player,
            context.ClientId,
            Uuid.FromValue(context.Op.TargetEntity));
        if (!authorization.Success)
        {
            return StationActionResult.Reject(authorization.Message);
        }

        int targetEntity = authorization.Value;
        if (!context.Store.TryGet(targetEntity, out MachineStateComponent machine)
            || !context.Store.TryGet(targetEntity, out BrickAnchorComponent anchor)
            || !context.Store.TryGet(anchor.VoxelEntityUuid, out int structureEntity)
            || !context.Store.TryGet(structureEntity, out VoxelWorldComponent voxelWorld))
        {
            return StationActionResult.Reject("Target brick-entity does not carry toggleable machine state.");
        }

        Voxel voxel = voxelWorld.VoxelObject.Get(anchor.VoxelCoord.X, anchor.VoxelCoord.Y, anchor.VoxelCoord.Z);
        Result<Brick> brickResult = brickDatabase.Get(voxel.ID);
        if (!brickResult.Success || !brickResult.Value.Toggleable)
        {
            return StationActionResult.Reject("Target brick is not toggleable.");
        }

        machine.PowerEnabled = !machine.PowerEnabled;
        context.Store.AddOrUpdate(targetEntity, machine);
        return StationActionResult.Handled;
    }
}
