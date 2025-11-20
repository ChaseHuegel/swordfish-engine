using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Building;

public interface IVoxelEntityDecorator
{
    void Process(in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelInfo voxelInfo);
}