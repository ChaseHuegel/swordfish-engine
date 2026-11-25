using System.Globalization;
using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;

namespace WaywardBeyond.AssetTools.Storage;

/// <summary>
///     Persists full-text drafts keyed by destination path hash with unclean-exit recovery detection.
/// </summary>
public sealed class DraftStore
{
    private const string SESSION_MARKER_FILE = "active-session.toml";
    private const string DRAFT_CONTENT_EXTENSION = ".draft";
    private const string DRAFT_META_EXTENSION = ".meta.toml";

    private readonly string _draftsDirectory;
    private readonly AtomicFileSaver _atomicSaver;

    /// <summary>
    ///     Creates a draft store rooted at <paramref name="draftsDirectory"/>.
    /// </summary>
    public DraftStore(string draftsDirectory, AtomicFileSaver? atomicSaver = null, string? sessionId = null)
    {
        if (string.IsNullOrWhiteSpace(draftsDirectory))
        {
            throw new ArgumentException("Drafts directory is required.", nameof(draftsDirectory));
        }

        _draftsDirectory = Path.GetFullPath(draftsDirectory);
        _atomicSaver = atomicSaver ?? new AtomicFileSaver();
        SessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId;
    }

    /// <summary>
    ///     Gets the identifier for the current draft store session.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    ///     Gets a value indicating whether an unclosed session marker was found during startup.
    /// </summary>
    public bool HadUncleanPreviousExit { get; private set; }

    /// <summary>
    ///     Gets the full path of the active session marker file.
    /// </summary>
    public string SessionMarkerPath => Path.Combine(_draftsDirectory, SESSION_MARKER_FILE);

    /// <summary>
    ///     Computes the deterministic draft storage key for <paramref name="destinationPath"/>.
    /// </summary>
    public static string GetDraftKey(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("Destination path is required.", nameof(destinationPath));
        }

        string canonical = Path.GetFullPath(destinationPath);
        if (OperatingSystem.IsWindows())
        {
            canonical = canonical.ToLowerInvariant();
        }

