using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
/// The running state of one interaction as the handler stages process it. Handlers mutate the fields
/// marked mutable; the rest are fixed inputs. A stage that returns <see cref="InteractionDecision.Handled"/>
/// owns the fields it writes.
/// </summary>
public struct InteractionContext
{
    /// <summary>Creates the running state for one interaction from its fixed inputs.</summary>
    public InteractionContext(
        DataStore store,
        int player,
        Uuid clientId,
        uint sequence,
        IVoxelInteractionWorld world,
        in InteractionRequest request,
        string? heldItemID
    ) {
        Store = store;
        Player = player;
        ClientId = clientId;
        Sequence = sequence;
        World = world;
        Origin = request.Origin;
        Hint = request.Hint;
        Kind = request.Kind;
        GameMode = request.GameMode;
        Reach = request.Reach;
        HeldItemID = heldItemID;
        Placeable = null;
        Resolution = InteractionResolution.None;
    }

    /// <summary>The world store the interaction is resolved against.</summary>
    public readonly DataStore Store;

    /// <summary>The player entity that produced the interaction.</summary>
    public readonly int Player;

    /// <summary>The stable client id of that player.</summary>
    public readonly Uuid ClientId;

    /// <summary>The client's interaction sequence number, used to correlate the echo.</summary>
    public readonly uint Sequence;

    /// <summary>The world view the resolver reads targets from.</summary>
    public readonly IVoxelInteractionWorld World;

    /// <summary>World-space interaction origin; reach is measured from here.</summary>
    public readonly Vector3 Origin;

    /// <summary>The client's resolved target voxel hint.</summary>
    public readonly BrickInteraction? Hint;

    /// <summary>The input edge that produced the interaction.</summary>
    public readonly InteractionKind Kind;

    /// <summary>The game mode the interaction was made in.</summary>
    public readonly GameMode GameMode;

    /// <summary>Maximum distance the interaction may reach, from the origin.</summary>
    public readonly float Reach;

    /// <summary>The id of the item in the player's active slot, or null.</summary>
    public readonly string? HeldItemID;

    /// <summary>The content the interaction places. Set by a <see cref="InteractionStage.Select"/> handler.</summary>
    public Brick? Placeable;

    /// <summary>The outcome under consideration. Set by <see cref="InteractionStage.Resolve"/> and adjusted downstream.</summary>
    public InteractionResolution Resolution;

    /// <summary>Builds the immutable request the shared resolver decides over.</summary>
    public readonly InteractionRequest BuildRequest()
    {
        return new InteractionRequest(Origin, Hint, Kind, Placeable, GameMode, Reach);
    }
}
