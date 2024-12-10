using Swordfish.Bricks;
using Swordfish.ECS;

namespace WaywardBeyond.Client.Core.Components;

internal struct BrickComponent(in BrickGrid grid, in int transparencyPtr) : IDataComponent
{
    public readonly BrickGrid Grid = grid;
    public readonly int TransparencyPtr = transparencyPtr;
}