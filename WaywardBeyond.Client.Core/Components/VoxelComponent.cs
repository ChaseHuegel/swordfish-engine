using Swordfish.ECS;
using WaywardBeyond.Client.Core.Voxels;

namespace WaywardBeyond.Client.Core.Components;

public struct VoxelComponent(in VoxelObject voxelObject, in int transparencyPtr) : IDataComponent
{
    public readonly VoxelObject VoxelObject = voxelObject;
    public readonly int TransparencyPtr = transparencyPtr;
}