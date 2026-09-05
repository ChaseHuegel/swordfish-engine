using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Networking.Components;

[NetworkComponent(1, NetworkDirection.ClientOwned)]
public partial struct InputComponent : IDataComponent;