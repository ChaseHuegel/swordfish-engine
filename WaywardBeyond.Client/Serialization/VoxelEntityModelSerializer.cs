using System.Numerics;
using Krill;
using Swordfish.Library.Serialization;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Serialization;

/// <summary>Serializes and deserializes <see cref="VoxelEntityModel"/> blueprints with brick and paint palettes.</summary>
internal sealed class VoxelEntityModelSerializer(
    in IBrickRegistry brickRegistry,
    IPaintRegistry? paintRegistry = null
) : ISerializer<VoxelEntityModel>
{
    private readonly IBrickRegistry _brickRegistry = brickRegistry;
    private readonly IPaintRegistry? _paintRegistry = paintRegistry;

    /// <inheritdoc/>
    public byte[] Serialize(VoxelEntityModel value)
    {
        ChunkInfo[] chunkInfos = value.VoxelObject.GetChunkInfos();

        var voxelEntityData = new VoxelEntityData(
            value.Uuid.ToValue(),
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
            chunkInfos,
            _BrickPalette: null,
            _PaintPalette: null
        );

        VoxelEntityData encoded = VoxelCodec.Encode(in voxelEntityData, _brickRegistry, _paintRegistry);
        return encoded.Serialize();
    }

    /// <inheritdoc/>
    public VoxelEntityModel Deserialize(byte[] data)
    {
        VoxelEntityData raw = VoxelEntityData.Deserialize(data);
        VoxelEntityData decoded = VoxelCodec.Decode(in raw, _brickRegistry, _paintRegistry);

        var position = new Vector3((float)decoded.X, (float)decoded.Y, (float)decoded.Z);
        var orientation = new Quaternion(decoded.OrientationX, decoded.OrientationY, decoded.OrientationZ, decoded.OrientationW);
        var scale = new Vector3(decoded.ScaleX, decoded.ScaleY, decoded.ScaleZ);
        if (scale == Vector3.Zero)
        {
            scale = Vector3.One;
        }

        byte chunkSize = decoded.Chunks.Length > 0 ? decoded.Chunks[0].Chunk.Size : (byte)0;
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            chunkSize = VoxelObject.DEFAULT_CHUNK_EDGE;
        }

        var voxelObject = new VoxelObject(chunkSize, decoded.Chunks);

        return new VoxelEntityModel(Uuid.FromValue(decoded.Uuid), position, orientation, voxelObject)
        {
            Scale = scale,
        };
    }
}