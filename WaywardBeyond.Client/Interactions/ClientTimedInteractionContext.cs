using Krill;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.Interactions;

/// <summary>Snapshot of the local player's state passed to <see cref="IClientTimedInteraction"/> implementations.</summary>
public readonly struct ClientTimedInteractionContext(
    DataStore store,
    int playerEntity,
    GameMode gameMode,
    int activeSlot,
    string? heldItemId,
    Item? heldItem,
    Brick? heldPlaceable,
    ClientBrickRaycastDelegate tryRaycastBrick
) {
    /// <summary>The client ECS store.</summary>
    public readonly DataStore Store = store;

    /// <summary>The local player entity handle.</summary>
    public readonly int PlayerEntity = playerEntity;

    /// <summary>The local player's active game mode.</summary>
    public readonly GameMode GameMode = gameMode;

    /// <summary>The active hotbar slot index.</summary>
    public readonly int ActiveSlot = activeSlot;

    /// <summary>The held item id, or null when empty-handed.</summary>
    public readonly string? HeldItemId = heldItemId;

    /// <summary>The resolved held item definition, or null.</summary>
    public readonly Item? HeldItem = heldItem;

    /// <summary>The resolved placeable brick for the held item, or null.</summary>
    public readonly Brick? HeldPlaceable = heldPlaceable;

    /// <summary>Delegate performing a client screen-space brick raycast and hint construction.</summary>
    public readonly ClientBrickRaycastDelegate TryRaycastBrick = tryRaycastBrick;
}
