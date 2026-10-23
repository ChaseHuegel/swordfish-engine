using Swordfish.ECS;
using WaywardBeyond.Data.Registry;

namespace WaywardBeyond.Data.Components;

/// <summary>
/// The active equipment slots of an entity. The client owns this but the server
/// validates interactions and consumes items against the values.
/// </summary>
[NetworkComponent(12, NetworkDirection.ClientOwned)]
public partial struct EquipmentComponent : IDataComponent;