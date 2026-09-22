using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Networking.Components;

/// <summary>
/// The server-authoritative active inventory slot of a player, replicated downstream. Authored by the
/// server after the join-time seed; the client predicts presentably against it but does not author it.
/// </summary>
[NetworkComponent(12, NetworkDirection.ServerOwned)]
public partial struct EquipmentComponent : IDataComponent;