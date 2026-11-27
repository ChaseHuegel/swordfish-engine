using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Blueprints;

/// <summary>Loads bundled read-only <c>.wbp</c> blueprint files from <c>blueprints/</c> in the virtual file system.</summary>
internal sealed class BundledBlueprintDatabase : VirtualAssetDatabase<BlueprintData, Resource<BlueprintData>, BlueprintData>, IAutoActivate, IBundledBlueprintDatabase
{
    private const string VIRTUAL_ROOT = "blueprints/";

    private readonly string? _userBlueprintsPrefix;
    private readonly Lock _catalogLock = new();
    private readonly List<BlueprintSummary> _summaries = [];

    /// <summary>Creates the bundled blueprint database and eagerly indexes mounted <c>.wbp</c> assets.</summary>
    public BundledBlueprintDatabase(
        in ILogger<BundledBlueprintDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        StoragePaths? storagePaths = null
    ) : base(logger, fileParseService, vfs)
    {
        if (storagePaths != null)
        {
            string full = Path.GetFullPath(storagePaths.GetBlueprintsDirectory().Value)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _userBlueprintsPrefix = full + Path.DirectorySeparatorChar;
        }

        Load();
    }

    /// <inheritdoc/>
    public IReadOnlyList<BlueprintSummary> ListSummaries()
    {
        lock (_catalogLock)
        {
            return [.. _summaries];
        }
    }

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
    protected override PathInfo GetRootPath() => new(VIRTUAL_ROOT);

    /// <inheritdoc/>
    protected override IEnumerable<Resource<BlueprintData>> GetAssetInfo(PathInfo path, BlueprintData resource)
    {
        yield return new Resource<BlueprintData>(path, resource);
    }

    /// <inheritdoc/>
    protected override string GetAssetID(Resource<BlueprintData> assetInfo)
    {
        string normalized = assetInfo.SourcePath.ToString().Replace('\\', '/');
        int indexOfRoot = normalized.LastIndexOf(VIRTUAL_ROOT, StringComparison.Ordinal);
        string relativePath = indexOfRoot >= 0
            ? normalized[(indexOfRoot + VIRTUAL_ROOT.Length)..]
            : normalized;

        int extensionIndex = relativePath.LastIndexOf('.');
        return extensionIndex != -1 ? relativePath[..extensionIndex] : relativePath;
    }

    /// <inheritdoc/>
    protected override Result<BlueprintData> LoadAsset(string id, Resource<BlueprintData> assetInfo)
    {
        BlueprintData raw = assetInfo.Value;
        if (raw.SizeX <= 0 || raw.SizeY <= 0 || raw.SizeZ <= 0 || raw.Chunks == null)
        {
            return Result<BlueprintData>.FromFailure($"Bundled blueprint \"{id}\" has invalid bounds.");
        }

        int brickCount = raw.BrickCount > 0 ? raw.BrickCount : CountSolidVoxels(in raw);
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

        return Result<BlueprintData>.FromSuccess(raw);
    }

    private static int CountSolidVoxels(in BlueprintData blueprint)
    {
        ChunkInfo[]? chunks = blueprint.Chunks;
        if (chunks == null || chunks.Length == 0)
        {
            return 0;
        }

        byte chunkSize = chunks[0].Chunk.Size;
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            return 0;
        }

        int shift = BitOperations.TrailingZeroCount(chunkSize);
        int shift2 = shift * 2;
        int mask = chunkSize - 1;
        var count = 0;

        for (var c = 0; c < chunks.Length; c++)
        {
            ChunkInfo info = chunks[c];
            Voxel[]? voxels = info.Chunk.Voxels;
            if (voxels == null || info.Chunk.Size != chunkSize)
            {
                continue;
            }

            int baseX = info.OffsetX * chunkSize;
            int baseY = info.OffsetY * chunkSize;
            int baseZ = info.OffsetZ * chunkSize;

            for (var i = 0; i < voxels.Length; i++)
            {
                if (voxels[i].ID == 0)
                {
                    continue;
                }

                int x = baseX + (i & mask);
                int y = baseY + ((i >> shift) & mask);
                int z = baseZ + (i >> shift2);
                if ((uint)x < (uint)blueprint.SizeX && (uint)y < (uint)blueprint.SizeY && (uint)z < (uint)blueprint.SizeZ)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
