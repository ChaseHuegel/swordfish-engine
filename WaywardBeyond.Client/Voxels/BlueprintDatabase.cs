using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using Krill;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels;

/// <summary>
/// Provides read-only access to bundled <c>.wbp</c> blueprints as both <see cref="VoxelEntityModel"/>
/// assets and raw <see cref="BlueprintData"/> / <see cref="BlueprintSummary"/> entries.
/// </summary>
internal sealed class BlueprintDatabase : ResourceVirtualAssetDatabase<BlueprintData, VoxelEntityModel>, IAutoActivate
{
    private readonly IBrickRegistry _brickRegistry;
    private readonly IPaintRegistry? _paintRegistry;
    private readonly string? _userBlueprintsPrefix;
    private readonly Lock _catalogLock = new();
    private readonly Dictionary<string, BlueprintData> _blueprints = new(StringComparer.Ordinal);
    private readonly List<BlueprintSummary> _summaries = [];

    public BlueprintDatabase(
        in ILogger<BlueprintDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        in IBrickRegistry brickRegistry,
        IPaintRegistry? paintRegistry = null,
        StoragePaths? storagePaths = null)
        : base(logger, fileParseService, vfs)
    {
        _brickRegistry = brickRegistry;
        _paintRegistry = paintRegistry;
        if (storagePaths != null)
        {
            string full = Path.GetFullPath(storagePaths.GetBlueprintsDirectory().Value)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _userBlueprintsPrefix = full + Path.DirectorySeparatorChar;
        }

        Load();
    }

    /// <summary>Returns metadata summaries for all loaded bundled blueprints.</summary>
    public IReadOnlyList<BlueprintSummary> ListSummaries()
    {
        lock (_catalogLock)
        {
            return [.. _summaries];
        }
    }

    /// <summary>Gets the raw palette-encoded <see cref="BlueprintData"/> for a bundled blueprint by ID.</summary>
    public Result<BlueprintData> GetBlueprint(string id)
    {
        lock (_catalogLock)
        {
            if (!string.IsNullOrEmpty(id) && _blueprints.TryGetValue(id, out BlueprintData data))
            {
                return Result<BlueprintData>.FromSuccess(data);
            }

            return Result<BlueprintData>.FromFailure($"Unknown bundled blueprint \"{id}\"");
        }
    }

    /// <inheritdoc/>
    protected override bool ExcludeExtensionFromID => true;

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path)
    {
        if (!path.HasExtension(FileBlueprintStorage.FILE_EXTENSION))
        {
            return false;
        }

        if (_userBlueprintsPrefix != null && !string.Equals(path.Scheme, "zip", StringComparison.OrdinalIgnoreCase))
        {
            string fullPath = Path.GetFullPath(path.Value);
            StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (fullPath.StartsWith(_userBlueprintsPrefix, comparison))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Root.At("blueprints");

    /// <inheritdoc/>
    protected override Result<VoxelEntityModel> LoadAsset(string id, Resource<BlueprintData> assetInfo)
    {
        BlueprintData raw = assetInfo.Value;
        if (raw.SizeX <= 0 || raw.SizeY <= 0 || raw.SizeZ <= 0 || raw.Chunks == null)
        {
            return Result<VoxelEntityModel>.FromFailure($"Bundled blueprint \"{id}\" has invalid bounds.");
        }

        VoxelRegionData encoded = raw.ToRegion();
        VoxelRegionData decoded = VoxelCodec.Decode(in encoded, _brickRegistry, _paintRegistry);
        VoxelObject voxelObject = VoxelRegionApplier.ToCenteredVoxelObject(in decoded, out _);

        int brickCount = raw.BrickCount > 0 ? raw.BrickCount : VoxelRegionApplier.CountSolidVoxels(in decoded);
        string displayName = string.IsNullOrWhiteSpace(raw.Name) ? id : raw.Name;
        var summary = new BlueprintSummary(
            id,
            displayName,
            raw.SizeX,
            raw.SizeY,
            raw.SizeZ,
            brickCount,
            raw.CreatedAtMs,
            raw.ModifiedAtMs);

        lock (_catalogLock)
        {
            _blueprints[id] = raw;
            int existing = _summaries.FindIndex(s => string.Equals(s.Id, id, StringComparison.Ordinal));
            if (existing >= 0)
            {
                _summaries[existing] = summary;
            }
            else
            {
                _summaries.Add(summary);
                _summaries.Sort(static (left, right) =>
                {
                    int nameCompare = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
                    return nameCompare != 0
                        ? nameCompare
                        : StringComparer.Ordinal.Compare(left.Id, right.Id);
                });
            }
        }

        var model = new VoxelEntityModel(Uuid.NewUuid(), Vector3.Zero, Quaternion.Identity, voxelObject)
        {
            Scale = Vector3.One,
        };

        return Result<VoxelEntityModel>.FromSuccess(model);
    }
}
