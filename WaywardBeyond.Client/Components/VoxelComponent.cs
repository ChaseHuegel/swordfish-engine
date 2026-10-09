using Swordfish.ECS;
using WaywardBeyond.Client.Voxels;

namespace WaywardBeyond.Client.Components;

public struct VoxelComponent(in VoxelObject voxelObject, in Uuid transparencyPtr) : IDataComponent
{
    public readonly VoxelObject VoxelObject = voxelObject;
    public readonly Uuid TransparencyPtr = transparencyPtr;
}