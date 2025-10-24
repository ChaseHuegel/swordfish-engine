using Swordfish.ECS;

namespace WaywardBeyond.Client.Core.Components;

internal struct BrickIdentifierComponent : IDataComponent
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;
    
    public BrickIdentifierComponent(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}