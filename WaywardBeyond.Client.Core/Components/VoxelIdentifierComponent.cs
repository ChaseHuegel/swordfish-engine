using Swordfish.ECS;

namespace WaywardBeyond.Client.Core.Components;

public struct VoxelIdentifierComponent : IDataComponent
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;
    
    public VoxelIdentifierComponent(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}