        return ContentHash.ComputeSha256Hex(canonical);
    }

    /// <summary>
    ///     Records session startup and detects whether the prior session exited uncleanly.
    /// </summary>
    public Result StartSession()
    {
        HadUncleanPreviousExit = File.Exists(SessionMarkerPath);
        Directory.CreateDirectory(_draftsDirectory);

        var builder = new StringBuilder();
        builder.Append("SessionId = ");
        builder.Append(TomlValueFormatter.FormatBasicString(SessionId));
        builder.Append('\n');
        builder.Append("ProcessId = ");
        builder.Append(TomlValueFormatter.FormatInteger(Environment.ProcessId));
        builder.Append('\n');
        builder.Append("StartedUtc = ");
        builder.Append(TomlValueFormatter.FormatBasicString(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        builder.Append('\n');

        var request = new FileSaveRequest(SessionMarkerPath, Encoding.UTF8.GetBytes(builder.ToString()));
        return _atomicSaver.Save(in request).ToResult();
    }

    /// <summary>
    ///     Removes the active session marker to indicate a clean shutdown.
    /// </summary>
    public Result MarkCleanShutdown()
    {
        try
        {
            if (File.Exists(SessionMarkerPath))
            {
                File.Delete(SessionMarkerPath);
            }

            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    /// <summary>
    ///     Atomically writes a full-text draft copy and its base file SHA-256 metadata.
    /// </summary>
    public Result SaveDraft(string destinationPath, ReadOnlySpan<byte> content, string baseSha256)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return Result.FromFailure("Destination path is required.");
        }

        string fullDestination = Path.GetFullPath(destinationPath);
        string key = GetDraftKey(fullDestination);
        string contentPath = Path.Combine(_draftsDirectory, key + DRAFT_CONTENT_EXTENSION);
        string metaPath = Path.Combine(_draftsDirectory, key + DRAFT_META_EXTENSION);

        var contentRequest = new FileSaveRequest(contentPath, content.ToArray());
        AtomicSaveResult contentSave = _atomicSaver.Save(in contentRequest);
        if (!contentSave.Success)
        {
            return contentSave.ToResult();
        }

        var metadata = new DraftMetadata(fullDestination, baseSha256 ?? string.Empty, SessionId, DateTimeOffset.UtcNow);
        string metaToml = SerializeMetadata(in metadata);
        var metaRequest = new FileSaveRequest(metaPath, Encoding.UTF8.GetBytes(metaToml));
        return _atomicSaver.Save(in metaRequest).ToResult();
    }

    /// <summary>
    ///     Loads a stored draft for <paramref name="destinationPath"/> along with live disk hash state.
    /// </summary>
    public Result<DraftRecoveryEntry> LoadDraft(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return Result<DraftRecoveryEntry>.FromFailure("Destination path is required.");
        }

        string key = GetDraftKey(destinationPath);
        string metaPath = Path.Combine(_draftsDirectory, key + DRAFT_META_EXTENSION);
        if (!File.Exists(metaPath))
        {
            return Result<DraftRecoveryEntry>.FromFailure($"No draft found for '{destinationPath}'.");
        }

        return LoadDraftFromMetaPath(metaPath);
    }

    /// <summary>
    ///     Returns all recoverable drafts stored in this draft store ordered by destination path.
    /// </summary>
    public Result<IReadOnlyList<DraftRecoveryEntry>> GetRecoverableDrafts()
    {
        if (!Directory.Exists(_draftsDirectory))
        {
            return Result<IReadOnlyList<DraftRecoveryEntry>>.FromSuccess([]);
        }

        try
        {
            string[] metaFiles = Directory.GetFiles(_draftsDirectory, "*" + DRAFT_META_EXTENSION, SearchOption.TopDirectoryOnly);
            List<DraftRecoveryEntry> entries = new(metaFiles.Length);
            for (int i = 0; i < metaFiles.Length; i++)
            {
                Result<DraftRecoveryEntry> loaded = LoadDraftFromMetaPath(metaFiles[i]);
                if (!loaded.Success)
                {
                    return Result<IReadOnlyList<DraftRecoveryEntry>>.FromFailure(loaded.Message);
                }

                entries.Add(loaded.Value);
            }

            entries.Sort(static (left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.DestinationPath, right.DestinationPath));
            return Result<IReadOnlyList<DraftRecoveryEntry>>.FromSuccess(entries);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<DraftRecoveryEntry>>.FromFailure(ex);
        }
    }

    /// <summary>
    ///     Deletes the stored draft and metadata for <paramref name="destinationPath"/>.
    /// </summary>
    public Result DiscardDraft(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return Result.FromFailure("Destination path is required.");
        }

        try
        {
            string key = GetDraftKey(destinationPath);
            string contentPath = Path.Combine(_draftsDirectory, key + DRAFT_CONTENT_EXTENSION);
            string metaPath = Path.Combine(_draftsDirectory, key + DRAFT_META_EXTENSION);

            if (File.Exists(contentPath))
            {
                File.Delete(contentPath);
            }

            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    /// <summary>
    ///     Deletes every stored draft and metadata file from the drafts directory.
    /// </summary>
    public Result DiscardAllDrafts()
    {
        if (!Directory.Exists(_draftsDirectory))
        {
            return Result.FromSuccess();
        }

        try
        {
            string[] contentFiles = Directory.GetFiles(_draftsDirectory, "*" + DRAFT_CONTENT_EXTENSION, SearchOption.TopDirectoryOnly);
            for (int i = 0; i < contentFiles.Length; i++)
            {
                File.Delete(contentFiles[i]);
            }

            string[] metaFiles = Directory.GetFiles(_draftsDirectory, "*" + DRAFT_META_EXTENSION, SearchOption.TopDirectoryOnly);
            for (int i = 0; i < metaFiles.Length; i++)
            {
                File.Delete(metaFiles[i]);
            }

            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    private Result<DraftRecoveryEntry> LoadDraftFromMetaPath(string metaPath)
    {
        Result<TomlDocument> docResult = TomlDocument.Load(metaPath);
        if (!docResult.Success)
        {
            return Result<DraftRecoveryEntry>.FromFailure(docResult.Message);
        }

        TomlDocument document = docResult.Value;
        if (document.HasSyntaxErrors)
        {
            return Result<DraftRecoveryEntry>.FromFailure($"Draft metadata '{metaPath}' has invalid TOML syntax.");
        }

        Result<TomlFieldSpan> destField = document.GetRootField("DestinationPath");
        Result<TomlFieldSpan> baseField = document.GetRootField("BaseSha256");
        Result<TomlFieldSpan> sessionField = document.GetRootField("SessionId");
        Result<TomlFieldSpan> savedField = document.GetRootField("SavedUtc");

        if (!destField.Success || destField.Value.TypedValue is not string destinationPath ||
            !baseField.Success || baseField.Value.TypedValue is not string baseSha256 ||
            !sessionField.Success || sessionField.Value.TypedValue is not string sessionId ||
            !savedField.Success || savedField.Value.TypedValue is not string savedUtcText)
        {
            return Result<DraftRecoveryEntry>.FromFailure($"Draft metadata '{metaPath}' is missing required fields.");
        }

        if (!DateTimeOffset.TryParse(savedUtcText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset savedUtc))
        {
            return Result<DraftRecoveryEntry>.FromFailure($"Draft metadata '{metaPath}' has an invalid timestamp.");
        }

        string keyPrefix = metaPath.Substring(0, metaPath.Length - DRAFT_META_EXTENSION.Length);
        string contentPath = keyPrefix + DRAFT_CONTENT_EXTENSION;
        if (!File.Exists(contentPath))
        {
            return Result<DraftRecoveryEntry>.FromFailure($"Draft content file is missing for '{destinationPath}'.");
        }

        byte[] draftBytes;
        try
        {
            draftBytes = File.ReadAllBytes(contentPath);
        }
        catch (Exception ex)
        {
            return Result<DraftRecoveryEntry>.FromFailure(ex);
        }

        Result<string?> diskHashResult = ContentHash.ComputeFileSha256Hex(destinationPath);
        if (!diskHashResult.Success)
        {
            return Result<DraftRecoveryEntry>.FromFailure(diskHashResult.Message);
        }

        string? currentDiskSha256 = diskHashResult.Value;
        bool hasExternalModification = string.IsNullOrEmpty(baseSha256)
            ? currentDiskSha256 != null
            : !string.Equals(currentDiskSha256, baseSha256, StringComparison.OrdinalIgnoreCase);

        var entry = new DraftRecoveryEntry(
            destinationPath,
            draftBytes,
            baseSha256,
            currentDiskSha256,
            hasExternalModification,
            sessionId,
            savedUtc);
        return Result<DraftRecoveryEntry>.FromSuccess(entry);
    }

    private static string SerializeMetadata(in DraftMetadata metadata)
    {
        var builder = new StringBuilder();
        builder.Append("DestinationPath = ");
        builder.Append(TomlValueFormatter.FormatBasicString(metadata.DestinationPath));
        builder.Append('\n');
        builder.Append("BaseSha256 = ");
        builder.Append(TomlValueFormatter.FormatBasicString(metadata.BaseSha256));
        builder.Append('\n');
        builder.Append("SessionId = ");
        builder.Append(TomlValueFormatter.FormatBasicString(metadata.SessionId));
        builder.Append('\n');
        builder.Append("SavedUtc = ");
        builder.Append(TomlValueFormatter.FormatBasicString(metadata.SavedUtc.ToString("O", CultureInfo.InvariantCulture)));
        builder.Append('\n');
        return builder.ToString();
    }
}
