using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Krill;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels;

/// <summary>
/// Decodes bundled <c>.wbp</c> blueprints from <see cref="IBundledBlueprintDatabase"/> into centered
/// <see cref="VoxelEntityModel"/> instances for client runtime scenes.
/// </summary>
internal sealed class BlueprintDatabase(
    in IBundledBlueprintDatabase bundledBlueprints,
    in IBrickRegistry brickRegistry,
    IPaintRegistry? paintRegistry = null
) : IAssetDatabase<VoxelEntityModel>
{
    private readonly IBundledBlueprintDatabase _bundledBlueprints = bundledBlueprints;
    private readonly IBrickRegistry _brickRegistry = brickRegistry;
    private readonly IPaintRegistry? _paintRegistry = paintRegistry;
    private readonly Lock _cacheLock = new();
    private readonly Dictionary<string, VoxelEntityModel> _models = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Result<VoxelEntityModel> Get(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Result<VoxelEntityModel>.FromFailure("Blueprint identifier is required.");
        }

        lock (_cacheLock)
        {
            if (_models.TryGetValue(id, out VoxelEntityModel cached))
            {
                return Result<VoxelEntityModel>.FromSuccess(cached);
            }
        }

        Result<BlueprintData> rawResult = _bundledBlueprints.Get(id);
        if (!rawResult.Success)
        {
            return new Result<VoxelEntityModel>(false, default!, rawResult.Message, rawResult.Exception);
        }

        BlueprintData raw = rawResult.Value;
        if (raw.SizeX <= 0 || raw.SizeY <= 0 || raw.SizeZ <= 0 || raw.Chunks == null)
        {
            return Result<VoxelEntityModel>.FromFailure($"Bundled blueprint \"{id}\" has invalid bounds.");
        }

        VoxelRegionData encoded = raw.ToRegion();
        VoxelRegionData decoded = VoxelCodec.Decode(in encoded, _brickRegistry, _paintRegistry);
        VoxelObject voxelObject = VoxelRegionApplier.ToCenteredVoxelObject(in decoded, out _);

        var model = new VoxelEntityModel(Uuid.NewUuid(), Vector3.Zero, Quaternion.Identity, voxelObject)
        {
            Scale = Vector3.One,
        };

        lock (_cacheLock)
        {
            _models[id] = model;
        }

        return Result<VoxelEntityModel>.FromSuccess(model);
    }
}
