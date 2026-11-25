using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Storage;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Voxels;
using DataVersionType = WaywardBeyond.Data.Version;

namespace WaywardBeyond.AssetTools.Blueprints;

/// <summary>
///     Manages pack <c>.wbp</c> blueprint files including in-place rename, move, duplicate, delete, and palette-reencoded library transfer.
/// </summary>
public sealed class PackBlueprintManager
{
    private static readonly DataVersionType FORMAT_VERSION = new(
        _DataVersion: DataVersion.CURRENT,
        _Name: "Wayward Beyond",
        _Environment: "Blueprint");

    private readonly string _blueprintsRoot;
    private readonly AtomicFileSaver _atomicSaver;

    /// <summary>
    ///     Creates a pack blueprint manager rooted at <paramref name="blueprintsDirectory"/>.
    /// </summary>
    public PackBlueprintManager(string blueprintsDirectory, AtomicFileSaver? atomicSaver = null)
    {
        if (string.IsNullOrWhiteSpace(blueprintsDirectory))
        {
            throw new ArgumentException("Blueprints directory is required.", nameof(blueprintsDirectory));
        }

        _blueprintsRoot = Path.GetFullPath(blueprintsDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _atomicSaver = atomicSaver ?? new AtomicFileSaver();
    }

    /// <summary>
    ///     Lists all valid <c>.wbp</c> blueprints under the pack blueprints root ordered by relative path ID.
    /// </summary>
    public Result<IReadOnlyList<PackBlueprintEntry>> List()
    {
        if (!Directory.Exists(_blueprintsRoot))
        {
            return Result<IReadOnlyList<PackBlueprintEntry>>.FromSuccess([]);
        }

        try
        {
            string[] files = Directory.GetFiles(_blueprintsRoot, "*" + FileBlueprintStorage.FILE_EXTENSION, SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            List<PackBlueprintEntry> entries = new(files.Length);
            for (int i = 0; i < files.Length; i++)
            {
                string fullPath = Path.GetFullPath(files[i]);
                if (Path.GetFileName(fullPath).StartsWith('.'))
                {
                    continue;
                }

                string relativeId = ToRelativeId(fullPath);
                Result<BlueprintData> loaded = ReadAndValidateFile(fullPath, relativeId);
                if (!loaded.Success)
                {
                    return Result<IReadOnlyList<PackBlueprintEntry>>.FromFailure(loaded.Message);
                }

                long fileBytes = new FileInfo(fullPath).Length;
                entries.Add(ToEntry(relativeId, fullPath, loaded.Value, fileBytes));
            }

            return Result<IReadOnlyList<PackBlueprintEntry>>.FromSuccess(entries);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<PackBlueprintEntry>>.FromFailure(ex);
        }
    }

    /// <summary>
    ///     Loads and validates the pack blueprint identified by <paramref name="relativeId"/>.
    /// </summary>
    public Result<BlueprintData> Load(string relativeId)
    {
        Result<string> pathResult = ResolveSafeBlueprintPath(relativeId);
        if (!pathResult.Success)
        {
            return Result<BlueprintData>.FromFailure(pathResult.Message);
        }

        if (!File.Exists(pathResult.Value))
        {
            return Result<BlueprintData>.FromFailure($"Blueprint '{relativeId}' was not found.");
        }

        return ReadAndValidateFile(pathResult.Value, NormalizeRelativeId(relativeId));
    }

    /// <summary>
    ///     Validates and atomically saves <paramref name="data"/> at <paramref name="relativeId"/>.
    /// </summary>
    public Result<PackBlueprintEntry> Save(string relativeId, in BlueprintData data)
    {
        Result<string> pathResult = ResolveSafeBlueprintPath(relativeId);
        if (!pathResult.Success)
        {
            return Result<PackBlueprintEntry>.FromFailure(pathResult.Message);
        }

        Result<string> nameResult = ValidateDisplayName(data.Name);
        if (!nameResult.Success)
        {
            return Result<PackBlueprintEntry>.FromFailure(nameResult.Message);
        }

        if (!IsValidRegionBounds(data.SizeX, data.SizeY, data.SizeZ) || data.Chunks == null)
        {
            return Result<PackBlueprintEntry>.FromFailure("Blueprint region dimensions exceed FileBlueprintStorage limits.");
        }

        if (data.BrickCount < 0)
        {
            return Result<PackBlueprintEntry>.FromFailure("Blueprint brick count cannot be negative.");
        }

        byte[] serialized = data.Serialize();
        if (serialized.Length > FileBlueprintStorage.MAX_FILE_BYTES)
        {
            return Result<PackBlueprintEntry>.FromFailure(
                $"Blueprint exceeds maximum file size of {FileBlueprintStorage.MAX_FILE_BYTES} bytes.");
        }

        var request = new FileSaveRequest(pathResult.Value, serialized);
        AtomicSaveResult saved = _atomicSaver.Save(in request);
        if (!saved.Success)
        {
            return Result<PackBlueprintEntry>.FromFailure(saved.ErrorMessage);
        }

        string normalizedId = NormalizeRelativeId(relativeId);
        return Result<PackBlueprintEntry>.FromSuccess(ToEntry(normalizedId, pathResult.Value, in data, serialized.Length));
    }

    /// <summary>
    ///     Renames only the embedded display name in place without moving the file or changing its pack ID.
    /// </summary>
    public Result<PackBlueprintEntry> RenameDisplayName(string relativeId, string newName)
    {
        Result<string> nameResult = ValidateDisplayName(newName);
        if (!nameResult.Success)
        {
            return Result<PackBlueprintEntry>.FromFailure(nameResult.Message);
        }

        Result<BlueprintData> loaded = Load(relativeId);
        if (!loaded.Success)
        {
            return Result<PackBlueprintEntry>.FromFailure(loaded.Message);
        }

        BlueprintData existing = loaded.Value;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var updated = new BlueprintData(
            FORMAT_VERSION,
            nameResult.Value,
            existing.CreatedAtMs,
            now,
            existing.BrickCount,
            existing.SizeX,
            existing.SizeY,
            existing.SizeZ,
            existing.Chunks ?? [],
            existing.BrickPalette,
            existing.PaintPalette);

        return Save(relativeId, in updated);
    }

    /// <summary>
    ///     Moves a pack blueprint file to <paramref name="newRelativeId"/> and warns that the pack ID changed.
    /// </summary>
    public Result<PackBlueprintMoveResult> Move(string oldRelativeId, string newRelativeId)
    {
        Result<string> oldPathResult = ResolveSafeBlueprintPath(oldRelativeId);
        if (!oldPathResult.Success)
        {
            return Result<PackBlueprintMoveResult>.FromFailure(oldPathResult.Message);
        }

        Result<string> newPathResult = ResolveSafeBlueprintPath(newRelativeId);
        if (!newPathResult.Success)
        {
            return Result<PackBlueprintMoveResult>.FromFailure(newPathResult.Message);
        }

        if (!File.Exists(oldPathResult.Value))
        {
            return Result<PackBlueprintMoveResult>.FromFailure($"Blueprint '{oldRelativeId}' was not found.");
        }

        if (File.Exists(newPathResult.Value))
        {
            return Result<PackBlueprintMoveResult>.FromFailure($"Destination blueprint '{newRelativeId}' already exists.");
        }

        Result<BlueprintData> loaded = ReadAndValidateFile(oldPathResult.Value, NormalizeRelativeId(oldRelativeId));
        if (!loaded.Success)
        {
            return Result<PackBlueprintMoveResult>.FromFailure(loaded.Message);
        }

        try
        {
            string? targetDir = Path.GetDirectoryName(newPathResult.Value);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            File.Move(oldPathResult.Value, newPathResult.Value);
            string oldNorm = NormalizeRelativeId(oldRelativeId);
            string newNorm = NormalizeRelativeId(newRelativeId);
            long fileSize = new FileInfo(newPathResult.Value).Length;
            PackBlueprintEntry entry = ToEntry(newNorm, newPathResult.Value, loaded.Value, fileSize);
            string notice = $"Moved blueprint pack ID from '{oldNorm}' to '{newNorm}'. Update references to the old path.";
            return Result<PackBlueprintMoveResult>.FromSuccess(new PackBlueprintMoveResult(entry, oldNorm, newNorm, notice));
        }
        catch (Exception ex)
        {
            return Result<PackBlueprintMoveResult>.FromFailure(ex);
        }
    }

    /// <summary>
    ///     Duplicates a pack blueprint to a new relative path with an optional replacement display name.
    /// </summary>
    public Result<PackBlueprintEntry> Duplicate(string sourceRelativeId, string destinationRelativeId, string? newName = null)
    {
        Result<string> destPath = ResolveSafeBlueprintPath(destinationRelativeId);
        if (!destPath.Success)
        {
            return Result<PackBlueprintEntry>.FromFailure(destPath.Message);
        }

        if (File.Exists(destPath.Value))
        {
            return Result<PackBlueprintEntry>.FromFailure($"Destination blueprint '{destinationRelativeId}' already exists.");
        }

        Result<BlueprintData> loaded = Load(sourceRelativeId);
        if (!loaded.Success)
        {
            return Result<PackBlueprintEntry>.FromFailure(loaded.Message);
        }

        BlueprintData source = loaded.Value;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string copyName = string.IsNullOrWhiteSpace(newName) ? source.Name : newName.Trim();
        var copy = new BlueprintData(
            FORMAT_VERSION,
            copyName,
            now,
            now,
            source.BrickCount,
            source.SizeX,
            source.SizeY,
            source.SizeZ,
            source.Chunks ?? [],
            source.BrickPalette,
            source.PaintPalette);

        return Save(destinationRelativeId, in copy);
    }

    /// <summary>
    ///     Deletes the pack blueprint identified by <paramref name="relativeId"/>.
    /// </summary>
    public Result Delete(string relativeId)
    {
        Result<string> pathResult = ResolveSafeBlueprintPath(relativeId);
        if (!pathResult.Success)
        {
            return Result.FromFailure(pathResult.Message);
        }

        if (!File.Exists(pathResult.Value))
        {
            return Result.FromFailure($"Blueprint '{relativeId}' was not found.");
        }

        try
        {
            File.Delete(pathResult.Value);
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    /// <summary>
    ///     Imports a player library <c>.wbp</c> file into the pack, keeping its name and re-encoding palettes through <see cref="VoxelCodec"/>.
    /// </summary>
    public Result<PackBlueprintEntry> ImportFromPlayerLibrary(
        string playerBlueprintFilePath,
        string targetRelativeId,
        IBrickRegistry sourceBricks,
        IBrickRegistry targetBricks,
        IPaintRegistry? sourcePaints = null,
        IPaintRegistry? targetPaints = null)
    {
        if (string.IsNullOrWhiteSpace(playerBlueprintFilePath) || !File.Exists(playerBlueprintFilePath))
        {
            return Result<PackBlueprintEntry>.FromFailure($"Player blueprint file not found: '{playerBlueprintFilePath}'.");
        }

        if (sourceBricks == null || targetBricks == null)
        {
            return Result<PackBlueprintEntry>.FromFailure("Source and target brick registries are required for palette re-encoding.");
        }

        Result<BlueprintData> loaded = ReadAndValidateFile(playerBlueprintFilePath, Path.GetFileName(playerBlueprintFilePath));
        if (!loaded.Success)
        {
            return Result<PackBlueprintEntry>.FromFailure(loaded.Message);
        }

        BlueprintData reencoded = ReencodeBlueprintPalettes(loaded.Value, sourceBricks, targetBricks, sourcePaints, targetPaints);
        return Save(targetRelativeId, in reencoded);
    }

    /// <summary>
    ///     Exports a pack blueprint to a player library directory after re-encoding palettes through <see cref="VoxelCodec"/>.
    /// </summary>
    public Result<string> ExportToPlayerLibrary(
        string relativeId,
        string playerLibraryDirectory,
        IBrickRegistry sourceBricks,
        IBrickRegistry targetBricks,
        IPaintRegistry? sourcePaints = null,
        IPaintRegistry? targetPaints = null)
    {
        if (string.IsNullOrWhiteSpace(playerLibraryDirectory))
        {
            return Result<string>.FromFailure("Player library directory is required.");
        }

        if (sourceBricks == null || targetBricks == null)
        {
            return Result<string>.FromFailure("Source and target brick registries are required for palette re-encoding.");
        }

        Result<BlueprintData> loaded = Load(relativeId);
        if (!loaded.Success)
        {
            return Result<string>.FromFailure(loaded.Message);
        }

        BlueprintData reencoded = ReencodeBlueprintPalettes(loaded.Value, sourceBricks, targetBricks, sourcePaints, targetPaints);
        byte[] serialized = reencoded.Serialize();
        if (serialized.Length > FileBlueprintStorage.MAX_FILE_BYTES)
        {
            return Result<string>.FromFailure($"Blueprint exceeds maximum file size of {FileBlueprintStorage.MAX_FILE_BYTES} bytes.");
        }

        string fileName = Path.GetFileName(NormalizeRelativeId(relativeId)) + FileBlueprintStorage.FILE_EXTENSION;
        string destinationPath = Path.Combine(Path.GetFullPath(playerLibraryDirectory), fileName);
        var saveReq = new FileSaveRequest(destinationPath, serialized);
        AtomicSaveResult saved = _atomicSaver.Save(in saveReq);
        return saved.Success
            ? Result<string>.FromSuccess(destinationPath)
            : Result<string>.FromFailure(saved.ErrorMessage);
    }

    private static BlueprintData ReencodeBlueprintPalettes(
        in BlueprintData source,
        IBrickRegistry sourceBricks,
        IBrickRegistry targetBricks,
        IPaintRegistry? sourcePaints,
        IPaintRegistry? targetPaints)
    {
        VoxelRegionData region = source.ToRegion();
        VoxelRegionData withPortablePalette = VoxelCodec.Encode(in region, sourceBricks, sourcePaints);
        VoxelRegionData decodedForTarget = VoxelCodec.Decode(in withPortablePalette, targetBricks, targetPaints);
        VoxelRegionData reencoded = VoxelCodec.Encode(in decodedForTarget, targetBricks, targetPaints);

        return new BlueprintData(
            FORMAT_VERSION,
            source.Name,
            source.CreatedAtMs,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            source.BrickCount,
            reencoded.SizeX,
            reencoded.SizeY,
            reencoded.SizeZ,
            reencoded.Chunks ?? [],
            reencoded.BrickPalette,
            reencoded.PaintPalette);
    }

    private static Result<BlueprintData> ReadAndValidateFile(string filePath, string idLabel)
    {
        try
        {
            var info = new FileInfo(filePath);
            if (info.Length > FileBlueprintStorage.MAX_FILE_BYTES)
            {
                return Result<BlueprintData>.FromFailure(
                    $"Blueprint '{idLabel}' exceeds maximum file size of {FileBlueprintStorage.MAX_FILE_BYTES} bytes.");
            }

            byte[] bytes = File.ReadAllBytes(filePath);
            BlueprintData data = BlueprintData.Deserialize(bytes);

            if (data.Version.DataVersion > DataVersion.CURRENT)
            {
                return Result<BlueprintData>.FromFailure(
                    $"Blueprint '{idLabel}' uses data version {data.Version.DataVersion}, newer than {DataVersion.CURRENT}.");
            }

            if (!IsValidRegionBounds(data.SizeX, data.SizeY, data.SizeZ) || data.Chunks == null)
            {
                return Result<BlueprintData>.FromFailure($"Blueprint '{idLabel}' has invalid region bounds.");
            }

            Result<string> nameValid = ValidateDisplayName(data.Name);
            if (!nameValid.Success)
            {
                return Result<BlueprintData>.FromFailure($"Blueprint '{idLabel}': {nameValid.Message}");
            }

            return Result<BlueprintData>.FromSuccess(data);
        }
        catch (Exception ex)
        {
            return Result<BlueprintData>.FromFailure(ex);
        }
    }

    private Result<string> ResolveSafeBlueprintPath(string relativeId)
    {
        if (string.IsNullOrWhiteSpace(relativeId))
        {
            return Result<string>.FromFailure("Blueprint relative path is required.");
        }

        string normalized = NormalizeRelativeId(relativeId);
        if (normalized.StartsWith('/') || normalized.Contains("..", StringComparison.Ordinal))
        {
            return Result<string>.FromFailure($"Blueprint path '{relativeId}' contains invalid traversal segments.");
        }

        string combined = Path.GetFullPath(Path.Combine(
            _blueprintsRoot,
            normalized.Replace('/', Path.DirectorySeparatorChar) + FileBlueprintStorage.FILE_EXTENSION));
        string prefix = _blueprintsRoot + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!combined.StartsWith(prefix, comparison))
        {
            return Result<string>.FromFailure($"Blueprint path '{relativeId}' escapes the blueprints directory.");
        }

        return Result<string>.FromSuccess(combined);
    }

    private string ToRelativeId(string fullPath)
    {
        string relative = Path.GetRelativePath(_blueprintsRoot, fullPath).Replace('\\', '/');
        if (relative.EndsWith(FileBlueprintStorage.FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
        {
            relative = relative.Substring(0, relative.Length - FileBlueprintStorage.FILE_EXTENSION.Length);
        }

        return relative;
    }

    private static string NormalizeRelativeId(string relativeId)
    {
        string normalized = relativeId.Trim().Replace('\\', '/');
        if (normalized.EndsWith(FileBlueprintStorage.FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - FileBlueprintStorage.FILE_EXTENSION.Length);
        }

        return normalized;
    }

    private static Result<string> ValidateDisplayName(string? name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return Result<string>.FromFailure("Blueprint name is required.");
        }

        if (trimmed.Length > FileBlueprintStorage.MAX_NAME_LENGTH)
        {
            return Result<string>.FromFailure(
                $"Blueprint name must be {FileBlueprintStorage.MAX_NAME_LENGTH} characters or fewer.");
        }

        return Result<string>.FromSuccess(trimmed);
    }

    private static bool IsValidRegionBounds(int sizeX, int sizeY, int sizeZ)
    {
        if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
        {
            return false;
        }

        if (sizeX > FileBlueprintStorage.MAX_REGION_EDGE ||
            sizeY > FileBlueprintStorage.MAX_REGION_EDGE ||
            sizeZ > FileBlueprintStorage.MAX_REGION_EDGE)
        {
            return false;
        }

        long volume = (long)sizeX * sizeY * sizeZ;
        return volume <= FileBlueprintStorage.MAX_REGION_VOLUME;
    }

    private static PackBlueprintEntry ToEntry(string relativeId, string filePath, in BlueprintData data, long fileSizeBytes)
    {
        return new PackBlueprintEntry(
            relativeId,
            filePath,
            data.Name,
            data.SizeX,
            data.SizeY,
            data.SizeZ,
            data.BrickCount,
            data.CreatedAtMs,
            data.ModifiedAtMs,
            fileSizeBytes);
    }
}
