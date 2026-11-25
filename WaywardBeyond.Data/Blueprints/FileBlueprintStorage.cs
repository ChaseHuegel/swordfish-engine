using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Migrations;

namespace WaywardBeyond.Data.Blueprints;

/// <summary>Stores player blueprints as atomic, palette-encoded <c>.wbp</c> files under the blueprints root.</summary>
public sealed class FileBlueprintStorage(
    ILogger<FileBlueprintStorage> logger,
    StoragePaths paths,
    Migrator migrator,
    GameplayConfig? gameplayConfig = null
) : IBlueprintStorage
{
    /// <summary>File extension for portable blueprint files.</summary>
    public const string FILE_EXTENSION = ".wbp";

    /// <summary>Maximum allowed blueprint display name length.</summary>
    public const int MAX_NAME_LENGTH = 64;

    /// <summary>Maximum allowed region dimension along any axis.</summary>
    public const int MAX_REGION_EDGE = 64;

    /// <summary>Maximum allowed total voxel volume for a saved blueprint.</summary>
    public const int MAX_REGION_VOLUME = 65_536;

    /// <summary>Maximum serialized <c>.wbp</c> blueprint file size in bytes.</summary>
    public const int MAX_FILE_BYTES = 4 * 1024 * 1024;

    private const int MAX_SLUG_LENGTH = 48;

    private static readonly Version _formatVersion = new(
        _DataVersion: DataVersion.CURRENT,
        _Name: "Wayward Beyond",
        _Environment: "Blueprint"
    );

    private readonly ILogger<FileBlueprintStorage> _logger = logger;
    private readonly StoragePaths _paths = paths;
    private readonly Migrator _migrator = migrator;
    private readonly GameplayConfig? _gameplayConfig = gameplayConfig;
    private readonly Lock _cacheLock = new();
    private readonly Dictionary<string, CachedSummary> _summaryCache = new(GetPathComparer());

    /// <inheritdoc/>
    public Result<IReadOnlyList<BlueprintSummary>> List()
    {
        try
        {
            string rootDir = GetNormalizedRootDirectory();
            if (!Directory.Exists(rootDir))
            {
                lock (_cacheLock)
                {
                    _summaryCache.Clear();
                }

                return Result<IReadOnlyList<BlueprintSummary>>.FromSuccess([]);
            }

            var entries = new List<BlueprintSummary>();
            var seenPaths = new HashSet<string>(GetPathComparer());
            foreach (string filePath in Directory.EnumerateFiles(rootDir, "*" + FILE_EXTENSION, SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(filePath);
                if (fileName.StartsWith('.'))
                {
                    continue;
                }

                seenPaths.Add(filePath);
                var fileInfo = new FileInfo(filePath);
                long length = fileInfo.Length;
                long lastWriteTicks = fileInfo.LastWriteTimeUtc.Ticks;

                if (TryGetCachedSummary(filePath, length, lastWriteTicks, out BlueprintSummary cached))
                {
                    entries.Add(cached);
                    continue;
                }

                string id = Path.GetFileNameWithoutExtension(fileName);
                Result<BlueprintData> loaded = ReadBlueprintFile(filePath, id);
                if (!loaded.Success)
                {
                    RemoveCachedSummary(filePath);
                    _logger.LogWarning("Skipping unreadable blueprint {id}: {message}", id, loaded.Message);
                    continue;
                }

                BlueprintSummary summary = ToSummary(id, loaded.Value);
                SetCachedSummary(filePath, length, lastWriteTicks, in summary);
                entries.Add(summary);
            }

            PruneCachedSummaries(seenPaths);

            entries.Sort(static (left, right) =>
            {
                int modifiedCompare = right.ModifiedAtMs.CompareTo(left.ModifiedAtMs);
                if (modifiedCompare != 0)
                {
                    return modifiedCompare;
                }

                int nameCompare = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
                return nameCompare != 0
                    ? nameCompare
                    : StringComparer.Ordinal.Compare(left.Id, right.Id);
            });

            return Result<IReadOnlyList<BlueprintSummary>>.FromSuccess(entries);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<BlueprintSummary>>.FromFailure(ex);
        }
    }

    /// <inheritdoc/>
    public Result<BlueprintData> Load(string id)
    {
        if (!TryResolveSafePath(id, out string filePath, out string error))
        {
            return Result<BlueprintData>.FromFailure(error);
        }

        if (!File.Exists(filePath))
        {
            return Result<BlueprintData>.FromFailure($"Blueprint '{id}' was not found.");
        }

        return ReadBlueprintFile(filePath, id);
    }

    /// <inheritdoc/>
    public Result<BlueprintSummary> Save(string name, in VoxelRegionData encodedRegion, int brickCount)
    {
        if (!TryValidateName(name, out string trimmedName, out string error))
        {
            return Result<BlueprintSummary>.FromFailure(error);
        }

        if (!IsValidRegionSize(encodedRegion.SizeX, encodedRegion.SizeY, encodedRegion.SizeZ) || encodedRegion.Chunks == null)
        {
            return Result<BlueprintSummary>.FromFailure("Blueprint region data has invalid bounds.");
        }

        if (brickCount < 0)
        {
            return Result<BlueprintSummary>.FromFailure("Blueprint brick count cannot be negative.");
        }

        try
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var data = new BlueprintData(
                _formatVersion,
                trimmedName,
                now,
                now,
                brickCount,
                encodedRegion.SizeX,
                encodedRegion.SizeY,
                encodedRegion.SizeZ,
                encodedRegion.Chunks,
                encodedRegion.BrickPalette,
                encodedRegion.PaintPalette
            );

            byte[] bytes = data.Serialize();
            int maxBytes = GetMaxFileBytes();
            if (maxBytes > 0 && bytes.Length > maxBytes)
            {
                return Result<BlueprintSummary>.FromFailure($"Blueprint exceeds maximum file size of {maxBytes} bytes.");
            }

            string rootDir = GetNormalizedRootDirectory();
            Directory.CreateDirectory(rootDir);

            string slug = SanitizeSlug(trimmedName);
            string id = AllocateUniqueId(rootDir, slug, excludingId: null);
            string targetPath = Path.Combine(rootDir, id + FILE_EXTENSION);

            WriteAtomic(rootDir, targetPath, bytes);
            BlueprintSummary summary = ToSummary(id, data);
            CacheWrittenFile(targetPath, in summary);
            return Result<BlueprintSummary>.FromSuccess(summary);
        }
        catch (Exception ex)
        {
            return Result<BlueprintSummary>.FromFailure(ex);
        }
    }

    /// <inheritdoc/>
    public Result<BlueprintSummary> Rename(string id, string newName)
    {
        if (!TryResolveSafePath(id, out string oldPath, out string pathError))
        {
            return Result<BlueprintSummary>.FromFailure(pathError);
        }

        if (!TryValidateName(newName, out string trimmedName, out string nameError))
        {
            return Result<BlueprintSummary>.FromFailure(nameError);
        }

        if (!File.Exists(oldPath))
        {
            return Result<BlueprintSummary>.FromFailure($"Blueprint '{id}' was not found.");
        }

        Result<BlueprintData> loaded = ReadBlueprintFile(oldPath, id);
        if (!loaded.Success)
        {
            return new Result<BlueprintSummary>(false, default!, loaded.Message, loaded.Exception);
        }

        try
        {
            BlueprintData existing = loaded.Value;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var updated = new BlueprintData(
                _formatVersion,
                trimmedName,
                existing.CreatedAtMs,
                now,
                existing.BrickCount,
                existing.SizeX,
                existing.SizeY,
                existing.SizeZ,
                existing.Chunks ?? [],
                existing.BrickPalette,
                existing.PaintPalette
            );

            string rootDir = GetNormalizedRootDirectory();
            string desiredSlug = SanitizeSlug(trimmedName);
            string targetId = string.Equals(desiredSlug, id, StringComparison.Ordinal)
                ? id
                : AllocateUniqueId(rootDir, desiredSlug, excludingId: id);
            string targetPath = Path.Combine(rootDir, targetId + FILE_EXTENSION);

            WriteAtomic(rootDir, targetPath, updated.Serialize());

            if (!string.Equals(targetPath, oldPath, GetPathComparison()) && File.Exists(oldPath))
            {
                File.Delete(oldPath);
                RemoveCachedSummary(oldPath);
            }

            BlueprintSummary summary = ToSummary(targetId, updated);
            CacheWrittenFile(targetPath, in summary);
            return Result<BlueprintSummary>.FromSuccess(summary);
        }
        catch (Exception ex)
        {
            return Result<BlueprintSummary>.FromFailure(ex);
        }
    }

    /// <inheritdoc/>
    public Result Delete(string id)
    {
        if (!TryResolveSafePath(id, out string filePath, out string error))
        {
            return Result.FromFailure(error);
        }

        if (!File.Exists(filePath))
        {
            return Result.FromFailure($"Blueprint '{id}' was not found.");
        }

        try
        {
            File.Delete(filePath);
            RemoveCachedSummary(filePath);
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    private Result<BlueprintData> ReadBlueprintFile(string filePath, string id)
    {
        try
        {
            var info = new FileInfo(filePath);
            int maxBytes = GetMaxFileBytes();
            if (maxBytes > 0 && info.Length > maxBytes)
            {
                return Result<BlueprintData>.FromFailure(
                    $"Blueprint '{id}' exceeds maximum file size of {maxBytes} bytes.");
            }

            byte[] bytes = File.ReadAllBytes(filePath);
            BlueprintData data = BlueprintData.Deserialize(bytes);

            uint version = data.Version.DataVersion;
            if (version > DataVersion.CURRENT || !_migrator.IsSupported<BlueprintData>(version))
            {
                return Result<BlueprintData>.FromFailure(
                    $"Blueprint '{id}' uses data version {version}, which is newer than the supported format version {DataVersion.CURRENT}."
                );
            }

            if (!IsValidRegionSize(data.SizeX, data.SizeY, data.SizeZ) || data.Chunks == null)
            {
                return Result<BlueprintData>.FromFailure($"Blueprint '{id}' contains invalid region bounds.");
            }

            BlueprintData migrated = _migrator.Migrate(data, version);
            return Result<BlueprintData>.FromSuccess(migrated);
        }
        catch (Exception ex)
        {
            return new Result<BlueprintData>(false, default!, $"Failed to deserialize blueprint '{id}': {ex.Message}", ex);
        }
    }

    private string GetNormalizedRootDirectory()
    {
        string raw = _paths.GetBlueprintsDirectory().Value;
        string full = Path.GetFullPath(raw);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private bool TryResolveSafePath(string id, out string fullPath, out string error)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "Blueprint identifier is required.";
            return false;
        }

        if (id.IndexOfAny(['/', '\\']) >= 0 || id.Contains("..", StringComparison.Ordinal))
        {
            error = "Blueprint identifier contains invalid path characters.";
            return false;
        }

        if (!string.Equals(Path.GetFileName(id), id, StringComparison.Ordinal))
        {
            error = "Blueprint identifier must be a single file name.";
            return false;
        }

        string rootDir = GetNormalizedRootDirectory();
        string candidate = Path.GetFullPath(Path.Combine(rootDir, id + FILE_EXTENSION));
        string rootPrefix = rootDir + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, GetPathComparison()))
        {
            error = "Blueprint path escapes the blueprints directory.";
            return false;
        }

        fullPath = candidate;
        error = string.Empty;
        return true;
    }

    private static bool TryValidateName(string name, out string trimmedName, out string error)
    {
        trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
        {
            error = "Blueprint name is required.";
            return false;
        }

        if (trimmedName.Length > MAX_NAME_LENGTH)
        {
            error = $"Blueprint name must be {MAX_NAME_LENGTH} characters or fewer.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string SanitizeSlug(string name)
    {
        var builder = new StringBuilder(Math.Min(name.Length, MAX_SLUG_LENGTH));
        var lastWasDash = false;

        for (var i = 0; i < name.Length && builder.Length < MAX_SLUG_LENGTH; i++)
        {
            char c = char.ToLowerInvariant(name[i]);
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        string slug = builder.ToString().Trim('-');
        return slug.Length > 0 ? slug : "blueprint";
    }

    private static string AllocateUniqueId(string rootDir, string baseSlug, string? excludingId)
    {
        string candidate = baseSlug;
        var suffix = 2;

        while (true)
        {
            if (excludingId != null && string.Equals(candidate, excludingId, GetPathComparison()))
            {
                return candidate;
            }

            string path = Path.Combine(rootDir, candidate + FILE_EXTENSION);
            if (!File.Exists(path))
            {
                return candidate;
            }

            candidate = $"{baseSlug}-{suffix++}";
        }
    }

    private int GetMaxRegionEdge() => _gameplayConfig?.Regions.MaxEdge.Get() ?? MAX_REGION_EDGE;

    private int GetMaxRegionVolume() => _gameplayConfig?.Regions.MaxVolume.Get() ?? MAX_REGION_VOLUME;

    private int GetMaxFileBytes() => _gameplayConfig?.Regions.MaxPayloadBytes.Get() ?? MAX_FILE_BYTES;

    private bool IsValidRegionSize(int sizeX, int sizeY, int sizeZ)
    {
        if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
        {
            return false;
        }

        int maxEdge = GetMaxRegionEdge();
        if (maxEdge > 0 && (sizeX > maxEdge || sizeY > maxEdge || sizeZ > maxEdge))
        {
            return false;
        }

        int maxVolume = GetMaxRegionVolume();
        if (maxVolume > 0)
        {
            long xy = (long)sizeX * sizeY;
            if (xy > maxVolume || xy * sizeZ > maxVolume)
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteAtomic(string rootDir, string targetPath, byte[] bytes)
    {
        string tempPath = Path.Combine(rootDir, $".tmp-{Guid.NewGuid():N}{FILE_EXTENSION}");
        try
        {
            File.WriteAllBytes(tempPath, bytes);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static BlueprintSummary ToSummary(string id, in BlueprintData data)
    {
        return new BlueprintSummary(
            id,
            data.Name,
            data.SizeX,
            data.SizeY,
            data.SizeZ,
            data.BrickCount,
            data.CreatedAtMs,
            data.ModifiedAtMs
        );
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    private bool TryGetCachedSummary(string filePath, long length, long lastWriteTicks, out BlueprintSummary summary)
    {
        lock (_cacheLock)
        {
            if (_summaryCache.TryGetValue(filePath, out CachedSummary entry)
                && entry.Length == length
                && entry.LastWriteUtcTicks == lastWriteTicks)
            {
                summary = entry.Summary;
                return true;
            }
        }

        summary = default;
        return false;
    }

    private void SetCachedSummary(string filePath, long length, long lastWriteTicks, in BlueprintSummary summary)
    {
        lock (_cacheLock)
        {
            _summaryCache[filePath] = new CachedSummary(length, lastWriteTicks, summary);
        }
    }

    private void CacheWrittenFile(string filePath, in BlueprintSummary summary)
    {
        var info = new FileInfo(filePath);
        if (info.Exists)
        {
            SetCachedSummary(filePath, info.Length, info.LastWriteTimeUtc.Ticks, in summary);
        }
    }

    private void RemoveCachedSummary(string filePath)
    {
        lock (_cacheLock)
        {
            _summaryCache.Remove(filePath);
        }
    }

    private void PruneCachedSummaries(HashSet<string> seenPaths)
    {
        lock (_cacheLock)
        {
            List<string>? stale = null;
            foreach (string key in _summaryCache.Keys)
            {
                if (!seenPaths.Contains(key))
                {
                    stale ??= [];
                    stale.Add(key);
                }
            }

            if (stale != null)
            {
                for (var i = 0; i < stale.Count; i++)
                {
                    _summaryCache.Remove(stale[i]);
                }
            }
        }
    }

    private readonly record struct CachedSummary(long Length, long LastWriteUtcTicks, BlueprintSummary Summary);
}
