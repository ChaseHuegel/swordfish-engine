using Swordfish.ECS;
using WaywardBeyond.Data.Registry;

namespace WaywardBeyond.Data.Components;

/// <summary>
/// A discrete interaction edge event (button press/release) owned by the client.
/// Optional metadata carries hints about the desired interaction, such as client-resolved targets.
/// A hintless event is valid (ex: right-click empty space).
/// </summary>
[NetworkComponent(15, NetworkDirection.ClientOwned)]
public partial struct InteractionEvent : IDataComponent;