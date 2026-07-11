using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Sessions;

namespace WaywardBeyond.Shared.Networking.Components;

public struct NetworkComponent : IDataComponent
{
    public uint NetworkID;
    public Session Session;
    public uint LastAckedInput;
    public uint LastAckedSnapshot;
    public uint ServerTPS;
}
