using Swordfish.ECS;

namespace WaywardBeyond.Data.Components;

/// <summary>A discrete player inventory operation edge event. The server is authoritative over inventory moves: it validates the
/// op against its copy, applies it, and its <see cref="InventoryComponent"/> echo (already a replicated
/// ServerOwned component) corrects any invalid client prediction.
/// </summary>
public partial struct InventoryEvent : IDataComponent;