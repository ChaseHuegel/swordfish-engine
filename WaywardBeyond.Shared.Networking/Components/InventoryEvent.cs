using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Networking.Components;

/// <summary>
/// A discrete player inventory operation edge (slot move) transported upstream as a latched
/// <see cref="ClientOwned"/> event. The server is authoritative over inventory moves: it validates the
/// op against its copy, applies it, and its <see cref="InventoryComponent"/> echo (already a replicated
/// ServerOwned component) corrects any invalid client prediction. One presence-based member mirrors the
/// <see cref="InteractionEvent"/> union pattern; reserved members (Sort, Drop, Transfer) are added as
/// nullable fields without code.
/// </summary>
[NetworkComponent(16, NetworkDirection.ClientOwned)]
public partial struct InventoryEvent : IDataComponent;