using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Headless interface for resolving the two facts the authoritative server needs to apply a brick
/// interaction without any render-coupled client database: (1) which brick a held item places, and (2)
/// what loot a broken brick grants in survival mode. The client implements this over its
/// <c>ItemDatabase</c>/<c>BrickDatabase</c> and registers it in the shared container; the server resolves
/// it to author placeable/loot resolution. Tests provide a lightweight fake.
/// </summary>
public interface IInteractionContent
{
    /// <summary>
    /// Resolves a held item to the placeable brick it places, if any. Returns false when the item is not
    /// a placeable brick (e.g. the laser tool).
    /// </summary>
    bool TryGetPlaceable(string? itemID, out PlaceableBrick placeable);

    /// <summary>Resolves a broken brick's data id to the item stack granted on break (survival mode).</summary>
    bool TryGetLoot(ushort brickDataID, out ItemData loot);
}