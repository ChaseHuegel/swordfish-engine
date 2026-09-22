using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Networking.Components;

/// <summary>
/// A discrete player interaction edge (button press/release) transported upstream as a latched
/// <see cref="ClientOwned"/> event. Common metadata lives at the root; an optional <see cref="Brick"/>
/// hint carries the client's resolved target. A hint-less event is valid (e.g. right-click empty space)
/// and resolves to <see cref="InteractionKind"/>'s None action server-side.
/// </summary>
[NetworkComponent(15, NetworkDirection.ClientOwned)]
public partial struct InteractionEvent : IDataComponent;

/// <summary>
/// Client hint for a brick interaction: the target cell plus place-only shape/orientation. The server
/// independently validates and remains authoritative; this is a hint, never trusted state.
/// </summary>
public partial struct BrickInteraction;