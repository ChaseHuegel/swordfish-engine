using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Networking.Components;

/// <summary>
/// The appearance index (a character's <c>Body</c>) of a networked entity, authored by the server and
/// replicated to clients. Purely data — any entity (player, NPC, prop) may carry it, and rendering is
/// decoupled via the client's general billboard path.
/// </summary>
[NetworkComponent(10, NetworkDirection.ServerOwned)]
public partial struct BodyViewComponent : IDataComponent;