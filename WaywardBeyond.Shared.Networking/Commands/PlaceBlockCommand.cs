using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Commands;

public struct PlaceBlockCommand : IDataComponent
{
    public int X;
    public int Y;
    public int Z;
    public int BrickID;
}
