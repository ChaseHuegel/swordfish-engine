using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.Data;
using WaywardBeyond.Recipes;
using GameContentVersion = WaywardBeyond.Data.ContentVersion;
using ToolsBlueprints = WaywardBeyond.AssetTools.Blueprints;
using ToolsDiagnostics = WaywardBeyond.AssetTools.Diagnostics;
using ToolsDocuments = WaywardBeyond.AssetTools.Documents;
using ToolsEditing = WaywardBeyond.AssetTools.Editing;
using ToolsLocalization = WaywardBeyond.AssetTools.Localization;
using ToolsPacks = WaywardBeyond.AssetTools.Packs;
using ToolsSearch = WaywardBeyond.AssetTools.Search;
using ToolsSettings = WaywardBeyond.AssetTools.Settings;
using ToolsStorage = WaywardBeyond.AssetTools.Storage;
using ToolsValidation = WaywardBeyond.AssetTools.Validation;
using ToolsWorkspace = WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Real disk-backed studio workspace service powered by <c>WaywardBeyond.AssetTools</c>.
/// </summary>
public sealed class AssetToolsStudioWorkspaceService : IStudioWorkspaceService
{
    private const string TOOL_VERSION = "0.1.0-alpha";
    private const string AVALONIA_VERSION = "11.3.23";
    private const long RECENT_STUDIO_SAVE_WINDOW_MS = 1500;
    private const long RECENT_STUDIO_SAVE_PRUNE_MS = 10000;

    private readonly ToolsSettings.ToolDataPaths _dataPaths;
    private readonly ToolsSettings.ToolSettingsStore _settingsStore;
    private readonly ToolsStorage.AtomicFileSaver _atomicSaver;
    private readonly ToolsStorage.MultiFileSaver _multiSaver;
    private readonly ToolsStorage.DraftStore _draftStore;
    private readonly ToolsWorkspace.WorkspaceLoader _workspaceLoader;
    private readonly ToolsValidation.WorkspaceValidator _validator;
    private readonly ToolsEditing.RenamePlanner _renamePlanner;
    private readonly ToolsEditing.AssetScaffolder _assetScaffolder;
    private readonly ToolsEditing.AssetDeleter _assetDeleter;
    private readonly ToolsEditing.AssetCloner _assetCloner;
    private readonly ToolsPacks.PackScaffolder _packScaffolder;
    private readonly ToolsPacks.PackExporter _packExporter;
    private readonly ToolsWorkspace.InstallLocator _installLocator;
    private readonly ToolsDiagnostics.DiagnosticsBundleWriter _diagnosticsWriter;
    private readonly ToolsSearch.CommandPaletteSearchIndex _searchIndex;
    private readonly StudioLogSession? _logSession;
    private readonly Lock _recentSavesLock = new();
    private readonly Dictionary<string, long> _recentStudioSaves = new(StringComparer.OrdinalIgnoreCase);

    private ToolsSettings.ToolSettings _settings;
    private ToolsWorkspace.WorkspaceIndex? _currentWorkspace;
    private ToolsValidation.ValidationReport? _lastValidationReport;
    private ToolsEditing.WorkspaceDraftBatch? _lastRenameBatch;
    private string? _currentWorkspaceDisplayName;

    /// <summary>
    ///     Initializes the real AssetTools workspace service using resolved data paths and an optional log session.
    /// </summary>
    public AssetToolsStudioWorkspaceService(
        ToolsSettings.ToolDataPaths? dataPaths = null,
        StudioLogSession? logSession = null)
    {
        _dataPaths = dataPaths ?? ToolsSettings.ToolDataPaths.Resolve();
        _logSession = logSession;
        _atomicSaver = new ToolsStorage.AtomicFileSaver();
        _settingsStore = new ToolsSettings.ToolSettingsStore(_dataPaths, _atomicSaver);
        _multiSaver = new ToolsStorage.MultiFileSaver(_dataPaths.RecoveryDirectory, _atomicSaver);
        _draftStore = new ToolsStorage.DraftStore(_dataPaths.DraftsDirectory, _atomicSaver);
        _workspaceLoader = new ToolsWorkspace.WorkspaceLoader();
        _validator = new ToolsValidation.WorkspaceValidator();
        _renamePlanner = new ToolsEditing.RenamePlanner();
        _assetScaffolder = new ToolsEditing.AssetScaffolder();
        _assetDeleter = new ToolsEditing.AssetDeleter(_atomicSaver);
        _assetCloner = new ToolsEditing.AssetCloner(_atomicSaver);
        _packScaffolder = new ToolsPacks.PackScaffolder(_atomicSaver);
        _packExporter = new ToolsPacks.PackExporter(_validator, _atomicSaver);
        _installLocator = new ToolsWorkspace.InstallLocator();
        _diagnosticsWriter = new ToolsDiagnostics.DiagnosticsBundleWriter();
        _searchIndex = new ToolsSearch.CommandPaletteSearchIndex();

        var loadedSettings = _settingsStore.Load();
        _settings = loadedSettings.Success ? loadedSettings.Value : new ToolsSettings.ToolSettings();
        ToolsLocalization.ToolLocalization.ApplyCulture(_settings.CultureOverride);

        if (_multiSaver.HasPendingRecoveryManifest())
        {
            var recovered = _multiSaver.RecoverPendingTransaction();
            if (recovered.Success)
            {
                _logSession?.LogWarning("Rolled back incomplete multi-file save manifest from prior session.");
            }
            else
            {
                _logSession?.LogError($"Failed recovering pending multi-file save manifest: {recovered.Message}");
            }
        }

        var sessionStart = _draftStore.StartSession();
        if (!sessionStart.Success)
        {
            _logSession?.LogWarning($"Draft store session start warning: {sessionStart.Message}");
        }
    }

    /// <inheritdoc/>
    public bool UsesRealBackend => true;

    /// <summary>
    ///     Gets the currently loaded <see cref="ToolsWorkspace.WorkspaceIndex"/>, or null before opening a workspace.
    /// </summary>
    public ToolsWorkspace.WorkspaceIndex? CurrentWorkspace => _currentWorkspace;

    /// <summary>
    ///     Gets the most recent <see cref="ToolsValidation.ValidationReport"/>, or null before validation runs.
    /// </summary>
    public ToolsValidation.ValidationReport? LastValidationReport => _lastValidationReport;

    /// <summary>
    ///     Gets the underlying <see cref="ToolsStorage.DraftStore"/> used for crash-recovery drafts.
    /// </summary>
    public ToolsStorage.DraftStore DraftStore => _draftStore;

    /// <summary>
    ///     Gets the most recent undoable rename draft batch applied during this session, if any.
    /// </summary>
    public ToolsEditing.WorkspaceDraftBatch? LastRenameBatch => _lastRenameBatch;

    /// <inheritdoc/>
    public IReadOnlyList<RecentWorkspaceEntry> GetRecentWorkspaces()
    {
        var list = new List<RecentWorkspaceEntry>(_settings.RecentWorkspaces.Count);
        for (var i = 0; i < _settings.RecentWorkspaces.Count; i++)
        {
            var path = _settings.RecentWorkspaces[i];
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                continue;
            }

            var isRepo = File.Exists(Path.Combine(path, "Swordfish.sln"))
                || File.Exists(Path.Combine(path, "config", "modules.toml"));
            var hasPackToml = File.Exists(Path.Combine(path, ToolsPacks.PackMetadata.FILE_NAME));
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var lastWrite = Directory.GetLastWriteTime(path).ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
            list.Add(new RecentWorkspaceEntry(
                Name: string.IsNullOrWhiteSpace(name) ? path : name,
                Mode: isRepo ? WorkspaceMode.Repo : WorkspaceMode.Pack,
                RootPath: path,
                LastOpenedLabel: lastWrite,
                AssetCount: CountQuickAssets(path),
                HasPackMetadata: hasPackToml));
        }

