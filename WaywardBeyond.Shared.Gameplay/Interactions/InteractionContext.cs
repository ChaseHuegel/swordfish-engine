using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// The immutable facts handed to an interaction handler after base validation has resolved a legal
/// interaction. Carries the original <see cref="InteractionRequest"/> (kind, hint, held placeable, game
/// mode, reach, ray), the <see cref="Resolution"/> the shared resolver produced, the target structure and
/// cell context (<see cref="Entity"/>, <see cref="Coordinate"/>, <see cref="CurrentVoxel"/> - the voxel
/// currently occupying the target cell before the edit), and the held item id. A handler passes a
/// resolution through unchanged by returning <see cref="Resolution"/>.
/// </summary>
public readonly struct InteractionContext
{
    public readonly InteractionRequest Request;

    /// <summary>The base-resolved outcome (Break or Place) the shared resolver produced before handlers ran.</summary>
    public readonly InteractionResolution Resolution;

    /// <summary>The target structure entity the resolved action applies to.</summary>
    public readonly int Entity;

    /// <summary>The target cell in the structure's brick space.</summary>
    public readonly Int3 Coordinate;

    /// <summary>The voxel currently occupying the target cell before the edit is applied.</summary>
    public readonly Voxel CurrentVoxel;

    /// <summary>The id of the item currently held by the interacting player, or null.</summary>
    public readonly string? HeldItemID;

    public InteractionContext(in InteractionRequest request, in InteractionResolution resolution, string? heldItemID)
    {
        Request = request;
        Resolution = resolution;
        Entity = resolution.Entity;
        Coordinate = resolution.Coordinate;
        CurrentVoxel = resolution.Voxel;
        HeldItemID = heldItemID;
    }
}