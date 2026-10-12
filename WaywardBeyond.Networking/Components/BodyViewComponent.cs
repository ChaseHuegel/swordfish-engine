using Swordfish.ECS;
using WaywardBeyond.Networking.Registry;

namespace WaywardBeyond.Networking.Components;

/// <summary>
/// The appearance index (a character's <c>Body</c>) of a networked entity, authored by the server and
/// replicated to clients. Any entity (player, NPC, prop) may carry it.
/// </summary>
[NetworkComponent(10, NetworkDirection.ServerOwned)]
public partial struct BodyViewComponent : IDataComponent;