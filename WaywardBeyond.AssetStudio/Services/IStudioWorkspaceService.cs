using Swordfish.Library.Util;
using WaywardBeyond.AssetStudio.Models;
using ToolsEditing = WaywardBeyond.AssetTools.Editing;
using ToolsValidation = WaywardBeyond.AssetTools.Validation;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Defines backend workspace operations behind a UI-testable service contract.
/// </summary>
public interface IStudioWorkspaceService
{
    /// <summary>
    ///     Gets whether this service is backed by the real disk-based AssetTools backend.
    /// </summary>
    bool UsesRealBackend { get; }

    /// <summary>
    ///     Returns recently opened workspaces for the start screen.
    /// </summary>
    IReadOnlyList<RecentWorkspaceEntry> GetRecentWorkspaces();

    /// <summary>
    ///     Removes a workspace from recent history and optionally deletes its folder recursively from disk.
    /// </summary>
    StudioOperationResult RemoveRecentWorkspace(string rootPath, bool deleteFromDisk);

    /// <summary>
    ///     Loads a workspace snapshot for the requested mode and path.
    /// </summary>
    StudioWorkspaceSnapshot OpenWorkspace(WorkspaceMode mode, string rootPath, string? displayName = null);

    /// <summary>
    ///     Auto-detects and opens a repository or content pack workspace at <paramref name="rootPath"/>.
    /// </summary>
    StudioWorkspaceSnapshot? TryOpenAuto(string rootPath, string? referenceInstallPath = null);

    /// <summary>
    ///     Scaffolds a new content pack directory with manifest, pack metadata, and asset folders.
    /// </summary>
    StudioOperationResult CreatePackWorkspace(
        string directory,
        string packId,
        string packNamespace,
        string displayName,
        string author);

    /// <summary>
    ///     Discovers installed Wayward Beyond game directories without guessing when multiple exist.
    /// </summary>
    IReadOnlyList<string> LocateGameInstalls();

    /// <summary>
    ///     Returns current persisted studio user settings.
    /// </summary>
    StudioSettingsState GetSettings();

    /// <summary>
    ///     Saves updated studio user settings.
    /// </summary>
    StudioOperationResult SaveSettings(StudioSettingsState settings);

    /// <summary>
    ///     Atomically saves dirty workspace documents with conflict verification and rollback.
    /// </summary>
    WorkspaceSaveResult SaveDocuments(IReadOnlyList<DocumentSavePayload> dirtyDocuments);

    /// <summary>
    ///     Persists a single document draft snapshot for crash recovery.
    /// </summary>
    StudioOperationResult SaveDraft(DocumentSavePayload payload);

    /// <summary>
    ///     Computes lossless preview TOML reflecting form edits over the original file text.
    /// </summary>
    string? PreviewEditedToml(DocumentSavePayload payload);

    /// <summary>
    ///     Applies a recovered crash draft back to its target file or workspace buffer.
    /// </summary>
    StudioOperationResult RecoverDraft(DraftRecoveryEntry entry);

    /// <summary>
    ///     Discards a stored crash recovery draft from disk.
    /// </summary>
    StudioOperationResult DiscardDraft(DraftRecoveryEntry entry);

    /// <summary>
    ///     Discards any stored unsaved draft snapshot for <paramref name="absoluteFilePath"/>.
    /// </summary>
    StudioOperationResult DiscardDraft(string? absoluteFilePath);

    /// <summary>
    ///     Discards every stored unsaved draft snapshot from the draft store.
    /// </summary>
    StudioOperationResult DiscardAllDrafts();

    /// <summary>
    ///     Deletes a standalone asset file or surgically removes one entry from a multi-entry asset file.
    /// </summary>
    StudioOperationResult DeleteAsset(
        AssetKind kind,
        string assetId,
        string? absoluteFilePath,
        int tableIndex,
        int entryIndex);

    /// <summary>
    ///     Reloads the currently open workspace from disk and returns a refreshed snapshot.
    /// </summary>
    StudioWorkspaceSnapshot? ReloadCurrentWorkspaceFromDisk();

    /// <summary>
    ///     Gets the effective persisted default namespace for <paramref name="rootPath"/>.
    /// </summary>
    string GetWorkspaceDefaultNamespace(string? rootPath);

    /// <summary>
    ///     Persists a default namespace override for <paramref name="rootPath"/>.
    /// </summary>
    void SetWorkspaceDefaultNamespace(string? rootPath, string defaultNamespace);

    /// <summary>
    ///     Returns existing workspace directories to monitor for external filesystem changes.
    /// </summary>
    IReadOnlyList<string> GetWatchedDirectories();

    /// <summary>
    ///     Returns whether <paramref name="filePath"/> was written recently by Asset Studio itself.
    /// </summary>
    bool WasRecentlySavedByStudio(string filePath);

    /// <summary>
    ///     Re-runs workspace validation and returns fresh findings plus plain-text output.
    /// </summary>
    StudioValidationSyncResult RevalidateWorkspace();

    /// <summary>
    ///     Rewrites case-only path mismatches in writable files to match exact disk casing.
    /// </summary>
    StudioOperationResult FixCaseIssues();

