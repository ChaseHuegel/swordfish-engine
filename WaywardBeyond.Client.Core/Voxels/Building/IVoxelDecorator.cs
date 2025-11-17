using System;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Voxels.Building;

public interface IVoxelDecorator
{
    void Process(in VoxelObjectBuilder.Data data, in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelIdentifierComponent voxelIdentifier);
    
    //  TODO remove this in favor of a general way of managing voxel entities inside the VoxelEntityBuilder.
    [Obsolete("This will be removed in a future release.")]
    void Process(in VoxelObjectBuilder.Data data, in DataStore store, in int entity, in VoxelComponent voxelComponent);
}