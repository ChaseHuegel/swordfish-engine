using Swordfish.ECS;
using WaywardBeyond.Data.Registry;

namespace WaywardBeyond.Data.Components;

/// <summary>The appearance information of an entity.</summary>
[NetworkComponent(10, NetworkDirection.ServerOwned)]
public partial struct BodyViewComponent : IDataComponent;