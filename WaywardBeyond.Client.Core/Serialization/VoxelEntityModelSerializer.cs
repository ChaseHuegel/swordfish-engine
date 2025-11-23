using System;
using System.Numerics;
using Swordfish.Library.Serialization;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Serialization;

internal class VoxelEntityModelSerializer : ISerializer<VoxelEntityModel>
{
    public byte[] Serialize(VoxelEntityModel value)
    {
        ChunkInfo[] chunkInfos = value.VoxelObject.GetChunkInfos();

        var voxelEntityData = new VoxelEntityData(
            value.Guid.ToString(),
            value.Position.X,
            value.Position.Y,
            value.Position.Z,
            value.Orientation.X,
            value.Orientation.Y,
            value.Orientation.Z,
            value.Orientation.W,
            value.Scale.X,
            value.Scale.Y,
            value.Scale.Z,
            chunkInfos
        );
        
        return voxelEntityData.Serialize();
    }
    
    public VoxelEntityModel Deserialize(byte[] data)
    {
        VoxelEntityData voxelEntityData = VoxelEntityData.Deserialize(data);

        Guid guid = Guid.TryParse(voxelEntityData.Guid, out Guid parsedGuid) ? parsedGuid : Guid.NewGuid();
        var position = new Vector3((float)voxelEntityData.X, (float)voxelEntityData.Y, (float)voxelEntityData.Z);
        var orientation = new Quaternion(voxelEntityData.OrientationX, voxelEntityData.OrientationY, voxelEntityData.OrientationZ, voxelEntityData.OrientationW);
        var voxelObject = new VoxelObject(chunkSize: 16, voxelEntityData.Chunks);
        
        return new VoxelEntityModel(guid, position, orientation, voxelObject);
    }
}