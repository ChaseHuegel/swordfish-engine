using Swordfish.ECS;
using WaywardBeyond.Networking.Registry;

namespace WaywardBeyond.Networking.Components;

[NetworkComponent(1, NetworkDirection.ClientOwned)]
public partial struct InputComponent : IDataComponent;