    /// <summary>
    ///     Applies house TOML formatting to the specified file path.
    /// </summary>
    StudioOperationResult FormatDocument(string filePath);

    /// <summary>
    ///     Applies the deterministic automated fix associated with a single validation finding.
    /// </summary>
    Result<bool> ApplyValidationFix(ValidationFinding finding);

    /// <summary>
    ///     Applies all deterministic, non-destructive validation fixes marked safe for bulk execution.
    /// </summary>
    Result<ToolsValidation.BulkFixResult> FixAllSafeIssues();

    /// <summary>
    ///     Formats validation findings as plain text suitable for clipboard copy.
    /// </summary>
    string FormatValidationPlainText(IReadOnlyList<ValidationFinding> fallbackFindings);

    /// <summary>
    ///     Computes cross-file affected spans before applying an explicit ID rename.
    /// </summary>
    IReadOnlyList<RenameAffectedSpan> PreviewRename(string currentId, string newId, AssetKind kind);

    /// <summary>
    ///     Applies a planned cross-file asset ID rename into workspace drafts as one undoable batch.
    /// </summary>
    StudioOperationResult ApplyRenameToDrafts(string currentId, string newId, AssetKind kind);

    /// <summary>
    ///     Scaffolds a new asset entry into a new or existing workspace TOML file.
    /// </summary>
    StudioOperationResult CreateAssetDocument(NewAssetRequest request);

    /// <summary>
    ///     Creates a new localization key in the specified target translation file.
    /// </summary>
    StudioOperationResult CreateLocalizationKey(string keyName, string targetRelativeFile, string initialText);

    /// <summary>
    ///     Executes a blueprint management action (duplicate, delete, rename, move, import, or export).
    /// </summary>
    StudioOperationResult ExecuteBlueprintAction(string action, string relativeId, string? argument = null);

    /// <summary>
    ///     Captures a self-contained clipboard payload for copying an asset without modifying disk state.
    /// </summary>
    Result<ToolsEditing.AssetClipboardPayload> CaptureAssetClipboard(
        AssetKind kind,
        string assetId,
        string? absoluteFilePath,
        int tableIndex);

    /// <summary>
    ///     Duplicates an existing asset using the next available conflict-free identifier.
    /// </summary>
    Result<ToolsEditing.AssetCloneResult> DuplicateAsset(
        AssetKind kind,
        string assetId,
        string? absoluteFilePath,
        int tableIndex);

    /// <summary>
    ///     Pastes a previously captured clipboard payload into a writable root or target file.
    /// </summary>
    Result<ToolsEditing.AssetCloneResult> PasteAsset(
        ToolsEditing.AssetClipboardPayload payload,
        string? targetFilePath = null);

    /// <summary>
    ///     Returns existing relative file paths in the target folder for a given asset kind.
    /// </summary>
    IReadOnlyList<string> GetExistingFilesForKind(AssetKind kind);

    /// <summary>
    ///     Returns the default localization file path for creating keys from a given asset kind.
    /// </summary>
    string GetDefaultLocalizationFile(AssetKind kind);

    /// <summary>
    ///     Returns all editable localization files in the workspace.
    /// </summary>
    IReadOnlyList<string> GetLocalizationFiles();

    /// <summary>
    ///     Returns ordered asset root directories for loading textures, meshes, and previews.
    /// </summary>
    IReadOnlyList<string> GetOrderedAssetRootDirectories();

    /// <summary>
    ///     Converts an absolute image file path into a texture-relative workspace path when inside the workspace.
    /// </summary>
    string ResolveImagePathForWorkspace(string absoluteImagePath);

    /// <summary>
    ///     Converts an absolute block texture image path into its bare block albedo layer name.
    /// </summary>
    string ResolveBlockLayerNameForWorkspace(string absoluteImagePath);

    /// <summary>
    ///     Converts an absolute mesh file path into a mesh-relative workspace path when inside the workspace.
    /// </summary>
    string ResolveMeshPathForWorkspace(string absoluteMeshPath);

    /// <summary>
    ///     Searches indexed workspace assets, resources, and commands using the backend search index.
    /// </summary>
    IReadOnlyList<CommandPaletteEntry> SearchCommandPalette(
        string query,
        string categoryFilter,
        string scopeFilter,
        int maxResults = 60);

    /// <summary>
    ///     Builds staged pack metadata TOML text for the export preview.
    /// </summary>
    string BuildStagedPackToml(string packId, string packNamespace, int contentVersion);

    /// <summary>
    ///     Executes a staged pack archive export.
    /// </summary>
    StudioOperationResult ExportPack(ExportPackRequest request);

    /// <summary>
    ///     Builds runtime environment diagnostics shown in the about dialog.
    /// </summary>
    AboutDiagnosticsInfo GetDiagnosticsInfo(WorkspaceMode mode, string rootPath, string? referenceRoot, string glVendorRenderer);

    /// <summary>
    ///     Creates a redacted diagnostics archive excluding content files.
    /// </summary>
    StudioOperationResult CopyDiagnosticsZip(AboutDiagnosticsInfo info);

    /// <summary>
    ///     Records a clean application shutdown in the draft store session marker.
    /// </summary>
    void MarkCleanShutdown();
}
