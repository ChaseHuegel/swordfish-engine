using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Components;

public struct NetworkComponent : IDataComponent
{
    public uint NetworkID;
    public uint LastAckedInput;
    public uint ServerTPS;
}
