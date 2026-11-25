using System.Diagnostics;
using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;

namespace WaywardBeyond.AssetTools.Storage;

/// <summary>
///     Applies multi-file saves with a persistent recovery manifest and reverse-order rollback on failure.
/// </summary>
public sealed class MultiFileSaver
{
    private const string MANIFEST_FILE_NAME = "pending-save-manifest.toml";

    private readonly string _recoveryDirectory;
    private readonly AtomicFileSaver _atomicSaver;
    private readonly Func<FileSaveRequest, AtomicSaveResult> _saveOperation;

    /// <summary>
    ///     Creates a multi-file saver storing transaction recovery manifests under <paramref name="recoveryDirectory"/>.
    /// </summary>
    public MultiFileSaver(string recoveryDirectory, AtomicFileSaver? atomicSaver = null)
    {
        if (string.IsNullOrWhiteSpace(recoveryDirectory))
        {
            throw new ArgumentException("Recovery directory is required.", nameof(recoveryDirectory));
        }

        _recoveryDirectory = Path.GetFullPath(recoveryDirectory);
        _atomicSaver = atomicSaver ?? new AtomicFileSaver();
        _saveOperation = request => _atomicSaver.Save(in request);
    }

    /// <summary>
    ///     Creates a multi-file saver with a custom single-file write delegate for failure-injection testing.
    /// </summary>
    internal MultiFileSaver(
        string recoveryDirectory,
        AtomicFileSaver atomicSaver,
        Func<FileSaveRequest, AtomicSaveResult> saveOperation)
    {
        _recoveryDirectory = Path.GetFullPath(recoveryDirectory);
        _atomicSaver = atomicSaver ?? throw new ArgumentNullException(nameof(atomicSaver));
        _saveOperation = saveOperation ?? throw new ArgumentNullException(nameof(saveOperation));
    }

    /// <summary>
    ///     Gets the full path to the active transaction recovery manifest file.
    /// </summary>
    public string ManifestFilePath => Path.Combine(_recoveryDirectory, MANIFEST_FILE_NAME);

    /// <summary>
    ///     Returns true when an uncompleted multi-file save recovery manifest exists on disk.
    /// </summary>
    public bool HasPendingRecoveryManifest()
    {
        return File.Exists(ManifestFilePath);
    }

    /// <summary>
    ///     Atomically applies all file writes in order, rolling back in reverse order if any file fails.
    /// </summary>
    public MultiFileSaveResult SaveAll(IReadOnlyList<FileSaveRequest> requests)
    {
        if (requests == null || requests.Count == 0)
        {
            return MultiFileSaveResult.FromFailure("At least one file save request is required.");
        }

        Result validation = ValidateRequests(requests);
        if (!validation.Success)
        {
            return MultiFileSaveResult.FromFailure(validation.Message ?? "Invalid save request batch.");
        }

        List<FileSaveConflict> conflicts = [];
        for (int i = 0; i < requests.Count; i++)
        {
            FileSaveRequest request = requests[i];
            Result<FileSaveConflict?> conflictCheck = _atomicSaver.CheckConflict(in request);
            if (!conflictCheck.Success)
            {
                return MultiFileSaveResult.FromFailure(conflictCheck.Message ?? "Failed to check file conflict.");
            }

            if (conflictCheck.Value.HasValue)
            {
                conflicts.Add(conflictCheck.Value.Value);
            }
        }

        if (conflicts.Count > 0)
        {
            return MultiFileSaveResult.FromConflicts(conflicts);
        }

        string transactionId = Guid.NewGuid().ToString("N");
        string backupDirectory = Path.Combine(_recoveryDirectory, $"tx-{transactionId}");
        List<RecoveryManifestEntry> entries = new(requests.Count);

        Result prepareResult = PrepareBackupsAndManifest(requests, backupDirectory, entries);
        if (!prepareResult.Success)
        {
            CleanupDirectory(backupDirectory);
            return MultiFileSaveResult.FromFailure(prepareResult.Message ?? "Failed to write recovery manifest.", prepareResult.Exception);
        }

        List<string> writtenPaths = new(requests.Count);
        for (int i = 0; i < requests.Count; i++)
        {
            FileSaveRequest request = requests[i];
            AtomicSaveResult saveResult = _saveOperation(request);
            if (saveResult.Success)
            {
                writtenPaths.Add(Path.GetFullPath(request.DestinationPath));
                continue;
            }

            (bool rolledBack, List<string> rolledBackPaths, string? rollbackError) = RollbackAppliedEntries(entries, i - 1);
            if (rolledBack)
            {
                CleanupTransactionArtifacts(backupDirectory);
            }

            string failureMessage = saveResult.ErrorMessage ?? $"Failed saving '{request.DestinationPath}'.";
            if (rollbackError != null)
            {
                failureMessage = $"{failureMessage} Rollback error: {rollbackError}";
            }

            return MultiFileSaveResult.FromRollback(rolledBack, rolledBackPaths, failureMessage, saveResult.Exception);
        }

        CleanupTransactionArtifacts(backupDirectory);
        return MultiFileSaveResult.FromSuccess(writtenPaths);
    }

