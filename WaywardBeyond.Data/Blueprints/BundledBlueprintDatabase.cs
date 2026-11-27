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

/// <summary>
/// Loads bundled read-only <c>.wbp</c> blueprints from mounted module asset trees for both client and server.
/// </summary>
public sealed class BundledBlueprintDatabase
    : VirtualAssetDatabase<BlueprintData, BundledBlueprintDatabase.Entry, BlueprintData>,
      IBundledBlueprintDatabase,
      IAutoActivate
{
    private const string ROOT_PREFIX = "blueprints/";

    private readonly string? _userBlueprintsPrefix;
    private readonly Lock _catalogLock = new();
    private readonly Dictionary<string, BlueprintData> _blueprints = new(StringComparer.Ordinal);
    private readonly List<BlueprintSummary> _summaries = [];

    /// <summary>Constructs the bundled blueprint database from mounted module assets.</summary>
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
    protected override PathInfo GetRootPath() => new(ROOT_PREFIX);

    /// <inheritdoc/>
    protected override IEnumerable<Entry> GetAssetInfo(PathInfo path, BlueprintData resource)
    {
        yield return new Entry(path, resource);
    }

    /// <inheritdoc/>
    protected override string GetAssetID(Entry assetInfo)
    {
        string normalized = assetInfo.SourcePath.ToString().Replace('\\', '/');
        int indexOfRoot = normalized.LastIndexOf(ROOT_PREFIX, StringComparison.Ordinal);
        string relativePath = indexOfRoot >= 0
            ? normalized[(indexOfRoot + ROOT_PREFIX.Length)..]
            : Path.GetFileName(normalized);

        int extensionIndex = relativePath.LastIndexOf('.');
        return extensionIndex >= 0 ? relativePath[..extensionIndex] : relativePath;
    }

    /// <inheritdoc/>
    protected override Result<BlueprintData> LoadAsset(string id, Entry assetInfo)
    {
        BlueprintData raw = assetInfo.Data;
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

        return Result<BlueprintData>.FromSuccess(raw);
    }

    private static int CountSolidVoxels(in BlueprintData data)
    {
        if (data.Chunks == null || data.Chunks.Length == 0)
        {
            return 0;
        }

        byte chunkSize = data.Chunks[0].Chunk.Size;
        if (chunkSize == 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            return 0;
        }

        int shift = BitOperations.TrailingZeroCount(chunkSize);
        int shift2 = shift * 2;
        int mask = chunkSize - 1;
        var count = 0;

        for (var c = 0; c < data.Chunks.Length; c++)
        {
            ChunkInfo info = data.Chunks[c];
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
                if ((uint)x < (uint)data.SizeX && (uint)y < (uint)data.SizeY && (uint)z < (uint)data.SizeZ)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Pairs a parsed <see cref="BlueprintData"/> record with its virtual source path.</summary>
    public readonly struct Entry(PathInfo sourcePath, BlueprintData data)
    {
        /// <summary>Virtual source path of the blueprint file.</summary>
        public readonly PathInfo SourcePath = sourcePath;

        /// <summary>Deserialized blueprint payload.</summary>
        public readonly BlueprintData Data = data;
    }
}
