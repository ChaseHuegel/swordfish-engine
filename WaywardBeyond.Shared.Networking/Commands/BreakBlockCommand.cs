using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Commands;

public struct BreakBlockCommand : IDataComponent
{
    public int X;
    public int Y;
    public int Z;
}