        return list;
    }

    /// <inheritdoc/>
    public StudioOperationResult RemoveRecentWorkspace(string rootPath, bool deleteFromDisk)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || rootPath.IndexOf('\0') >= 0)
        {
            return new StudioOperationResult(false, "Cannot remove a recent workspace with an empty or invalid path.");
        }

        var trimmedPath = rootPath.Trim();
        var displayName = Path.GetFileName(trimmedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = trimmedPath;
        }

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        string normalizedFullPath;
        try
        {
            normalizedFullPath = TrimTrailingDirectorySeparators(Path.GetFullPath(trimmedPath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            if (deleteFromDisk)
            {
                return new StudioOperationResult(false, $"Cannot delete invalid workspace path '{trimmedPath}': {ex.Message}");
            }

            normalizedFullPath = trimmedPath;
        }

        if (deleteFromDisk)
        {
            if (IsUnsafeDiskDeleteTarget(normalizedFullPath))
            {
                return new StudioOperationResult(
                    false,
                    $"Refusing to recursively delete protected directory '{normalizedFullPath}'.");
            }

            if (_currentWorkspace != null)
            {
                var activeNormalized = TrimTrailingDirectorySeparators(Path.GetFullPath(_currentWorkspace.RootPath));
                if (string.Equals(activeNormalized, normalizedFullPath, pathComparison))
                {
                    _currentWorkspace = null;
                    _lastValidationReport = null;
                    _currentWorkspaceDisplayName = null;
                }
            }

            if (Directory.Exists(normalizedFullPath))
            {
                try
                {
                    DeleteDirectoryRecursivelySafe(normalizedFullPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logSession?.LogError($"Failed deleting workspace folder '{normalizedFullPath}': {ex.Message}");
                    return new StudioOperationResult(
                        false,
                        $"Failed deleting workspace folder '{displayName}' on disk: {ex.Message}");
                }
            }

            _settings.SetDefaultNamespace(normalizedFullPath, null);
        }

        _settings.RemoveRecentWorkspace(trimmedPath);
        var saved = _settingsStore.Save(_settings);
        if (!saved.Success)
        {
            _logSession?.LogError($"Failed saving settings after removing recent workspace '{trimmedPath}': {saved.Message}");
            return new StudioOperationResult(false, saved.Message ?? "Failed updating recent workspaces settings.");
        }

        if (deleteFromDisk)
        {
            _logSession?.LogInfo($"Deleted workspace '{normalizedFullPath}' from disk and removed it from recents.");
            return new StudioOperationResult(true, $"Deleted '{displayName}' from disk and removed it from recents.");
        }

        _logSession?.LogInfo($"Removed '{trimmedPath}' from recent workspaces.");
        return new StudioOperationResult(true, $"Removed '{displayName}' from recent workspaces.");
    }

    /// <summary>
    ///     Returns whether <paramref name="fullPath"/> targets a protected root, home, or system directory that must never be deleted recursively.
    /// </summary>
    public static bool IsUnsafeDiskDeleteTarget(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || fullPath.IndexOf('\0') >= 0)
        {
            return true;
        }

        string normalized;
        try
        {
            normalized = TrimTrailingDirectorySeparators(Path.GetFullPath(fullPath.Trim()));
        }
        catch
        {
            return true;
        }

        var pathRoot = TrimTrailingDirectorySeparators(Path.GetPathRoot(normalized));
        if (string.IsNullOrEmpty(pathRoot) || string.Equals(normalized, pathRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return true;
        }

        ReadOnlySpan<string> protectedTargets =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Path.GetTempPath(),
            Environment.SystemDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        ];

        for (var i = 0; i < protectedTargets.Length; i++)
        {
            var candidate = protectedTargets[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var protectedPath = TrimTrailingDirectorySeparators(Path.GetFullPath(candidate));
            if (IsSameOrAncestorPath(normalized, protectedPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameOrAncestorPath(string candidatePath, string targetPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(candidatePath, targetPath, comparison))
        {
            return true;
        }

        var prefix = candidatePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return targetPath.StartsWith(prefix, comparison);
    }

    private static string TrimTrailingDirectorySeparators(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var root = Path.GetPathRoot(path);
        if (!string.IsNullOrEmpty(root) && string.Equals(path, root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return path;
        }

        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void DeleteDirectoryRecursivelySafe(string path)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists)
        {
            return;
        }

        if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            dir.Delete(recursive: false);
            return;
        }

        foreach (var entry in dir.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
        {
            if (entry is DirectoryInfo subDir)
            {
                if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    subDir.Delete(recursive: false);
                }
                else
                {
                    DeleteDirectoryRecursivelySafe(subDir.FullName);
                }
            }
            else
            {
                if ((entry.Attributes & FileAttributes.ReadOnly) != 0)
                {
                    entry.Attributes &= ~FileAttributes.ReadOnly;
                }

                entry.Delete();
            }
        }

        if ((dir.Attributes & FileAttributes.ReadOnly) != 0)
        {
            dir.Attributes &= ~FileAttributes.ReadOnly;
        }

        dir.Delete(recursive: false);
    }

    /// <inheritdoc/>
    public StudioWorkspaceSnapshot OpenWorkspace(WorkspaceMode mode, string rootPath, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            _logSession?.LogWarning($"Workspace directory does not exist: '{rootPath}'.");
            return BuildEmptySnapshot(mode, rootPath, displayName);
        }

        Result<ToolsWorkspace.WorkspaceIndex> loaded;
        if (mode == WorkspaceMode.Repo)
        {
            loaded = _workspaceLoader.LoadRepo(rootPath);
        }
        else
        {
            var referenceInstall = AssetSeedProjector.HasPackStructure(rootPath)
                ? ResolveSingleReferenceInstall(rootPath)
                : null;
            loaded = _workspaceLoader.LoadPack(rootPath, referenceInstall);
        }

        if (!loaded.Success)
        {
            loaded = _workspaceLoader.LoadAuto(rootPath);
        }

        if (!loaded.Success)
        {
            _logSession?.LogError($"Failed loading workspace '{rootPath}': {loaded.Message}");
            return BuildEmptySnapshot(mode, rootPath, displayName);
        }

        return FinalizeLoadedWorkspace(loaded.Value, displayName);
    }

    /// <inheritdoc/>
    public StudioWorkspaceSnapshot? TryOpenAuto(string rootPath, string? referenceInstallPath = null)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return null;
        }

        var loaded = _workspaceLoader.LoadAuto(rootPath, referenceInstallPath);
        if (!loaded.Success)
        {
            _logSession?.LogError($"Auto-load failed for '{rootPath}': {loaded.Message}");
            return null;
        }

        return FinalizeLoadedWorkspace(loaded.Value, explicitDisplayName: null);
    }

    /// <inheritdoc/>
    public StudioOperationResult CreatePackWorkspace(
        string directory,
        string packId,
        string packNamespace,
        string displayName,
        string author)
    {
        var created = _packScaffolder.CreatePack(directory, packId, packNamespace, displayName, author, TOOL_VERSION);
        if (!created.Success)
        {
            _logSession?.LogError($"CreatePackWorkspace failed: {created.Message}");
            return new StudioOperationResult(false, created.Message ?? "Failed creating pack workspace.");
        }

        _logSession?.LogInfo($"Scaffolded content pack '{packId}' at '{directory}'.");
        return new StudioOperationResult(true, $"Scaffolded content pack '{packId}' at '{directory}'.", directory);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> LocateGameInstalls()
    {
        var result = _installLocator.Locate();
        var paths = new List<string>(result.Candidates.Count);
        for (var i = 0; i < result.Candidates.Count; i++)
        {
            paths.Add(result.Candidates[i].InstallPath);
        }

        return paths;
    }

    /// <inheritdoc/>
    public StudioSettingsState GetSettings()
    {
        var defaultNamespace = _currentWorkspace != null
            ? (_settings.GetDefaultNamespace(_currentWorkspace.RootPath) ?? string.Empty)
            : string.Empty;

        return new StudioSettingsState(
            IsDarkTheme: _settings.Theme == ToolsSettings.ToolTheme.Dark,
            CultureOverride: FormatCultureSetting(_settings.CultureOverride),
            GlPreference: FormatGlPreference(_settings.GlPreference),
            LogLevel: _settings.LogLevel.ToString(),
            IsPortableMode: _dataPaths.IsPortable,
            SettingsPath: _dataPaths.SettingsFilePath,
            Theme: _settings.Theme,
            UiScale: _settings.UiScale,
            DefaultNamespace: defaultNamespace);
    }

    /// <inheritdoc/>
    public StudioOperationResult SaveSettings(StudioSettingsState settings)
    {
        _settings.Theme = settings.Theme == ToolsSettings.ToolTheme.System && settings.IsDarkTheme
            ? ToolsSettings.ToolTheme.Dark
            : settings.Theme;
        _settings.UiScale = ToolsSettings.ToolSettings.ClampUiScale(settings.UiScale);
        _settings.CultureOverride = ParseCultureSetting(settings.CultureOverride);
        _settings.GlPreference = ParseGlPreference(settings.GlPreference);
        if (Enum.TryParse(settings.LogLevel, ignoreCase: true, out ToolsSettings.ToolLogLevel parsedLog))
        {
            _settings.LogLevel = parsedLog;
        }

        if (_currentWorkspace != null)
        {
            _settings.SetDefaultNamespace(_currentWorkspace.RootPath, settings.DefaultNamespace);
        }

        ToolsLocalization.ToolLocalization.ApplyCulture(_settings.CultureOverride);
        var saved = _settingsStore.Save(_settings);
        if (!saved.Success)
        {
            _logSession?.LogError($"Failed saving settings: {saved.Message}");
            return new StudioOperationResult(false, saved.Message ?? "Failed saving settings.");
        }

        return new StudioOperationResult(true, "Settings saved.", _dataPaths.SettingsFilePath);
    }

    /// <inheritdoc/>
    public WorkspaceSaveResult SaveDocuments(IReadOnlyList<DocumentSavePayload> dirtyDocuments)
    {
        if (dirtyDocuments.Count == 0)
        {
            return new WorkspaceSaveResult(true, 0, "All documents are already up to date.");
        }

        var updatedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var updatedTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var blueprintSavedCount = 0;

        var groupedByFile = new Dictionary<string, List<DocumentSavePayload>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < dirtyDocuments.Count; i++)
        {
            var payload = dirtyDocuments[i];
            if (payload.Kind == AssetKind.Blueprint)
            {
                var bpSaved = SaveBlueprintMetadata(payload);
                if (!bpSaved.Success)
                {
                    return new WorkspaceSaveResult(false, 0, bpSaved.Message);
                }

                blueprintSavedCount++;
                continue;
            }

            var targetPath = ResolveAbsoluteFilePath(payload);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return new WorkspaceSaveResult(false, 0, $"Cannot resolve destination path for '{payload.RelativeFile}'.");
            }

            if (!groupedByFile.TryGetValue(targetPath, out var list))
            {
                list = [];
                groupedByFile[targetPath] = list;
            }

            list.Add(payload);
        }

        var requests = new List<ToolsStorage.FileSaveRequest>(groupedByFile.Count);
        foreach (var kvp in groupedByFile)
        {
            var fullPath = kvp.Key;
            var payloads = kvp.Value;
            var expectNew = !File.Exists(fullPath);

            ToolsDocuments.TomlDocument doc;
            string? expectedBaseHash = null;
            if (!expectNew)
            {
                var loaded = ToolsDocuments.TomlDocument.Load(fullPath);
                if (!loaded.Success)
                {
                    return new WorkspaceSaveResult(false, 0, loaded.Message ?? $"Failed reading '{fullPath}'.");
                }

                doc = loaded.Value;
                expectedBaseHash = payloads[0].BaseContentSha256 ?? ToolsStorage.ContentHash.ComputeSha256Hex(doc.ToBytes());
            }
            else
            {
                doc = ToolsDocuments.TomlDocument.Parse(payloads[0].RawTomlText, fullPath);
            }

            for (var i = 0; i < payloads.Count; i++)
            {
                var applied = AssetTomlEditApplier.ApplyPayload(doc, payloads[i]);
                if (!applied.Success)
                {
                    return new WorkspaceSaveResult(false, 0, applied.Message ?? $"Failed applying edits to '{fullPath}'.");
                }
            }

            var bytes = doc.ToBytes();
            var nextHash = ToolsStorage.ContentHash.ComputeSha256Hex(bytes);
            updatedHashes[fullPath] = nextHash;
            updatedTexts[fullPath] = doc.Text;
            requests.Add(new ToolsStorage.FileSaveRequest(
                fullPath,
                bytes,
                ExpectedBaseSha256: expectNew ? null : expectedBaseHash,
                ExpectFileNotToExist: expectNew));
        }

        if (requests.Count > 0)
        {
            for (var i = 0; i < requests.Count; i++)
            {
                RecordStudioFileWrite(requests[i].DestinationPath);
            }

            var multiResult = _multiSaver.SaveAll(requests);
            if (!multiResult.Success)
            {
                var err = multiResult.Conflicts.Count > 0
                    ? $"External file modification detected on {multiResult.Conflicts.Count} file(s); save aborted without overwriting."
                    : multiResult.ErrorMessage ?? "Atomic multi-file save failed.";
                _logSession?.LogError(err);
                return new WorkspaceSaveResult(false, 0, err);
            }

            for (var i = 0; i < requests.Count; i++)
            {
                RecordStudioFileWrite(requests[i].DestinationPath);
                _draftStore.DiscardDraft(requests[i].DestinationPath);
            }
        }

        ReloadCurrentWorkspaceFromDisk();
        var totalSaved = requests.Count + blueprintSavedCount;
        _logSession?.LogInfo($"Atomically saved {totalSaved} workspace file(s).");
        return new WorkspaceSaveResult(
            true,
            totalSaved,
            $"Saved {totalSaved} file(s) atomically (fsync + rename verified).",
            updatedHashes,
            updatedTexts);
    }

    /// <inheritdoc/>
    public StudioOperationResult SaveDraft(DocumentSavePayload payload)
    {
        if (payload.Kind == AssetKind.Blueprint)
        {
            return new StudioOperationResult(true, "Blueprint metadata staged in memory.");
        }

        var fullPath = ResolveAbsoluteFilePath(payload);
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return new StudioOperationResult(false, $"Cannot resolve draft target for '{payload.RelativeFile}'.");
        }

        var previewText = PreviewEditedToml(payload) ?? payload.RawTomlText;
        var bytes = Encoding.UTF8.GetBytes(previewText);
        var baseHash = payload.BaseContentSha256 ?? string.Empty;
        var saved = _draftStore.SaveDraft(fullPath, bytes, baseHash);
        return saved.Success
            ? new StudioOperationResult(true, $"Draft synced for {payload.RelativeFile}.")
            : new StudioOperationResult(false, saved.Message ?? "Failed saving draft.");
    }

    /// <inheritdoc/>
    public string? PreviewEditedToml(DocumentSavePayload payload)
    {
        if (payload.Kind == AssetKind.Blueprint || payload.IsDirectRawEdit || payload.HasSyntaxError)
        {
            return null;
        }

        var fullPath = ResolveAbsoluteFilePath(payload);
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return null;
        }

        ToolsDocuments.TomlDocument? sourceDoc = null;
        if (_currentWorkspace != null && _currentWorkspace.DocumentsByPath.TryGetValue(fullPath, out var indexed))
        {
            sourceDoc = ToolsDocuments.TomlDocument.Parse(indexed.ToBytes(), fullPath);
        }
        else if (File.Exists(fullPath))
        {
            var loaded = ToolsDocuments.TomlDocument.Load(fullPath);
            if (loaded.Success)
            {
                sourceDoc = loaded.Value;
            }
        }

        if (sourceDoc == null || sourceDoc.HasSyntaxErrors)
        {
            return null;
        }

        var applied = AssetTomlEditApplier.ApplyPayload(sourceDoc, payload);
        return applied.Success ? sourceDoc.Text : null;
    }

    /// <inheritdoc/>
    public StudioOperationResult RecoverDraft(DraftRecoveryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.AbsoluteFilePath))
        {
            return new StudioOperationResult(false, "Draft recovery entry is missing its destination file path.");
        }

        var loaded = _draftStore.LoadDraft(entry.AbsoluteFilePath);
        if (!loaded.Success)
        {
            return new StudioOperationResult(false, loaded.Message ?? "Draft could not be loaded.");
        }

        RecordStudioFileWrite(entry.AbsoluteFilePath);
        var saveReq = new ToolsStorage.FileSaveRequest(entry.AbsoluteFilePath, loaded.Value.DraftBytes);
        var saved = _atomicSaver.Save(in saveReq);
        if (!saved.Success)
        {
            return new StudioOperationResult(false, saved.ErrorMessage ?? "Failed applying recovered draft.");
        }

        RecordStudioFileWrite(entry.AbsoluteFilePath);
        _draftStore.DiscardDraft(entry.AbsoluteFilePath);
        ReloadCurrentWorkspaceFromDisk();
        return new StudioOperationResult(true, $"Recovered draft for {entry.RelativeFile}.");
    }

    /// <inheritdoc/>
    public StudioOperationResult DiscardDraft(DraftRecoveryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.AbsoluteFilePath))
        {
            return new StudioOperationResult(true, $"Discarded draft for {entry.RelativeFile}.");
        }

        var discarded = _draftStore.DiscardDraft(entry.AbsoluteFilePath);
        return discarded.Success
            ? new StudioOperationResult(true, $"Discarded draft for {entry.RelativeFile}.")
            : new StudioOperationResult(false, discarded.Message ?? "Failed discarding draft.");
    }

    /// <inheritdoc/>
    public StudioOperationResult DiscardDraft(string? absoluteFilePath)
    {
        if (string.IsNullOrWhiteSpace(absoluteFilePath))
        {
            return new StudioOperationResult(true, "No backing draft file to discard.");
        }

        var discarded = _draftStore.DiscardDraft(absoluteFilePath);
        return discarded.Success
            ? new StudioOperationResult(true, $"Discarded draft for {Path.GetFileName(absoluteFilePath)}.")
            : new StudioOperationResult(false, discarded.Message ?? "Failed discarding draft.");
    }

    /// <inheritdoc/>
    public StudioOperationResult DiscardAllDrafts()
    {
        var discarded = _draftStore.DiscardAllDrafts();
        return discarded.Success
            ? new StudioOperationResult(true, "Discarded all unsaved drafts.")
            : new StudioOperationResult(false, discarded.Message ?? "Failed discarding unsaved drafts.");
    }

    /// <inheritdoc/>
    public StudioOperationResult DeleteAsset(
        AssetKind kind,
        string assetId,
        string? absoluteFilePath,
        int tableIndex,
        int entryIndex)
    {
        if (_currentWorkspace == null)
        {
            return new StudioOperationResult(false, "No workspace is open.");
        }

        var toolsKind = AssetSeedProjector.ToToolsAssetKind(kind);
        var targetPath = ResolveDeleteTargetPath(toolsKind, assetId, absoluteFilePath);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return new StudioOperationResult(false, $"Cannot resolve backing file path for '{assetId}'.");
        }

        RecordStudioFileWrite(targetPath);
        var deleted = _assetDeleter.Delete(
            _currentWorkspace,
            toolsKind,
            targetPath,
            assetId,
            tableIndex >= 0 ? tableIndex : null,
            entryIndex >= 0 ? entryIndex : null);

        if (!deleted.Success)
        {
            _logSession?.LogWarning($"DeleteAsset failed for '{assetId}': {deleted.Message}");
            return new StudioOperationResult(false, deleted.Message ?? $"Failed deleting '{assetId}'.");
        }

        RecordStudioFileWrite(targetPath);
        _draftStore.DiscardDraft(targetPath);
        ReloadCurrentWorkspaceFromDisk();

        var fileName = Path.GetFileName(targetPath);
        var message = deleted.Value.DeletedWholeFile
            ? $"Deleted {kind} '{assetId}' and removed {fileName}."
            : $"Removed {kind} '{assetId}' from {fileName}.";
        _logSession?.LogInfo(message);
        return new StudioOperationResult(true, message, targetPath);
    }

    /// <inheritdoc/>
    public StudioWorkspaceSnapshot? ReloadCurrentWorkspaceFromDisk()
    {
        if (_currentWorkspace == null)
        {
            return null;
        }

        var reloaded = ToolsValidation.ValidationBulkActions.Revalidate(_workspaceLoader, _validator, _currentWorkspace);
        if (!reloaded.Success)
        {
            _logSession?.LogWarning($"Failed reloading workspace '{_currentWorkspace.RootPath}': {reloaded.Message}");
            return null;
        }

        _currentWorkspace = reloaded.Value.Workspace;
        _lastValidationReport = reloaded.Value.Report;
        RebuildSearchIndex(_currentWorkspace, _lastValidationReport);

        return AssetSeedProjector.BuildSnapshot(
            _currentWorkspace,
            _lastValidationReport,
            [],
            _currentWorkspaceDisplayName,
            _settings.GetDefaultNamespace(_currentWorkspace.RootPath));
    }

    /// <inheritdoc/>
    public string GetWorkspaceDefaultNamespace(string? rootPath)
    {
        var effectiveRoot = !string.IsNullOrWhiteSpace(rootPath) ? rootPath : _currentWorkspace?.RootPath;
        if (!string.IsNullOrWhiteSpace(effectiveRoot))
        {
            var persisted = _settings.GetDefaultNamespace(effectiveRoot);
            if (!string.IsNullOrWhiteSpace(persisted))
            {
                return persisted;
            }
        }

        if (_currentWorkspace != null)
        {
            if (_currentWorkspace.Mode == ToolsWorkspace.WorkspaceMode.Repo)
            {
                return "wb";
            }

            var primary = _currentWorkspace.GetPrimaryWritableRoot();
            if (primary.Success)
            {
                var meta = ToolsPacks.PackMetadata.Load(primary.Value.RootDirectory);
                if (meta.Success && !string.IsNullOrWhiteSpace(meta.Value.Namespace))
                {
                    return meta.Value.Namespace;
                }

                return primary.Value.Id;
            }
        }

        return string.Empty;
    }

    /// <inheritdoc/>
    public void SetWorkspaceDefaultNamespace(string? rootPath, string defaultNamespace)
    {
        var effectiveRoot = !string.IsNullOrWhiteSpace(rootPath) ? rootPath : _currentWorkspace?.RootPath;
        if (string.IsNullOrWhiteSpace(effectiveRoot))
        {
            return;
        }

        _settings.SetDefaultNamespace(effectiveRoot, defaultNamespace);
        var saved = _settingsStore.Save(_settings);
        if (!saved.Success)
        {
            _logSession?.LogWarning($"Failed saving default namespace for '{effectiveRoot}': {saved.Message}");
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetWatchedDirectories()
    {
        if (_currentWorkspace == null)
        {
            return [];
        }

        var rawDirs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < _currentWorkspace.Roots.Count; i++)
        {
            var root = _currentWorkspace.Roots[i];
            if (!root.IsEnabled)
            {
                continue;
            }

            if (_currentWorkspace.Mode == ToolsWorkspace.WorkspaceMode.Repo)
            {
                AddExistingWatchDirectory(root.AssetsDirectory, rawDirs, seen);
            }
            else
            {
                AddExistingWatchDirectory(root.RootDirectory, rawDirs, seen);
                AddExistingWatchDirectory(root.AssetsDirectory, rawDirs, seen);
            }
        }

        return RemoveNestedWatchDirectories(rawDirs);
    }

    /// <inheritdoc/>
    public bool WasRecentlySavedByStudio(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed normalizing path '{filePath}' for self-save check: {ex.Message}");
            return false;
        }

        var now = Environment.TickCount64;
        using (_recentSavesLock.EnterScope())
        {
            if (!_recentStudioSaves.TryGetValue(normalized, out var savedTick))
            {
                return false;
            }

            if (now - savedTick <= RECENT_STUDIO_SAVE_WINDOW_MS)
            {
                return true;
            }

            _recentStudioSaves.Remove(normalized);
            return false;
        }
    }

    /// <inheritdoc/>
    public StudioValidationSyncResult RevalidateWorkspace()
    {
        if (_currentWorkspace == null)
        {
            return new StudioValidationSyncResult(false, [], string.Empty);
        }

        var revalidated = ToolsValidation.ValidationBulkActions.Revalidate(_workspaceLoader, _validator, _currentWorkspace);
        if (!revalidated.Success)
        {
            var report = _validator.Validate(_currentWorkspace);
            _lastValidationReport = report;
            return new StudioValidationSyncResult(
                true,
                AssetSeedProjector.ProjectValidationFindings(_currentWorkspace, report),
                ToolsValidation.ValidationBulkActions.CopyAll(report));
        }

        _currentWorkspace = revalidated.Value.Workspace;
        _lastValidationReport = revalidated.Value.Report;
        RebuildSearchIndex(_currentWorkspace, _lastValidationReport);

        return new StudioValidationSyncResult(
            true,
            AssetSeedProjector.ProjectValidationFindings(_currentWorkspace, _lastValidationReport),
            ToolsValidation.ValidationBulkActions.CopyAll(_lastValidationReport));
    }

    /// <inheritdoc/>
    public IReadOnlyList<InboundReferenceItem> GetInboundReferences(AssetKind kind, string assetId)
    {
        if (_currentWorkspace == null || string.IsNullOrWhiteSpace(assetId))
        {
            return [];
        }

        return kind == AssetKind.Tag
            ? AssetSeedProjector.ProjectTagInboundReferences(_currentWorkspace, assetId)
            : AssetSeedProjector.ProjectInboundReferences(_currentWorkspace, assetId);
    }

    /// <inheritdoc/>
    public StudioOperationResult FixCaseIssues()
    {
        if (_currentWorkspace == null)
        {
            return new StudioOperationResult(false, "No workspace is open.");
        }

        var report = _lastValidationReport ?? _validator.Validate(_currentWorkspace);
        for (var i = 0; i < report.Findings.Count; i++)
        {
            var finding = report.Findings[i];
            if (string.Equals(finding.Code, "PATH001", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(finding.FilePath))
            {
                RecordStudioFileWrite(finding.FilePath);
            }
        }

        var result = ToolsValidation.ValidationBulkActions.FixCase(_currentWorkspace, report, _multiSaver);
        if (!result.Success)
        {
            return new StudioOperationResult(false, result.Message ?? "Fix case operation failed.");
        }

        if (result.Value > 0)
        {
            ReloadCurrentWorkspaceFromDisk();
        }

        return new StudioOperationResult(
            true,
            result.Value > 0
                ? $"Fixed {result.Value} cross-platform path casing mismatch(es)."
                : "No case-only path mismatches remain.");
    }

    /// <inheritdoc/>
    public StudioOperationResult FormatDocument(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new StudioOperationResult(false, $"Cannot format missing file '{filePath}'.");
        }

        RecordStudioFileWrite(filePath);
        var formatted = ToolsValidation.ValidationBulkActions.FormatFiles([filePath], _atomicSaver);
        if (!formatted.Success)
        {
            return new StudioOperationResult(false, formatted.Message ?? "TOML formatting failed.");
        }

        RecordStudioFileWrite(filePath);
        ReloadCurrentWorkspaceFromDisk();
        return new StudioOperationResult(
            true,
            formatted.Value > 0
                ? $"Formatted {Path.GetFileName(filePath)} with house alignment."
                : $"{Path.GetFileName(filePath)} already matches house formatting.");
    }

    /// <inheritdoc/>
    public Result<bool> ApplyValidationFix(ValidationFinding finding)
    {
        if (_currentWorkspace == null)
        {
            return Result<bool>.FromFailure("No workspace is open.");
        }

        var report = _lastValidationReport ?? _validator.Validate(_currentWorkspace);
        var resolved = ResolveBackendValidationFinding(report, finding);
        if (!resolved.HasValue)
        {
            return Result<bool>.FromFailure($"Validation finding '{finding.Code}' at {finding.RelativeFile}:{finding.LineNumber} is no longer active.");
        }

        var backendFinding = resolved.Value;
        RecordStudioFileWrite(backendFinding.FilePath);
        if (backendFinding.FixKind == ToolsValidation.ValidationFixKind.CreateLocalizationKey)
        {
            RecordWritableEnglishLocalizationFiles();
        }

        var applied = ToolsValidation.ValidationBulkActions.ApplySingleFix(_currentWorkspace, backendFinding, _atomicSaver);
        if (applied.Success && applied.Value)
        {
            RecordStudioFileWrite(backendFinding.FilePath);
            if (backendFinding.FixKind == ToolsValidation.ValidationFixKind.CreateLocalizationKey)
            {
                RecordWritableEnglishLocalizationFiles();
            }

            ReloadWorkspaceIndexOnly();
        }

        return applied;
    }

    /// <inheritdoc/>
    public Result<ToolsValidation.BulkFixResult> FixAllSafeIssues()
    {
        if (_currentWorkspace == null)
        {
            return Result<ToolsValidation.BulkFixResult>.FromFailure("No workspace is open.");
        }

        var report = _lastValidationReport ?? _validator.Validate(_currentWorkspace);
        for (var i = 0; i < report.Findings.Count; i++)
        {
            var candidate = report.Findings[i];
            if (candidate.IsSafeBulkFix
                && candidate.FixKind != ToolsValidation.ValidationFixKind.None
                && !string.IsNullOrWhiteSpace(candidate.FilePath))
            {
                RecordStudioFileWrite(candidate.FilePath);
            }
        }

        var result = ToolsValidation.ValidationBulkActions.FixAllSafeIssues(_currentWorkspace, report, _atomicSaver);
        if (!result.Success)
        {
            return result;
        }

        if (result.Value.ModifiedFileCount > 0)
        {
            for (var i = 0; i < result.Value.ModifiedFiles.Count; i++)
            {
                RecordStudioFileWrite(result.Value.ModifiedFiles[i]);
            }

            ReloadWorkspaceIndexOnly();
        }

        return result;
    }

    /// <inheritdoc/>
    public string FormatValidationPlainText(IReadOnlyList<ValidationFinding> fallbackFindings)
    {
        if (_lastValidationReport != null)
        {
            return ToolsValidation.ValidationBulkActions.CopyAll(_lastValidationReport);
        }

        var sb = new StringBuilder();
        for (var i = 0; i < fallbackFindings.Count; i++)
        {
            var f = fallbackFindings[i];
            sb.AppendLine($"[{f.Severity}] {f.RootName}/{f.RelativeFile}:{f.LineNumber} {f.Message}");
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public IReadOnlyList<RenameAffectedSpan> PreviewRename(string currentId, string newId, AssetKind kind)
    {
        if (_currentWorkspace == null || string.IsNullOrWhiteSpace(newId))
        {
            return [];
        }

        var toolsKind = AssetSeedProjector.ToToolsAssetKind(kind);
        var planned = _renamePlanner.PlanRename(_currentWorkspace, toolsKind, currentId, newId.Trim());
        if (!planned.Success)
        {
            return [];
        }

        var spans = new List<RenameAffectedSpan>();
        for (var i = 0; i < planned.Value.FilePlans.Count; i++)
        {
            var filePlan = planned.Value.FilePlans[i];
            var relFile = Path.GetRelativePath(_currentWorkspace.RootPath, filePlan.FilePath).Replace('\\', '/');
            for (var e = 0; e < filePlan.Edits.Count; e++)
            {
                var edit = filePlan.Edits[e];
                spans.Add(new RenameAffectedSpan(
                    relFile,
                    Math.Max(1, edit.Line),
                    edit.FieldName,
                    edit.OldRawText,
                    edit.NewRawText));
            }
        }

        return spans;
    }

    /// <inheritdoc/>
    public StudioOperationResult ApplyRenameToDrafts(string currentId, string newId, AssetKind kind)
    {
        if (_currentWorkspace == null)
        {
            return new StudioOperationResult(false, "No workspace is open.");
        }

        var toolsKind = AssetSeedProjector.ToToolsAssetKind(kind);
        var planned = _renamePlanner.PlanRename(_currentWorkspace, toolsKind, currentId, newId.Trim());
        if (!planned.Success)
        {
            return new StudioOperationResult(false, planned.Message ?? "Failed planning rename.");
        }

        var applied = _renamePlanner.ApplyToDrafts(planned.Value, _draftStore);
        if (!applied.Success)
        {
            return new StudioOperationResult(false, applied.Message ?? "Failed staging rename drafts.");
        }

        _lastRenameBatch = applied.Value;
        var saveRequests = applied.Value.BuildSaveRequests();
        for (var i = 0; i < saveRequests.Count; i++)
        {
            RecordStudioFileWrite(saveRequests[i].DestinationPath);
        }

        var saved = _multiSaver.SaveAll(saveRequests);
        if (!saved.Success)
        {
            return new StudioOperationResult(false, saved.ErrorMessage ?? "Failed committing rename transaction.");
        }

        for (var i = 0; i < saveRequests.Count; i++)
        {
            RecordStudioFileWrite(saveRequests[i].DestinationPath);
            _draftStore.DiscardDraft(saveRequests[i].DestinationPath);
        }

        ReloadCurrentWorkspaceFromDisk();
        return new StudioOperationResult(
            true,
            $"Renamed '{currentId}' -> '{newId.Trim()}' across {planned.Value.FilePlans.Count} file(s).");
    }

    /// <inheritdoc/>
    public StudioOperationResult CreateAssetDocument(NewAssetRequest request)
    {
        if (_currentWorkspace == null)
        {
            return new StudioOperationResult(false, "No workspace is open.");
        }

        var primaryRoot = _currentWorkspace.GetPrimaryWritableRoot();
        if (!primaryRoot.Success)
        {
            return new StudioOperationResult(false, primaryRoot.Message ?? "No writable workspace root is available.");
        }

        var relative = request.TargetRelativeFile.Trim().TrimStart('/', '\\');
        var fullPath = Path.GetFullPath(Path.Combine(primaryRoot.Value.RootDirectory, relative));
        var toolsKind = AssetSeedProjector.ToToolsAssetKind(request.Kind);

        ToolsDocuments.TomlDocument doc;
        var expectNotExist = !request.UseExistingFile;
        string? expectedBaseHash = null;

        if (request.UseExistingFile && File.Exists(fullPath))
        {
            var loaded = ToolsDocuments.TomlDocument.Load(fullPath);
            if (!loaded.Success)
            {
                return new StudioOperationResult(false, loaded.Message ?? $"Failed reading '{relative}'.");
            }

            doc = loaded.Value;
            expectedBaseHash = ToolsStorage.ContentHash.ComputeSha256Hex(doc.ToBytes());
            var appended = _assetScaffolder.AppendToExistingFile(toolsKind, request.AssetId, doc);
            if (!appended.Success)
            {
                return new StudioOperationResult(false, appended.Message ?? "Failed appending asset entry.");
            }
        }
        else
        {
            var created = _assetScaffolder.CreateInNewFile(toolsKind, request.AssetId, fullPath);
            if (!created.Success)
            {
                return new StudioOperationResult(false, created.Message ?? "Failed scaffolding new asset file.");
            }

            doc = created.Value;
        }

        RecordStudioFileWrite(fullPath);
        var saveReq = new ToolsStorage.FileSaveRequest(
            fullPath,
            doc.ToBytes(),
            ExpectedBaseSha256: expectedBaseHash,
            ExpectFileNotToExist: expectNotExist);
        var saveResult = _atomicSaver.Save(in saveReq);
        if (!saveResult.Success)
        {
            return new StudioOperationResult(false, saveResult.ErrorMessage ?? "Failed writing new asset file.");
        }

        RecordStudioFileWrite(fullPath);
        ReloadCurrentWorkspaceFromDisk();
        return new StudioOperationResult(true, $"Created {request.Kind} '{request.AssetId}' in {relative}.", fullPath);
    }

    /// <inheritdoc/>
    public StudioOperationResult CreateLocalizationKey(string keyName, string targetRelativeFile, string initialText)
    {
        if (_currentWorkspace == null)
        {
            return new StudioOperationResult(false, "No workspace is open.");
        }

        var primaryRoot = _currentWorkspace.GetPrimaryWritableRoot();
        if (!primaryRoot.Success)
        {
            return new StudioOperationResult(false, primaryRoot.Message ?? "No writable workspace root is available.");
        }

        var rel = targetRelativeFile.Trim().TrimStart('/', '\\');
        var fullPath = Path.GetFullPath(Path.Combine(primaryRoot.Value.RootDirectory, rel));
        var created = _assetScaffolder.CreateLocalizationKey(fullPath, "en", keyName, initialText);
        if (!created.Success)
        {
            return new StudioOperationResult(false, created.Message ?? "Failed creating localization key.");
        }

        RecordStudioFileWrite(fullPath);
        var saveReq = new ToolsStorage.FileSaveRequest(fullPath, created.Value.ToBytes());
        var saved = _atomicSaver.Save(in saveReq);
        if (!saved.Success)
        {
            return new StudioOperationResult(false, saved.ErrorMessage ?? "Failed saving localization file.");
        }

        RecordStudioFileWrite(fullPath);
        ReloadCurrentWorkspaceFromDisk();
        return new StudioOperationResult(true, $"Created key '{keyName}' in {rel}.", fullPath);
    }

    /// <inheritdoc/>
    public StudioOperationResult ExecuteBlueprintAction(string action, string relativeId, string? argument = null)
    {
        if (_currentWorkspace == null)
        {
            return new StudioOperationResult(false, "No workspace is open.");
        }

        var primaryRoot = _currentWorkspace.GetPrimaryWritableRoot();
        if (!primaryRoot.Success)
        {
            return new StudioOperationResult(false, primaryRoot.Message ?? "No writable root available for blueprints.");
        }

        var bpDir = Path.Combine(primaryRoot.Value.AssetsDirectory, "blueprints");
        var manager = new ToolsBlueprints.PackBlueprintManager(bpDir, _atomicSaver);

        switch (action)
        {
            case "duplicate":
            {
                var copyId = $"{relativeId}_copy";
                var dupPath = Path.Combine(bpDir, copyId.Replace('/', Path.DirectorySeparatorChar) + ".wbp");
                RecordStudioFileWrite(dupPath);
                var dup = manager.Duplicate(relativeId, copyId);
                if (!dup.Success)
                {
                    return new StudioOperationResult(false, dup.Message ?? "Duplicate blueprint failed.");
                }

                RecordStudioFileWrite(dup.Value.FilePath);
                ReloadCurrentWorkspaceFromDisk();
                return new StudioOperationResult(true, $"Duplicated blueprint '{relativeId}' -> '{copyId}'.", dup.Value.FilePath);
            }
            case "delete":
            {
                var delPath = Path.Combine(bpDir, relativeId.Replace('/', Path.DirectorySeparatorChar) + ".wbp");
                RecordStudioFileWrite(delPath);
                var del = manager.Delete(relativeId);
                if (!del.Success)
                {
                    return new StudioOperationResult(false, del.Message ?? "Delete blueprint failed.");
                }

                ReloadCurrentWorkspaceFromDisk();
                return new StudioOperationResult(true, $"Deleted blueprint '{relativeId}'.");
            }
            case "rename":
            {
                var renamePath = Path.Combine(bpDir, relativeId.Replace('/', Path.DirectorySeparatorChar) + ".wbp");
                RecordStudioFileWrite(renamePath);
                var renamed = manager.RenameDisplayName(relativeId, argument ?? relativeId);
                if (!renamed.Success)
                {
                    return new StudioOperationResult(false, renamed.Message ?? "Rename blueprint failed.");
                }

                RecordStudioFileWrite(renamed.Value.FilePath);
                ReloadCurrentWorkspaceFromDisk();
                return new StudioOperationResult(true, $"Updated blueprint display name to '{renamed.Value.Name}'.");
            }
            case "move":
            {
                var targetId = string.IsNullOrWhiteSpace(argument) ? $"{relativeId}_moved" : argument;
                var srcPath = Path.Combine(bpDir, relativeId.Replace('/', Path.DirectorySeparatorChar) + ".wbp");
                var dstPath = Path.Combine(bpDir, targetId.Replace('/', Path.DirectorySeparatorChar) + ".wbp");
                RecordStudioFileWrite(srcPath);
                RecordStudioFileWrite(dstPath);
                var moved = manager.Move(relativeId, targetId);
                if (!moved.Success)
                {
                    return new StudioOperationResult(false, moved.Message ?? "Move blueprint failed.");
                }

                RecordStudioFileWrite(moved.Value.Entry.FilePath);
                ReloadCurrentWorkspaceFromDisk();
                return new StudioOperationResult(true, moved.Value.IdChangeNotice, moved.Value.Entry.FilePath);
            }
            default:
                return new StudioOperationResult(true, $"Blueprint action '{action}' ready for '{relativeId}'.");
        }
    }

    /// <inheritdoc/>
    public Result<ToolsEditing.AssetClipboardPayload> CaptureAssetClipboard(
        AssetKind kind,
        string assetId,
        string? absoluteFilePath,
        int tableIndex)
    {
        if (_currentWorkspace == null)
        {
            return Result<ToolsEditing.AssetClipboardPayload>.FromFailure("No workspace is open.");
        }

        if (string.IsNullOrWhiteSpace(absoluteFilePath))
        {
            return Result<ToolsEditing.AssetClipboardPayload>.FromFailure($"Asset '{assetId}' does not have a backing file on disk.");
        }

        var toolsKind = AssetSeedProjector.ToToolsAssetKind(kind);
        return _assetCloner.CaptureClipboardPayload(_currentWorkspace, toolsKind, assetId, absoluteFilePath, tableIndex);
    }

    /// <inheritdoc/>
    public Result<ToolsEditing.AssetCloneResult> DuplicateAsset(
        AssetKind kind,
        string assetId,
        string? absoluteFilePath,
        int tableIndex)
    {
        if (_currentWorkspace == null)
        {
            return Result<ToolsEditing.AssetCloneResult>.FromFailure("No workspace is open.");
        }

        if (string.IsNullOrWhiteSpace(absoluteFilePath))
        {
            return Result<ToolsEditing.AssetCloneResult>.FromFailure($"Asset '{assetId}' does not have a backing file on disk.");
        }

        RecordStudioFileWrite(absoluteFilePath);
        var toolsKind = AssetSeedProjector.ToToolsAssetKind(kind);
        var result = _assetCloner.DuplicateAsset(_currentWorkspace, toolsKind, assetId, absoluteFilePath, tableIndex);
        if (result.Success)
        {
            RecordStudioFileWrite(result.Value.TargetFilePath);
            ReloadWorkspaceIndexOnly();
        }

        return result;
    }

    /// <inheritdoc/>
    public Result<ToolsEditing.AssetCloneResult> PasteAsset(
        ToolsEditing.AssetClipboardPayload payload,
        string? targetFilePath = null)
    {
        if (_currentWorkspace == null)
        {
            return Result<ToolsEditing.AssetCloneResult>.FromFailure("No workspace is open.");
        }

        if (!string.IsNullOrWhiteSpace(targetFilePath))
        {
            RecordStudioFileWrite(targetFilePath);
        }

        var result = _assetCloner.PasteAsset(_currentWorkspace, payload, targetFilePath);
        if (result.Success)
        {
            RecordStudioFileWrite(result.Value.TargetFilePath);
            ReloadWorkspaceIndexOnly();
        }

        return result;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetExistingFilesForKind(AssetKind kind)
    {
        if (_currentWorkspace == null)
        {
            return [];
        }

        var folder = kind switch
        {
            AssetKind.Brick => "bricks",
            AssetKind.Item => "items",
            AssetKind.Paint => "paints",
            AssetKind.Recipe => "recipes",
            AssetKind.Skill => "skills",
            AssetKind.Body => "bodies",
            AssetKind.Material => "materials",
            AssetKind.Tag => "lang/tags",
            AssetKind.Localization => "lang/en",
            AssetKind.Blueprint => "blueprints",
            _ => string.Empty,
        };

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _currentWorkspace.Roots.Count; i++)
        {
            var root = _currentWorkspace.Roots[i];
            if (!root.IsWritable || !root.IsEnabled)
            {
                continue;
            }

            var targetDir = Path.Combine(root.AssetsDirectory, folder);
            if (!Directory.Exists(targetDir))
            {
                continue;
            }

            var pattern = kind == AssetKind.Blueprint ? "*.wbp" : "*.toml";
            foreach (var file in Directory.EnumerateFiles(targetDir, pattern, SearchOption.AllDirectories))
            {
                files.Add(Path.GetRelativePath(root.RootDirectory, file).Replace('\\', '/'));
            }
        }

        var sorted = new List<string>(files);
        sorted.Sort(StringComparer.OrdinalIgnoreCase);
        return sorted;
    }

    /// <inheritdoc/>
    public string GetDefaultLocalizationFile(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.Item => "assets/lang/en/item.toml",
            AssetKind.Brick => "assets/lang/en/brick.toml",
            AssetKind.Paint => "assets/lang/en/paint.toml",
            AssetKind.Recipe => "assets/lang/en/recipes.toml",
            AssetKind.Skill => "assets/lang/en/skills.toml",
            AssetKind.Body => "assets/lang/en/body.toml",
            _ => "assets/lang/en/ui.toml",
        };
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetLocalizationFiles()
    {
        var files = GetExistingFilesForKind(AssetKind.Localization);
        if (files.Count > 0)
        {
            return files;
        }

        return
        [
            "assets/lang/en/item.toml",
            "assets/lang/en/brick.toml",
            "assets/lang/en/paint.toml",
            "assets/lang/en/skills.toml",
            "assets/lang/en/body.toml",
            "assets/lang/en/ui.toml",
        ];
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetOrderedAssetRootDirectories()
    {
        if (_currentWorkspace == null)
        {
            return [];
        }

        var list = new List<string>(_currentWorkspace.Roots.Count);
        for (var i = 0; i < _currentWorkspace.Roots.Count; i++)
        {
            var root = _currentWorkspace.Roots[i];
            if (root.IsEnabled && Directory.Exists(root.AssetsDirectory))
            {
                list.Add(root.AssetsDirectory);
            }
        }

        return list;
    }

    /// <inheritdoc/>
    public string ResolveImagePathForWorkspace(string absoluteImagePath) =>
        ResolvePathInWorkspaceSubdirectory(absoluteImagePath, "textures");

    /// <inheritdoc/>
    public string ResolveMeshPathForWorkspace(string absoluteMeshPath) =>
        ResolvePathInWorkspaceSubdirectory(absoluteMeshPath, "meshes");

    private string ResolvePathInWorkspaceSubdirectory(string absolutePath, string preferredAssetsSubdir)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return string.Empty;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(absolutePath.Trim());
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed normalizing picked path '{absolutePath}': {ex.Message}");
            return absolutePath.Trim().Replace('\\', '/');
        }

        if (_currentWorkspace != null)
        {
            // Prefer assets/<preferredAssetsSubdir>/ across enabled roots (highest priority first).
            for (var i = _currentWorkspace.Roots.Count - 1; i >= 0; i--)
            {
                var root = _currentWorkspace.Roots[i];
                if (!root.IsEnabled || string.IsNullOrWhiteSpace(root.AssetsDirectory))
                {
                    continue;
                }

                if (TryMakeRelativeUnderDirectory(Path.Combine(root.AssetsDirectory, preferredAssetsSubdir), fullPath, out var relSubdir))
                {
                    return relSubdir;
                }
            }

            if (TryMakeRelativeUnderDirectory(Path.Combine(_currentWorkspace.RootPath, "assets", preferredAssetsSubdir), fullPath, out var rootRelSubdir))
            {
                return rootRelSubdir;
            }

            for (var i = _currentWorkspace.Roots.Count - 1; i >= 0; i--)
            {
                var root = _currentWorkspace.Roots[i];
                if (!root.IsEnabled)
                {
                    continue;
                }

                if (TryMakeRelativeUnderDirectory(root.AssetsDirectory, fullPath, out var relAssets))
                {
                    return relAssets;
                }
            }

            if (TryMakeRelativeUnderDirectory(Path.Combine(_currentWorkspace.RootPath, "assets"), fullPath, out var rootRelAssets))
            {
                return rootRelAssets;
            }

            for (var i = _currentWorkspace.Roots.Count - 1; i >= 0; i--)
            {
                var root = _currentWorkspace.Roots[i];
                if (!root.IsEnabled)
                {
                    continue;
                }

                if (TryMakeRelativeUnderDirectory(root.RootDirectory, fullPath, out var relRoot))
                {
                    return relRoot;
                }
            }

            if (TryMakeRelativeUnderDirectory(_currentWorkspace.RootPath, fullPath, out var relWorkspace))
            {
                return relWorkspace;
            }
        }

        return fullPath.Replace('\\', '/');
    }

    /// <inheritdoc/>
    public string ResolveBlockLayerNameForWorkspace(string absoluteImagePath)
    {
        if (string.IsNullOrWhiteSpace(absoluteImagePath))
        {
            return string.Empty;
        }

        var trimmed = absoluteImagePath.Trim();
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(trimmed);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed normalizing picked block texture path '{absoluteImagePath}': {ex.Message}");
            var fallbackFileName = Path.GetFileName(trimmed.Replace('\\', '/'));
            return string.IsNullOrWhiteSpace(fallbackFileName)
                ? string.Empty
                : Path.GetFileNameWithoutExtension(fallbackFileName);
        }

        if (_currentWorkspace != null)
        {
            foreach (var kvp in _currentWorkspace.WinningBlockAlbedoLayers)
            {
                if (string.Equals(kvp.Value.FilePath, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }
        }

        var fileName = Path.GetFileName(fullPath);
        return string.IsNullOrWhiteSpace(fileName)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(fileName);
    }

    /// <inheritdoc/>
    public IReadOnlyList<CommandPaletteEntry> SearchCommandPalette(
        string query,
        string categoryFilter,
        string scopeFilter,
        int maxResults = 60)
    {
        if (_searchIndex.Count == 0)
        {
            return [];
        }

        ToolsSearch.SearchEntryKind? kindFilter = categoryFilter switch
        {
            "Assets" => ToolsSearch.SearchEntryKind.Asset,
            "Resources" => ToolsSearch.SearchEntryKind.Resource,
            "Actions" => ToolsSearch.SearchEntryKind.Action,
            _ => null,
        };

        var scope = scopeFilter switch
        {
            "Writable" => ToolsSearch.SearchScope.Writable,
            "Reference" => ToolsSearch.SearchScope.Reference,
            "Overridden" => ToolsSearch.SearchScope.Overridden,
            _ => ToolsSearch.SearchScope.All,
        };

        var results = _searchIndex.Search(new ToolsSearch.SearchQuery(
            Text: query ?? string.Empty,
            KindFilter: kindFilter,
            AssetKindFilter: null,
            ScopeFilter: scope,
            MaxResults: maxResults));

        var mapped = new List<CommandPaletteEntry>(results.Count);
        for (var i = 0; i < results.Count; i++)
        {
            var entry = results[i].Entry;
            var category = entry.Kind switch
            {
                ToolsSearch.SearchEntryKind.Asset => "Assets",
                ToolsSearch.SearchEntryKind.Resource => "Resources",
                _ => "Actions",
            };

            AssetKind? assetKind = null;
            if (!string.IsNullOrWhiteSpace(entry.AssetKind)
                && Enum.TryParse(entry.AssetKind, ignoreCase: true, out AssetKind parsedKind))
            {
                assetKind = parsedKind;
            }

            AssetProvenance? prov = null;
            if (entry.Kind != ToolsSearch.SearchEntryKind.Action)
            {
                if (_currentWorkspace?.Mode == ToolsWorkspace.WorkspaceMode.Repo)
                {
                    prov = AssetProvenance.Repo;
                }
                else if (!entry.Provenance.IsWritable)
                {
                    prov = AssetProvenance.Base;
                }
                else
                {
                    prov = entry.Provenance.IsShadowed || !string.IsNullOrEmpty(entry.Provenance.ShadowedByRoot)
                        ? AssetProvenance.Override
                        : AssetProvenance.Pack;
                }
            }

            var targetAssetId = entry.Kind == ToolsSearch.SearchEntryKind.Asset ? entry.Id : null;
            var actionId = entry.Kind == ToolsSearch.SearchEntryKind.Action
                ? entry.Id
                : (entry.Kind == ToolsSearch.SearchEntryKind.Resource ? $"resource:{entry.Id}" : null);

            mapped.Add(new CommandPaletteEntry(
                Title: entry.Kind == ToolsSearch.SearchEntryKind.Action ? entry.DisplayName : entry.Id,
                Subtitle: entry.Kind == ToolsSearch.SearchEntryKind.Action
                    ? entry.ResourcePath ?? "Workbench command"
                    : $"{entry.AssetKind ?? entry.Kind.ToString()} · {entry.ResourcePath ?? entry.Provenance.RootName}",
                Category: category,
                Kind: assetKind,
                TargetAssetId: targetAssetId,
                ActionId: actionId,
                Provenance: prov,
                ResolutionLabel: entry.Kind == ToolsSearch.SearchEntryKind.Action ? "Command" : entry.ResolutionState.ToString(),
                ShadowedFile: entry.Provenance.ShadowedByRoot != null ? $"{entry.Provenance.ShadowedByRoot}/{entry.ResourcePath}" : null,
                Score: results[i].Score));
        }

        return mapped;
    }

    /// <inheritdoc/>
    public string BuildStagedPackToml(string packId, string packNamespace, int contentVersion)
    {
        var meta = new ToolsPacks.PackMetadata(
            string.IsNullOrWhiteSpace(packId) ? "pack" : packId,
            string.IsNullOrWhiteSpace(packNamespace) ? "pack" : packNamespace,
            (uint)Math.Max(1, contentVersion),
            TOOL_VERSION,
            DateTimeOffset.UtcNow);
        return meta.ToToml().TrimEnd();
    }

    /// <inheritdoc/>
    public StudioOperationResult ExportPack(ExportPackRequest request)
    {
        if (_currentWorkspace == null)
        {
            return new StudioOperationResult(false, "No workspace is open to export.");
        }

        var outputDir = Path.Combine(_dataPaths.DataDirectory, "exports");
        var isTarGz = !string.IsNullOrWhiteSpace(request.ArchiveFormat)
            && request.ArchiveFormat.Contains("tar", StringComparison.OrdinalIgnoreCase);
        var format = isTarGz
            ? ToolsPacks.PackArchiveFormat.TarGz
            : ToolsPacks.PackArchiveFormat.Zip;

        var outcome = _packExporter.Export(
            _currentWorkspace,
            outputDir,
            TOOL_VERSION,
            confirmWarnings: request.AcknowledgeWarnings,
            format: format);

        if (!outcome.Success)
        {
            _logSession?.LogWarning($"Pack export did not complete: {outcome.ErrorMessage}");
            return new StudioOperationResult(false, outcome.ErrorMessage ?? "Pack export failed.");
        }

        _logSession?.LogInfo($"Exported staged pack archive to '{outcome.ArchivePath}'.");
        return new StudioOperationResult(
            true,
            $"Staged and exported {Path.GetFileName(outcome.ArchivePath)}",
            outcome.ArchivePath);
    }

    /// <inheritdoc/>
    public AboutDiagnosticsInfo GetDiagnosticsInfo(
        WorkspaceMode mode,
        string rootPath,
        string? referenceRoot,
        string glVendorRenderer)
    {
        var roots = BuildRootsSummary(rootPath, referenceRoot);
        var logPath = _logSession?.LogFilePath ?? Path.Combine(_dataPaths.DataDirectory, "logs", "latest.log");
        return new AboutDiagnosticsInfo(
            ToolVersion: TOOL_VERSION,
            ContentVersion: (int)GameContentVersion.CURRENT,
            WorkspaceModeLabel: mode == WorkspaceMode.Pack ? "Pack Mode" : "Repository Mode",
            RootsSummary: roots,
            OsArchitecture: $"{RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.OSArchitecture})",
            GlVendorRenderer: glVendorRenderer,
            AvaloniaVersion: AVALONIA_VERSION,
            LogPath: logPath);
    }

    /// <inheritdoc/>
    public StudioOperationResult CopyDiagnosticsZip(AboutDiagnosticsInfo info)
    {
        var diagnosticsDir = Path.Combine(_dataPaths.DataDirectory, "diagnostics");
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var outputZip = Path.Combine(diagnosticsDir, $"asset-studio-diagnostics-{stamp}.zip");

        _logSession?.Flush();

        var rootPaths = new List<string>();
        if (_currentWorkspace != null)
        {
            for (var i = 0; i < _currentWorkspace.Roots.Count; i++)
            {
                rootPaths.Add(_currentWorkspace.Roots[i].RootDirectory);
            }
        }

        var (vendor, renderer) = SplitVendorRenderer(info.GlVendorRenderer);
        var input = new ToolsDiagnostics.DiagnosticsBundleInput(
            ToolVersion: info.ToolVersion,
            ContentVersion: (uint)Math.Max(1, info.ContentVersion),
            WorkspaceMode: _currentWorkspace?.Mode ?? ToolsWorkspace.WorkspaceMode.Pack,
            WorkspaceRoots: rootPaths,
            GlVendor: vendor,
            GlRenderer: renderer,
            AvaloniaVersion: info.AvaloniaVersion,
            LogFilePath: _logSession?.LogFilePath ?? info.LogPath,
            ValidationReport: _lastValidationReport,
            RecentErrors: _logSession?.GetRecentErrors() ?? []);

        var written = _diagnosticsWriter.WriteZip(outputZip, in input);
        if (!written.Success)
        {
            _logSession?.LogError($"Failed creating diagnostics zip: {written.Message}");
            return new StudioOperationResult(false, written.Message ?? "Failed writing diagnostics zip.");
        }

        return new StudioOperationResult(
            true,
            $"Diagnostics archive written to {outputZip} (content files excluded, paths redacted).",
            outputZip);
    }

    /// <inheritdoc/>
    public void MarkCleanShutdown()
    {
        _draftStore.MarkCleanShutdown();
        _logSession?.LogInfo("Asset Studio clean shutdown recorded.");
        _logSession?.Flush();
    }

    private StudioWorkspaceSnapshot FinalizeLoadedWorkspace(
        ToolsWorkspace.WorkspaceIndex workspace,
        string? explicitDisplayName)
    {
        _currentWorkspace = workspace;
        _currentWorkspaceDisplayName = explicitDisplayName;
        _lastValidationReport = _validator.Validate(workspace);
        RebuildSearchIndex(workspace, _lastValidationReport);

        _settings.AddRecentWorkspace(workspace.RootPath);
        _settingsStore.Save(_settings);

        IReadOnlyList<ToolsStorage.DraftRecoveryEntry> pendingDrafts = [];
        if (_draftStore.HadUncleanPreviousExit)
        {
            var recovered = _draftStore.GetRecoverableDrafts();
            if (recovered.Success)
            {
                pendingDrafts = FilterDraftsForWorkspace(workspace, recovered.Value);
            }
        }

        _logSession?.LogInfo(
            $"Opened {workspace.Mode} workspace '{workspace.RootPath}' ({workspace.WinningAssets.Count} winning assets, " +
            $"{_lastValidationReport.FatalCount} fatal, {_lastValidationReport.BrokenCount} broken).");

        return AssetSeedProjector.BuildSnapshot(
            workspace,
            _lastValidationReport,
            pendingDrafts,
            explicitDisplayName,
            _settings.GetDefaultNamespace(workspace.RootPath));
    }

    private void RebuildSearchIndex(
        ToolsWorkspace.WorkspaceIndex workspace,
        ToolsValidation.ValidationReport report)
    {
        var brokenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < report.Findings.Count; i++)
        {
            var f = report.Findings[i];
            if (f.Severity == ToolsValidation.ValidationSeverity.Fatal || f.Severity == ToolsValidation.ValidationSeverity.Broken)
            {
                brokenFiles.Add(f.FilePath);
            }
            else if (f.Severity == ToolsValidation.ValidationSeverity.CrossPlatform || f.Severity == ToolsValidation.ValidationSeverity.Style)
            {
                warnFiles.Add(f.FilePath);
            }
        }

        var entries = new List<ToolsSearch.SearchEntry>();

        foreach (var asset in workspace.WinningAssets.Values)
        {
            var p = asset.Provenance;
            var state = brokenFiles.Contains(p.FilePath)
                ? ToolsSearch.SearchResolutionState.Broken
                : (warnFiles.Contains(p.FilePath) ? ToolsSearch.SearchResolutionState.Warning : ToolsSearch.SearchResolutionState.Resolved);

            var localStem = asset.Id.Contains(':') ? asset.Id.Substring(asset.Id.IndexOf(':') + 1) : asset.Id;
            var extraTokens = new List<string> { localStem, p.ProviderRootName };
            if (asset.AuthoritativeDefinition is RecipeDefinition recipeDef)
            {
                if (!string.IsNullOrWhiteSpace(recipeDef.TargetTag))
                {
                    extraTokens.Add(recipeDef.TargetTag.Trim());
                }

                if (recipeDef.Inputs != null)
                {
                    for (var i = 0; i < recipeDef.Inputs.Count; i++)
                    {
                        var itemId = recipeDef.Inputs[i].ItemId?.Trim();
                        if (!string.IsNullOrWhiteSpace(itemId))
                        {
                            extraTokens.Add(itemId);
                        }
                    }
                }

                if (recipeDef.Outputs != null)
                {
                    for (var i = 0; i < recipeDef.Outputs.Count; i++)
                    {
                        var itemId = recipeDef.Outputs[i].ItemId?.Trim();
                        if (!string.IsNullOrWhiteSpace(itemId))
                        {
                            extraTokens.Add(itemId);
                        }
                    }
                }
            }

            entries.Add(new ToolsSearch.SearchEntry(
                Id: asset.Id,
                DisplayName: asset.DisplayName,
                Kind: ToolsSearch.SearchEntryKind.Asset,
                Provenance: new ToolsSearch.SearchProvenance(
                    p.ProviderRootName,
                    p.IsWritable,
                    IsShadowed: false,
                    ShadowedByRoot: p.IsOverride ? p.ShadowedProviders[^1].RootName : null,
                    FilePath: p.FilePath),
                ResolutionState: state,
                AssetKind: asset.Kind.ToString(),
                LocalizationKey: null,
                ResourcePath: p.RelativePath,
                ExtraTokens: extraTokens));
        }

        var seenTags = new HashSet<string>(StringComparer.Ordinal);
        for (var i = workspace.TagEntries.Count - 1; i >= 0; i--)
        {
            var tag = workspace.TagEntries[i];
            if (!seenTags.Add(tag.TagName))
            {
                continue;
            }

            entries.Add(new ToolsSearch.SearchEntry(
                Id: tag.TagName,
                DisplayName: tag.TagName,
                Kind: ToolsSearch.SearchEntryKind.Asset,
                Provenance: new ToolsSearch.SearchProvenance(tag.RootName, tag.IsWritable, false, null, tag.FilePath),
                ResolutionState: ToolsSearch.SearchResolutionState.Resolved,
                AssetKind: AssetKind.Tag.ToString(),
                ResourcePath: Path.GetFileName(tag.FilePath)));
        }

        foreach (var res in workspace.WinningResources.Values)
        {
            entries.Add(new ToolsSearch.SearchEntry(
                Id: res.Id,
                DisplayName: res.Id,
                Kind: ToolsSearch.SearchEntryKind.Resource,
                Provenance: new ToolsSearch.SearchProvenance(
                    res.ProviderRootName,
                    res.IsWritable,
                    IsShadowed: false,
                    ShadowedByRoot: res.ShadowedRootIds.Count > 0 ? res.ShadowedRootIds[^1] : null,
                    FilePath: res.FilePath),
                ResolutionState: ToolsSearch.SearchResolutionState.Resolved,
                AssetKind: res.Kind.ToString(),
                ResourcePath: res.RelativePath));
        }

        AddActionEntry(entries, "action:new-asset", "New Asset...", "Create a new asset or add to an existing file");
        AddActionEntry(entries, "action:revalidate", "Revalidate Workspace", "Run validation checks across all workspace roots");
        AddActionEntry(entries, "action:export", "Export Pack Archive...", "Validate and export a pack archive");
        AddActionEntry(entries, "action:theme", "Cycle Theme (System / Light / Dark)", "Cycle between System, Light, and Dark studio themes");
        AddActionEntry(entries, "action:settings", "Open Settings", "Configure theme, UI scale, default namespace, language, renderer, and log level");
        AddActionEntry(entries, "action:recovery", "Draft Recovery...", "Inspect unsaved recovery snapshots");

        _searchIndex.SetEntries(entries);
    }

    private static void AddActionEntry(List<ToolsSearch.SearchEntry> entries, string id, string title, string subtitle)
    {
        entries.Add(new ToolsSearch.SearchEntry(
            Id: id,
            DisplayName: title,
            Kind: ToolsSearch.SearchEntryKind.Action,
            Provenance: new ToolsSearch.SearchProvenance("studio", true, false),
            ResolutionState: ToolsSearch.SearchResolutionState.Resolved,
            ResourcePath: subtitle));
    }

    private StudioOperationResult SaveBlueprintMetadata(DocumentSavePayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.AbsoluteFilePath))
        {
            return new StudioOperationResult(false, $"Blueprint '{payload.AssetId}' is missing its backing file path.");
        }

        var dir = Path.GetDirectoryName(payload.AbsoluteFilePath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return new StudioOperationResult(false, "Cannot determine blueprint directory.");
        }

        var bpRoot = FindBlueprintsRoot(payload.AbsoluteFilePath) ?? dir;
        var relativeId = Path.GetRelativePath(bpRoot, payload.AbsoluteFilePath).Replace('\\', '/');
        if (relativeId.EndsWith(".wbp", StringComparison.OrdinalIgnoreCase))
        {
            relativeId = relativeId.Substring(0, relativeId.Length - 4);
        }

        RecordStudioFileWrite(payload.AbsoluteFilePath);
        var manager = new ToolsBlueprints.PackBlueprintManager(bpRoot, _atomicSaver);
        var updated = manager.RenameDisplayName(relativeId, payload.BlueprintDisplayName ?? payload.AssetId);
        if (updated.Success)
        {
            RecordStudioFileWrite(updated.Value.FilePath);
        }

        return updated.Success
            ? new StudioOperationResult(true, $"Saved blueprint '{updated.Value.Name}'.")
            : new StudioOperationResult(false, updated.Message ?? "Failed saving blueprint metadata.");
    }

    private void RecordStudioFileWrite(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed recording studio file write for '{filePath}': {ex.Message}");
            return;
        }

        var now = Environment.TickCount64;
        using (_recentSavesLock.EnterScope())
        {
            _recentStudioSaves[normalized] = now;
            if (_recentStudioSaves.Count > 64)
            {
                List<string>? expired = null;
                foreach (var kvp in _recentStudioSaves)
                {
                    if (now - kvp.Value > RECENT_STUDIO_SAVE_PRUNE_MS)
                    {
                        expired ??= [];
                        expired.Add(kvp.Key);
                    }
                }

                if (expired != null)
                {
                    for (var i = 0; i < expired.Count; i++)
                    {
                        _recentStudioSaves.Remove(expired[i]);
                    }
                }
            }
        }
    }

    private string? ResolveDeleteTargetPath(ToolsWorkspace.AssetKind toolsKind, string assetId, string? absoluteFilePath)
    {
        if (!string.IsNullOrWhiteSpace(absoluteFilePath))
        {
            return Path.GetFullPath(absoluteFilePath);
        }

        if (_currentWorkspace == null || string.IsNullOrWhiteSpace(assetId))
        {
            return null;
        }

        if (toolsKind == ToolsWorkspace.AssetKind.Tag)
        {
            for (var i = _currentWorkspace.TagEntries.Count - 1; i >= 0; i--)
            {
                var entry = _currentWorkspace.TagEntries[i];
                if (entry.IsWritable && string.Equals(entry.TagName, assetId, StringComparison.Ordinal))
                {
                    return entry.FilePath;
                }
            }

            return null;
        }

        var found = _currentWorkspace.GetAsset(toolsKind, assetId);
        return found.Success ? found.Value.Provenance.FilePath : null;
    }

    private static void AddExistingWatchDirectory(string? dir, List<string> output, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(dir))
        {
            return;
        }

        try
        {
            var full = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Directory.Exists(full) && seen.Add(full))
            {
                output.Add(full);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Skipping invalid watch directory '{dir}': {ex.Message}");
        }
    }

    private static List<string> RemoveNestedWatchDirectories(List<string> dirs)
    {
        if (dirs.Count <= 1)
        {
            return dirs;
        }

        dirs.Sort(static (left, right) => left.Length.CompareTo(right.Length));
        var kept = new List<string>(dirs.Count);
        for (var i = 0; i < dirs.Count; i++)
        {
            var candidate = dirs[i];
            var isNested = false;
            for (var j = 0; j < kept.Count; j++)
            {
                var parentPrefix = kept[j] + Path.DirectorySeparatorChar;
                if (candidate.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    isNested = true;
                    break;
                }
            }

            if (!isNested)
            {
                kept.Add(candidate);
            }
        }

        return kept;
    }

    private string? ResolveAbsoluteFilePath(DocumentSavePayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.AbsoluteFilePath))
        {
            return Path.GetFullPath(payload.AbsoluteFilePath);
        }

        if (_currentWorkspace == null)
        {
            return null;
        }

        var primary = _currentWorkspace.GetPrimaryWritableRoot();
        if (!primary.Success)
        {
            return null;
        }

        var rel = payload.RelativeFile.Trim().TrimStart('/', '\\');
        return Path.GetFullPath(Path.Combine(primary.Value.RootDirectory, rel));
    }

    private string? ResolveSingleReferenceInstall(string packRoot)
    {
        var located = _installLocator.Locate();
        if (located.Candidates.Count == 1)
        {
            var candidate = located.Candidates[0].InstallPath;
            if (!string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(packRoot), StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private string BuildRootsSummary(string rootPath, string? referenceRoot)
    {
        if (_currentWorkspace == null || _currentWorkspace.Roots.Count == 0)
        {
            return referenceRoot != null
                ? $"Writable: {rootPath}\nReference: {referenceRoot}"
                : $"Writable: {rootPath}";
        }

        var lines = new List<string>(_currentWorkspace.Roots.Count);
        for (var i = 0; i < _currentWorkspace.Roots.Count; i++)
        {
            var root = _currentWorkspace.Roots[i];
            if (!root.IsEnabled)
            {
                continue;
            }

            var access = root.IsWritable ? "Writable" : "Reference";
            lines.Add($"{access}: {root.Id} ({root.RootDirectory})");
        }

        return string.Join('\n', lines);
    }

    private static IReadOnlyList<ToolsStorage.DraftRecoveryEntry> FilterDraftsForWorkspace(
        ToolsWorkspace.WorkspaceIndex workspace,
        IReadOnlyList<ToolsStorage.DraftRecoveryEntry> allDrafts)
    {
        var matching = new List<ToolsStorage.DraftRecoveryEntry>();
        for (var i = 0; i < allDrafts.Count; i++)
        {
            var dest = Path.GetFullPath(allDrafts[i].DestinationPath);
            for (var r = 0; r < workspace.Roots.Count; r++)
            {
                var prefix = Path.GetFullPath(workspace.Roots[r].RootDirectory).TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (dest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matching.Add(allDrafts[i]);
                    break;
                }
            }
        }

        return matching;
    }

    private static bool TryMakeRelativeUnderDirectory(string? baseDirectory, string fullFilePath, out string relative)
    {
        relative = string.Empty;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return false;
        }

        try
        {
            var normalizedBase = Path.GetFullPath(baseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var prefix = normalizedBase + Path.DirectorySeparatorChar;
            if (!fullFilePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            relative = Path.GetRelativePath(normalizedBase, fullFilePath).Replace('\\', '/');
            return !string.IsNullOrWhiteSpace(relative) && !relative.StartsWith("..", StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed computing relative path under '{baseDirectory}': {ex.Message}");
            return false;
        }
    }

    private static StudioWorkspaceSnapshot BuildEmptySnapshot(WorkspaceMode mode, string rootPath, string? displayName)
    {
        var folder = string.IsNullOrWhiteSpace(rootPath)
            ? string.Empty
            : Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var name = string.IsNullOrWhiteSpace(displayName) ? folder : displayName.Trim();
        var effectiveName = string.IsNullOrWhiteSpace(name) ? "Workspace" : name;
        var effectivePackId = !string.IsNullOrWhiteSpace(folder)
            ? folder
            : (!string.IsNullOrWhiteSpace(name) ? name : "pack");
        var effectiveNamespace = mode == WorkspaceMode.Repo ? "wb" : effectivePackId;
        var isUninitialized = mode == WorkspaceMode.Pack && !AssetSeedProjector.HasPackStructure(rootPath);

        return new StudioWorkspaceSnapshot
        {
            WorkspaceName = effectiveName,
            Mode = mode,
            RootPath = rootPath,
            ReferenceRootPath = null,
            ContentVersion = (int)GameContentVersion.CURRENT,
            PackId = effectivePackId,
            PackNamespace = effectiveNamespace,
            DefaultNamespace = effectiveNamespace,
            AvailableShaders = [],
            WorkspaceImages = [],
            IsUninitializedFolder = isUninitialized,
            Assets = [],
            Findings = [],
            Candidates = [],
            PendingDrafts = [],
        };
    }

    private static int CountQuickAssets(string rootPath)
    {
        try
        {
            var assetsDir = Path.Combine(rootPath, "assets");
            if (Directory.Exists(assetsDir))
            {
                return Directory.EnumerateFiles(assetsDir, "*.toml", SearchOption.AllDirectories).Count();
            }

            return Directory.EnumerateDirectories(rootPath)
                .Where(static d => Directory.Exists(Path.Combine(d, "assets")))
                .Sum(static d => Directory.EnumerateFiles(Path.Combine(d, "assets"), "*.toml", SearchOption.AllDirectories).Count());
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed counting assets under '{rootPath}': {ex.Message}");
            return 0;
        }
    }

    private static string? FindBlueprintsRoot(string fullFilePath)
    {
        var dir = Path.GetDirectoryName(fullFilePath);
        while (!string.IsNullOrEmpty(dir))
        {
            if (string.Equals(Path.GetFileName(dir), "blueprints", StringComparison.OrdinalIgnoreCase))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    private static (string Vendor, string Renderer) SplitVendorRenderer(string combined)
    {
        if (string.IsNullOrWhiteSpace(combined))
        {
            return ("Unknown", "Software Fallback");
        }

        var sep = combined.IndexOf('/');
        if (sep > 0 && sep + 1 < combined.Length)
        {
            return (combined.Substring(0, sep).Trim(), combined.Substring(sep + 1).Trim());
        }

        return (combined.Trim(), combined.Trim());
    }

    private static string FormatCultureSetting(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "System Default";
        }

        if (string.Equals(raw, "en", StringComparison.OrdinalIgnoreCase))
        {
            return "en (English)";
        }

        if (string.Equals(raw, ToolsLocalization.ToolLocalization.PSEUDO_LOCALE_NAME, StringComparison.OrdinalIgnoreCase))
        {
            return "qps-ploc (Pseudo-locale)";
        }

        return raw;
    }

    private static string ParseCultureSetting(string? uiValue)
    {
        if (string.IsNullOrWhiteSpace(uiValue) || uiValue.StartsWith("System", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (uiValue.StartsWith("qps-ploc", StringComparison.OrdinalIgnoreCase))
        {
            return ToolsLocalization.ToolLocalization.PSEUDO_LOCALE_NAME;
        }

        if (uiValue.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return "en";
        }

        return uiValue.Trim();
    }

    private static string FormatGlPreference(ToolsSettings.GlBackendPreference pref)
    {
        return pref switch
        {
            ToolsSettings.GlBackendPreference.Wgl or ToolsSettings.GlBackendPreference.Glx => "OpenGL Core (Desktop)",
            ToolsSettings.GlBackendPreference.Angle or ToolsSettings.GlBackendPreference.Egl => "OpenGL ES (ANGLE / EGL)",
            _ => "Auto",
        };
    }

    private static ToolsSettings.GlBackendPreference ParseGlPreference(string? uiValue)
    {
        if (string.IsNullOrWhiteSpace(uiValue) || uiValue.StartsWith("Auto", StringComparison.OrdinalIgnoreCase))
        {
            return ToolsSettings.GlBackendPreference.Auto;
        }

        if (uiValue.Contains("Desktop", StringComparison.OrdinalIgnoreCase))
        {
            return OperatingSystem.IsWindows()
                ? ToolsSettings.GlBackendPreference.Wgl
                : ToolsSettings.GlBackendPreference.Glx;
        }

        if (uiValue.Contains("EGL", StringComparison.OrdinalIgnoreCase) || uiValue.Contains("ANGLE", StringComparison.OrdinalIgnoreCase))
        {
            return OperatingSystem.IsWindows()
                ? ToolsSettings.GlBackendPreference.Angle
                : ToolsSettings.GlBackendPreference.Egl;
        }

        return ToolsSettings.GlBackendPreference.Auto;
    }

    private ToolsValidation.ValidationFinding? ResolveBackendValidationFinding(
        ToolsValidation.ValidationReport report,
        in ValidationFinding studioFinding)
    {
        if (studioFinding.BackendFindingIndex >= 0
            && studioFinding.BackendFindingIndex < report.Findings.Count)
        {
            var indexed = report.Findings[studioFinding.BackendFindingIndex];
            if (string.Equals(indexed.Code, studioFinding.Code, StringComparison.Ordinal)
                && Math.Max(1, indexed.Line) == studioFinding.LineNumber)
            {
                return indexed;
            }
        }

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        for (var i = 0; i < report.Findings.Count; i++)
        {
            var candidate = report.Findings[i];
            if (!string.Equals(candidate.Code, studioFinding.Code, StringComparison.Ordinal)
                || Math.Max(1, candidate.Line) != studioFinding.LineNumber)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(studioFinding.AbsoluteFilePath)
                && !string.IsNullOrWhiteSpace(candidate.FilePath)
                && string.Equals(Path.GetFullPath(candidate.FilePath), Path.GetFullPath(studioFinding.AbsoluteFilePath), pathComparison))
            {
                return candidate;
            }

            if (string.Equals(candidate.Message, studioFinding.Message, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private void RecordWritableEnglishLocalizationFiles()
    {
        if (_currentWorkspace == null)
        {
            return;
        }

        for (var i = 0; i < _currentWorkspace.Roots.Count; i++)
        {
            var root = _currentWorkspace.Roots[i];
            if (!root.IsEnabled || !root.IsWritable || string.IsNullOrWhiteSpace(root.AssetsDirectory))
            {
                continue;
            }

            var enDir = Path.Combine(root.AssetsDirectory, "lang", "en");
            if (!Directory.Exists(enDir))
            {
                continue;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(enDir, "*.toml", SearchOption.TopDirectoryOnly))
                {
                    RecordStudioFileWrite(file);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed enumerating localization files under '{enDir}': {ex.Message}");
            }
        }
    }

    private void ReloadWorkspaceIndexOnly()
    {
        if (_currentWorkspace == null)
        {
            return;
        }

        var reloaded = _currentWorkspace.Mode == ToolsWorkspace.WorkspaceMode.Repo
            ? _workspaceLoader.LoadRepo(_currentWorkspace.RootPath)
            : _workspaceLoader.LoadPack(_currentWorkspace.RootPath, _currentWorkspace.ReferenceInstallPath);

        if (reloaded.Success)
        {
            _currentWorkspace = reloaded.Value;
        }
    }
}
