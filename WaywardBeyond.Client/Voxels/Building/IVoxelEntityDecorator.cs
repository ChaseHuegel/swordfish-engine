using Swordfish.ECS;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels.Models;

namespace WaywardBeyond.Client.Voxels.Building;

public interface IVoxelEntityDecorator
{
    void Process(in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelInfo voxelInfo);
}