    /// <summary>
    ///     Rolls back any pending transaction recorded in the recovery manifest in reverse order.
    /// </summary>
    public Result RecoverPendingTransaction()
    {
        if (!HasPendingRecoveryManifest())
        {
            return Result.FromSuccess();
        }

        Result<TomlDocument> docResult = TomlDocument.Load(ManifestFilePath);
        if (!docResult.Success)
        {
            return Result.FromFailure(docResult.Message ?? "Failed to read recovery manifest.");
        }

        TomlDocument document = docResult.Value;
        if (document.HasSyntaxErrors)
        {
            return Result.FromFailure("Recovery manifest contains invalid TOML syntax.");
        }

        string backupDirectory = string.Empty;
        Result<TomlFieldSpan> backupDirField = document.GetRootField("BackupDirectory");
        if (backupDirField.Success && backupDirField.Value.TypedValue is string parsedBackupDir)
        {
            backupDirectory = parsedBackupDir;
        }

        IReadOnlyList<TomlTableSpan> entryTables = document.GetTables("Entries");
        List<RecoveryManifestEntry> entries = new(entryTables.Count);
        for (int i = 0; i < entryTables.Count; i++)
        {
            Result<RecoveryManifestEntry> parsedEntry = ParseManifestEntry(document, entryTables[i]);
            if (!parsedEntry.Success)
            {
                return Result.FromFailure(parsedEntry.Message);
            }

            entries.Add(parsedEntry.Value);
        }

        (bool rolledBack, _, string? rollbackError) = RollbackAppliedEntries(entries, entries.Count - 1);
        if (!rolledBack)
        {
            return Result.FromFailure(rollbackError ?? "Failed to roll back pending recovery manifest.");
        }

        CleanupTransactionArtifacts(backupDirectory);
        return Result.FromSuccess();
    }

    private Result PrepareBackupsAndManifest(
        IReadOnlyList<FileSaveRequest> requests,
        string backupDirectory,
        List<RecoveryManifestEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(_recoveryDirectory);
            Directory.CreateDirectory(backupDirectory);

            for (int i = 0; i < requests.Count; i++)
            {
                Result<RecoveryManifestEntry> entryResult = SnapshotTargetFile(requests[i].DestinationPath, backupDirectory, i);
                if (!entryResult.Success)
                {
                    return Result.FromFailure(entryResult.Message);
                }

                entries.Add(entryResult.Value);
            }

            string manifestToml = SerializeManifest(backupDirectory, entries);
            var manifestRequest = new FileSaveRequest(ManifestFilePath, Encoding.UTF8.GetBytes(manifestToml));
            AtomicSaveResult manifestSave = _atomicSaver.Save(in manifestRequest);
            return manifestSave.ToResult();
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    private Result<RecoveryManifestEntry> SnapshotTargetFile(string destinationPath, string backupDirectory, int index)
    {
        string fullPath = Path.GetFullPath(destinationPath);
        if (!File.Exists(fullPath))
        {
            return Result<RecoveryManifestEntry>.FromSuccess(new RecoveryManifestEntry(fullPath, false, string.Empty));
        }

        string backupPath = Path.Combine(backupDirectory, $"{index:D4}.bak");
        byte[] originalBytes = File.ReadAllBytes(fullPath);
        var backupRequest = new FileSaveRequest(backupPath, originalBytes);
        AtomicSaveResult backupSave = _atomicSaver.Save(in backupRequest);
        if (!backupSave.Success)
        {
            return Result<RecoveryManifestEntry>.FromFailure(backupSave.ErrorMessage ?? $"Failed creating backup for '{fullPath}'.");
        }

        return Result<RecoveryManifestEntry>.FromSuccess(new RecoveryManifestEntry(fullPath, true, backupPath));
    }

    private (bool Success, List<string> RolledBackPaths, string? ErrorMessage) RollbackAppliedEntries(
        IReadOnlyList<RecoveryManifestEntry> entries,
        int lastAppliedIndex)
    {
        List<string> rolledBackPaths = [];
        for (int i = lastAppliedIndex; i >= 0; i--)
        {
            RecoveryManifestEntry entry = entries[i];
            Result entryRollback = RollbackSingleEntry(in entry);
            if (!entryRollback.Success)
            {
                return (false, rolledBackPaths, entryRollback.Message);
            }

            rolledBackPaths.Add(entry.DestinationPath);
        }

        return (true, rolledBackPaths, null);
    }

    private Result RollbackSingleEntry(in RecoveryManifestEntry entry)
    {
        try
        {
            if (!entry.ExistedBefore)
            {
                if (File.Exists(entry.DestinationPath))
                {
                    File.Delete(entry.DestinationPath);
                }

                return Result.FromSuccess();
            }

            if (!File.Exists(entry.BackupPath))
            {
                return Result.FromFailure($"Backup file missing for '{entry.DestinationPath}'.");
            }

            byte[] originalBytes = File.ReadAllBytes(entry.BackupPath);
            var restoreRequest = new FileSaveRequest(entry.DestinationPath, originalBytes);
            AtomicSaveResult restoreResult = _atomicSaver.Save(in restoreRequest);
            return restoreResult.Success
                ? Result.FromSuccess()
                : Result.FromFailure(restoreResult.ErrorMessage ?? $"Failed restoring '{entry.DestinationPath}'.");
        }
        catch (Exception ex)
        {
            return Result.FromFailure($"Failed rolling back '{entry.DestinationPath}': {ex.Message}");
        }
    }

    private static Result ValidateRequests(IReadOnlyList<FileSaveRequest> requests)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < requests.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(requests[i].DestinationPath))
            {
                return Result.FromFailure($"Save request at index {i} has an empty destination path.");
            }

