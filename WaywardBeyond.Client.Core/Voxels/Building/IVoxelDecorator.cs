using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Voxels.Building;

public interface IVoxelDecorator
{
    void Process(in VoxelObjectBuilder.Data data, in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelIdentifierComponent voxelIdentifier);
}