using System.Diagnostics;
using Swordfish.Library.Util;

namespace WaywardBeyond.AssetTools.Storage;

/// <summary>
///     Writes files atomically via same-directory temp file, disk flush, atomic move, and byte verification.
/// </summary>
public sealed class AtomicFileSaver
{
    /// <summary>
    ///     Checks whether the destination file conflicts with the expected base hash on disk.
    /// </summary>
    public Result<FileSaveConflict?> CheckConflict(in FileSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DestinationPath))
        {
            return Result<FileSaveConflict?>.FromFailure("Destination path is required.");
        }

        if (request.ExpectedBaseSha256 == null && !request.ExpectFileNotToExist)
        {
            return Result<FileSaveConflict?>.FromSuccess(null);
        }

        string fullPath = Path.GetFullPath(request.DestinationPath);
        Result<string?> diskHashResult = ContentHash.ComputeFileSha256Hex(fullPath);
        if (!diskHashResult.Success)
        {
            return Result<FileSaveConflict?>.FromFailure(diskHashResult.Message);
        }

        string? diskHash = diskHashResult.Value;
        if (request.ExpectFileNotToExist)
        {
            if (diskHash != null)
            {
                var created = new FileSaveConflict(fullPath, null, diskHash, FileSaveConflictReason.CreatedExternally);
                return Result<FileSaveConflict?>.FromSuccess(created);
            }

            return Result<FileSaveConflict?>.FromSuccess(null);
        }

        if (diskHash == null)
        {
            var deleted = new FileSaveConflict(fullPath, request.ExpectedBaseSha256, null, FileSaveConflictReason.DeletedExternally);
            return Result<FileSaveConflict?>.FromSuccess(deleted);
        }

        if (!string.Equals(diskHash, request.ExpectedBaseSha256, StringComparison.OrdinalIgnoreCase))
        {
            var modified = new FileSaveConflict(fullPath, request.ExpectedBaseSha256, diskHash, FileSaveConflictReason.ModifiedExternally);
            return Result<FileSaveConflict?>.FromSuccess(modified);
        }

        return Result<FileSaveConflict?>.FromSuccess(null);
    }

    /// <summary>
    ///     Atomically writes <paramref name="request"/> to disk after verifying no external modification occurred.
    /// </summary>
    public AtomicSaveResult Save(in FileSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DestinationPath))
        {
            return AtomicSaveResult.FromFailure("Destination path is required.");
        }

        if (request.Content == null)
        {
            return AtomicSaveResult.FromFailure("Content buffer is required.");
        }

        Result<FileSaveConflict?> conflictResult = CheckConflict(in request);
        if (!conflictResult.Success)
        {
            return AtomicSaveResult.FromFailure(conflictResult.Message ?? "Failed to verify disk file hash.");
        }

        if (conflictResult.Value.HasValue)
        {
            return AtomicSaveResult.FromConflict(conflictResult.Value.Value);
        }

        string fullPath = Path.GetFullPath(request.DestinationPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            return AtomicSaveResult.FromFailure($"Cannot resolve parent directory for '{fullPath}'.");
        }

        string tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);

            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(request.Content, 0, request.Content.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, fullPath, overwrite: true);

            byte[] writtenBytes = File.ReadAllBytes(fullPath);
            if (!writtenBytes.AsSpan().SequenceEqual(request.Content))
            {
                return AtomicSaveResult.FromFailure($"Post-write verification failed for '{fullPath}'.");
            }

            string writtenHash = ContentHash.ComputeSha256Hex(writtenBytes);
            return AtomicSaveResult.FromSuccess(writtenHash);
        }
        catch (Exception ex)
        {
            CleanupTempFile(tempPath);
            return AtomicSaveResult.FromFailure(ex);
        }
    }

    private static void CleanupTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException ex)
        {
            Trace.TraceWarning($"Failed to delete temporary save file '{tempPath}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Trace.TraceWarning($"Permission denied deleting temporary save file '{tempPath}': {ex.Message}");
        }
    }
}