            if (requests[i].Content == null)
            {
                return Result.FromFailure($"Save request at index {i} has null content.");
            }

            string normalized = Path.GetFullPath(requests[i].DestinationPath);
            if (!seen.Add(normalized))
            {
                return Result.FromFailure($"Duplicate destination path in batch: '{normalized}'.");
            }
        }

        return Result.FromSuccess();
    }

    private static string SerializeManifest(string backupDirectory, IReadOnlyList<RecoveryManifestEntry> entries)
    {
        var builder = new StringBuilder();
        builder.Append("BackupDirectory = ");
        builder.Append(TomlValueFormatter.FormatBasicString(backupDirectory));
        builder.Append('\n');

        for (int i = 0; i < entries.Count; i++)
        {
            RecoveryManifestEntry entry = entries[i];
            builder.Append('\n');
            builder.Append("[[Entries]]\n");
            builder.Append("DestinationPath = ");
            builder.Append(TomlValueFormatter.FormatBasicString(entry.DestinationPath));
            builder.Append('\n');
            builder.Append("ExistedBefore = ");
            builder.Append(TomlValueFormatter.FormatBoolean(entry.ExistedBefore));
            builder.Append('\n');
            builder.Append("BackupPath = ");
            builder.Append(TomlValueFormatter.FormatBasicString(entry.BackupPath));
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static Result<RecoveryManifestEntry> ParseManifestEntry(TomlDocument document, in TomlTableSpan table)
    {
        Result<TomlFieldSpan> destField = document.GetFieldInTable(table.Index, "DestinationPath");
        Result<TomlFieldSpan> existedField = document.GetFieldInTable(table.Index, "ExistedBefore");
        Result<TomlFieldSpan> backupField = document.GetFieldInTable(table.Index, "BackupPath");

        if (!destField.Success || destField.Value.TypedValue is not string destPath ||
            !existedField.Success || existedField.Value.TypedValue is not bool existedBefore ||
            !backupField.Success || backupField.Value.TypedValue is not string backupPath)
        {
            return Result<RecoveryManifestEntry>.FromFailure("Malformed entry in recovery manifest.");
        }

        return Result<RecoveryManifestEntry>.FromSuccess(new RecoveryManifestEntry(destPath, existedBefore, backupPath));
    }

    private void CleanupTransactionArtifacts(string backupDirectory)
    {
        try
        {
            if (File.Exists(ManifestFilePath))
            {
                File.Delete(ManifestFilePath);
            }
        }
        catch (IOException ex)
        {
            Trace.TraceWarning($"Failed deleting recovery manifest '{ManifestFilePath}': {ex.Message}");
        }

        if (!string.IsNullOrWhiteSpace(backupDirectory))
        {
            CleanupDirectory(backupDirectory);
        }
    }

    private static void CleanupDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch (IOException ex)
        {
            Trace.TraceWarning($"Failed deleting backup directory '{directoryPath}': {ex.Message}");
        }
    }
}
