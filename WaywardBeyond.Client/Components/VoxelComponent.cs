using Swordfish.ECS;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Components;

public struct VoxelComponent(in VoxelObject voxelObject, in Uuid transparencyPtr, in Uuid maskedPtr) : IDataComponent
{
    public readonly VoxelObject VoxelObject = voxelObject;
    public readonly Uuid TransparencyPtr = transparencyPtr;
    public readonly Uuid MaskedPtr = maskedPtr;
}