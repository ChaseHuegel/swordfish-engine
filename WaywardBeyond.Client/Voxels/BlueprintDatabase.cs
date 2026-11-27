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
/// Adapts <see cref="IBundledBlueprintDatabase"/> into decoded <see cref="VoxelEntityModel"/> assets
/// for client scenes while forwarding raw bundled blueprint summaries and data.
/// </summary>
internal sealed class BlueprintDatabase(
    in IBundledBlueprintDatabase bundledBlueprints,
    in IBrickRegistry brickRegistry,
    IPaintRegistry? paintRegistry = null
) : IAssetDatabase<VoxelEntityModel>, IBundledBlueprintDatabase
{
    private readonly IBundledBlueprintDatabase _bundledBlueprints = bundledBlueprints;
    private readonly IBrickRegistry _brickRegistry = brickRegistry;
    private readonly IPaintRegistry? _paintRegistry = paintRegistry;
    private readonly Lock _modelsLock = new();
    private readonly Dictionary<string, VoxelEntityModel> _models = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public IReadOnlyList<BlueprintSummary> ListSummaries() => _bundledBlueprints.ListSummaries();

    /// <inheritdoc/>
    public Result<BlueprintData> GetBlueprint(string id) => _bundledBlueprints.GetBlueprint(id);

    /// <inheritdoc/>
    public Result<VoxelEntityModel> Get(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Result<VoxelEntityModel>.FromFailure($"Unknown bundled blueprint \"{id}\"");
        }

        lock (_modelsLock)
        {
            if (_models.TryGetValue(id, out VoxelEntityModel cached))
            {
                return Result<VoxelEntityModel>.FromSuccess(cached);
            }
        }

        Result<BlueprintData> rawResult = _bundledBlueprints.GetBlueprint(id);
        if (!rawResult.Success)
        {
            return new Result<VoxelEntityModel>(false, default, rawResult.Message, rawResult.Exception);
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

        lock (_modelsLock)
        {
            _models[id] = model;
        }

        return Result<VoxelEntityModel>.FromSuccess(model);
    }
}
