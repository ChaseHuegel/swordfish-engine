using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels;

/// <summary>
///     Provides access to <see cref="VoxelEntityModel"/>s from virtual resources.
/// </summary>
internal sealed class BlueprintDatabase : ResourceVirtualAssetDatabase<VoxelEntityData, VoxelEntityModel>, IAutoActivate
{
    private readonly IBrickRegistry _brickRegistry;
    private readonly IPaintRegistry? _paintRegistry;

    public BlueprintDatabase(
        in ILogger<BlueprintDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        in IBrickRegistry brickRegistry,
        IPaintRegistry? paintRegistry = null)
        : base(logger, fileParseService, vfs)
    {
        _brickRegistry = brickRegistry;
        _paintRegistry = paintRegistry;
        Load();
    }

    /// <inheritdoc/>
    protected override bool ExcludeExtensionFromID => true;

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".dat");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Root.At("blueprints");

    /// <inheritdoc/>
    protected override Result<VoxelEntityModel> LoadAsset(string id, Resource<VoxelEntityData> assetInfo)
    {
        VoxelEntityData raw = assetInfo.Value;
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
        var model = new VoxelEntityModel(Uuid.FromValue(decoded.Uuid), position, orientation, voxelObject)
        {
            Scale = scale,
        };

        return Result<VoxelEntityModel>.FromSuccess(model);
    }
}