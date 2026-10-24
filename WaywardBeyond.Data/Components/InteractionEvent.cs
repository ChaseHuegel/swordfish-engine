using Swordfish.ECS;

namespace WaywardBeyond.Data.Components;

/// <summary>
/// A discrete interaction edge event (button press/release) owned by the client.
/// Optional metadata carries hints about the desired interaction, such as client-resolved targets.
/// A hintless event is valid (ex: right-click empty space).
/// </summary>
public partial struct InteractionEvent : IDataComponent;