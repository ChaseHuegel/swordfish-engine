using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Networking.Components;

/// <summary>
/// The client-authoritative active inventory slot of a player, replicated upstream. Authored by the
/// client (hotbar selection, number-key shortcuts); the server validates interactions and consumes
/// items against the inbound value.
/// </summary>
[NetworkComponent(12, NetworkDirection.ClientOwned)]
public partial struct EquipmentComponent : IDataComponent;