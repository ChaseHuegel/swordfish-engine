using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.AssetStudio.Services;
using ToolsEditing = WaywardBeyond.AssetTools.Editing;
using ToolsSearch = WaywardBeyond.AssetTools.Search;
using ToolsSettings = WaywardBeyond.AssetTools.Settings;
using ToolsStorage = WaywardBeyond.AssetTools.Storage;
using ToolsValidation = WaywardBeyond.AssetTools.Validation;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Root shell view model coordinating workspace state, navigation history, undo/redo, editor, preview, validation, and dialogs.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IStudioWorkspaceService _workspaceService;
    private readonly IPreviewSurfaceProvider _previewProvider;
    private readonly IWorkspacePathPicker? _pathPicker;
    private readonly List<AssetDocumentViewModel> _allDocuments = [];
    private readonly ToolsSearch.NavigationHistory _navigationHistory = new();
    private readonly StudioUndoStack _undoStack = new();
    private readonly WorkspaceFileWatcher _fileWatcher = new();
    private readonly List<ReferenceCandidate> _candidates = [];
    private readonly List<string> _availableShaders = [];
    private readonly List<BodyPreviewFrameItem> _bodyPreviewFrames = [];
    private readonly Dictionary<string, bool> _sectionExpandedMemory = new(StringComparer.Ordinal);
    private string _rootPath = "~/packs/my_colony";
    private string? _referenceRootPath = "/games/WaywardBeyond/install";
    private string _packId = "my_colony";
    private string _packNamespace = "colony";
    private string _defaultNamespace = "colony";
    private string _treeFilterText = string.Empty;
    private string? _lastSpritePickerDirectory;
    private string? _lastMeshPickerDirectory;
    private readonly StudioTransformClipboard _transformClipboard = new();
    private ToolsEditing.AssetClipboardPayload? _copiedAssetClipboard;
    private IReadOnlyList<(int Level, int Xp)>? _copiedSkillLevelCurve;
    private readonly Lock _toastTimerLock = new();
    private StudioFieldViewModel? _pendingLocTargetField;
    private Timer? _toastTimer;
    private long _toastGeneration;
    private double _lastPersistedUiScale = ToolsSettings.ToolSettings.DEFAULT_UI_SCALE;
    private bool _isInitializingSettings;
    private bool _isRebuildingBodyFrames;
    private bool _glContextReady;
    private bool _hasGlError;
    private bool _disposed;

    [ObservableProperty]
    private bool _isStartScreenVisible;

    [ObservableProperty]
    private WorkspaceMode _workspaceMode = WorkspaceMode.Pack;

    [ObservableProperty]
    private string _workspaceName = "my_colony";

    [ObservableProperty]
    private bool _isUninitializedFolder;

    [ObservableProperty]
    private int _contentVersion = 1;

    [ObservableProperty]
    private AssetDocumentViewModel? _selectedAsset;

    [ObservableProperty]
    private bool _isTomlViewActive;

    [ObservableProperty]
    private ToolsSettings.ToolTheme _themePreference = ToolsSettings.ToolTheme.System;

    [ObservableProperty]
    private double _uiScale = ToolsSettings.ToolSettings.DEFAULT_UI_SCALE;

    [ObservableProperty]
    private bool _isExplorerVisible = true;

    [ObservableProperty]
    private bool _isInspectorVisible = true;

    [ObservableProperty]
    private bool _isValidationDockVisible = true;

    [ObservableProperty]
    private BrickOrientation _previewOrientation = BrickOrientation.North;

    [ObservableProperty]
    private bool _isShapeGridMode;

    [ObservableProperty]
    private bool _isConnectedShowcaseMode;

    [ObservableProperty]
    private bool _isPreviewOrbiting = true;

    [ObservableProperty]
    private double _previewOrbitAngle;

    [ObservableProperty]
    private bool _useItemWorldModelPreset;

    [ObservableProperty]
    private MaterialPreviewShape _materialPreviewShape = MaterialPreviewShape.Sphere;

    [ObservableProperty]
    private bool _isLiveGlViewportActive;

    [ObservableProperty]
    private PreviewRenderDescriptor _activePreviewDescriptor;

    [ObservableProperty]
    private int _bodyPreviewFrameIndex;

    [ObservableProperty]
    private int _fatalCount;

    [ObservableProperty]
    private int _brokenCount;

    [ObservableProperty]
    private int _crossPlatformCount;

    [ObservableProperty]
    private int _styleCount;

    [ObservableProperty]
    private int _safeFixCount;

    [ObservableProperty]
    private ValidationSeverity? _activeValidationSeverityFilter;

    [ObservableProperty]
    private int _dirtyCount;

    [ObservableProperty]
    private string? _toastMessage;

    [ObservableProperty]
    private bool _hasPendingRecoveryDrafts;

    [ObservableProperty]
    private CommandPaletteViewModel? _activeCommandPalette;

    [ObservableProperty]
    private NewAssetDialogViewModel? _activeNewAssetDialog;

    [ObservableProperty]
    private RenamePreviewDialogViewModel? _activeRenameDialog;

    [ObservableProperty]
    private DeleteAssetDialogViewModel? _activeDeleteAssetDialog;

    [ObservableProperty]
    private DeleteRecentWorkspaceDialogViewModel? _activeDeleteRecentWorkspaceDialog;

    [ObservableProperty]
    private CreateLocalizationKeyDialogViewModel? _activeCreateLocKeyDialog;

    [ObservableProperty]
    private SettingsDialogViewModel? _activeSettingsDialog;

    [ObservableProperty]
    private AboutDialogViewModel? _activeAboutDialog;

    [ObservableProperty]
    private ExportDialogViewModel? _activeExportDialog;

    [ObservableProperty]
    private DraftRecoveryDialogViewModel? _activeDraftRecoveryDialog;

    [ObservableProperty]
    private ItemModelEditorViewModel? _activeModelEditor;

    /// <summary>
    ///     Initializes the main studio view model using the default sample services.
    /// </summary>
    public MainWindowViewModel()
        : this(new SampleStudioWorkspaceService(), new SamplePreviewSurfaceProvider(), startOnWelcomeScreen: false)
    {
    }

    /// <summary>
    ///     Initializes the main studio view model with explicit workspace and preview providers.
    /// </summary>
    public MainWindowViewModel(
        IStudioWorkspaceService workspaceService,
        IPreviewSurfaceProvider previewProvider,
        bool startOnWelcomeScreen = false,
        IWorkspacePathPicker? pathPicker = null,
        bool enableLiveGlViewport = false,
        string? initialWorkspacePath = null,
        string? initialReferenceInstallPath = null)
    {
        _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
        _previewProvider = previewProvider ?? throw new ArgumentNullException(nameof(previewProvider));
        _pathPicker = pathPicker;
        EnableLiveGlViewport = enableLiveGlViewport;

        _undoStack.StateChanged += HandleUndoStateChanged;
        _fileWatcher.FilesChanged += HandleExternalFilesChanged;

        StartScreen = new StartScreenViewModel(
            _workspaceService.GetRecentWorkspaces(),
            OpenWorkspace,
            _workspaceService,
            _pathPicker,
            ShowToast,
            RequestDeleteRecentWorkspace);
        _isStartScreenVisible = startOnWelcomeScreen;

        _isInitializingSettings = true;
        try
        {
            var settings = _workspaceService.GetSettings();
            _themePreference = settings.Theme;
            _uiScale = ToolsSettings.ToolSettings.ClampUiScale(settings.UiScale);
            _lastPersistedUiScale = _uiScale;
            ApplyThemeVariant(_themePreference);
        }
        finally
        {
            _isInitializingSettings = false;
        }

        if (!string.IsNullOrWhiteSpace(initialWorkspacePath))
        {
            var autoSnapshot = _workspaceService.TryOpenAuto(initialWorkspacePath, initialReferenceInstallPath);
            if (autoSnapshot != null)
            {
                ApplyWorkspaceSnapshot(autoSnapshot, showWorkbench: true);
                return;
            }
        }

        if (!_workspaceService.UsesRealBackend)
        {
            OpenWorkspace(WorkspaceMode.Pack, "~/packs/my_colony", "my_colony", showWorkbench: !startOnWelcomeScreen);
        }
        else
        {
            UpdatePreviewDescriptor();
        }
    }

    /// <summary>
    ///     Raised when the live <see cref="GlViewport"/> should synchronize its scene meshes or camera state.
    /// </summary>
    public event Action? PreviewSceneSyncRequested;

    /// <summary>
    ///     Raised when the live <see cref="GlViewport"/> should reset its orbit camera.
    /// </summary>
    public event Action? PreviewCameraResetRequested;

    /// <summary>
    ///     Raised whenever selection changes so the workspace explorer scrolls the active row into view.
    /// </summary>
    public event Action<AssetDocumentViewModel>? SelectionScrollRequested;

    /// <summary>
    ///     Auto-dismiss timeout for transient toast notifications.
    /// </summary>
    public TimeSpan ToastAutoDismissDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Application-wide reversible edit history stack.
    /// </summary>
    public StudioUndoStack UndoStack => _undoStack;

    /// <summary>
    ///     Whether the live OpenGL preview viewport should be mounted on desktop runs.
    /// </summary>
    public bool EnableLiveGlViewport { get; }

    /// <summary>
    ///     Whether the OpenGL viewport control should be rendered in the visual tree for the current asset.
    /// </summary>
    public bool ShouldMountGlViewport => EnableLiveGlViewport && !_hasGlError && Has3DPreviewControls && SupportsLiveGlMode;

    /// <summary>
    ///     Whether the fallback software preview canvas should be displayed over the OpenGL viewport.
    /// </summary>
    public bool ShowFallbackPreviewViewport => !IsLiveGlViewportActive;

    /// <summary>
    ///     Most recent plain-text validation report formatted by Copy All.
    /// </summary>
    public string? LastCopiedValidationText { get; private set; }

    /// <summary>
    ///     Start screen view model showing recent workspaces and open/create entry cards.
    /// </summary>
    public StartScreenViewModel StartScreen { get; }

    /// <summary>
    ///     Grouped asset tree shown in the left workspace explorer.
    /// </summary>
    public ObservableCollection<AssetTreeGroupViewModel> TreeGroups { get; } = [];

    /// <summary>
    ///     Validation findings grouped by Severity, then Root, then File.
    /// </summary>
    public ObservableCollection<ValidationSeverityGroupViewModel> ValidationGroups { get; } = [];

    /// <summary>
    ///     Raw flat validation findings list used for status counts and copy-all output.
    /// </summary>
    public ObservableCollection<ValidationFinding> AllFindings { get; } = [];

    /// <summary>
    ///     Unclean-exit drafts available for recovery.
    /// </summary>
    public ObservableCollection<DraftRecoveryEntry> PendingRecoveryDrafts { get; } = [];

    /// <summary>
    ///     Image files discovered recursively across enabled workspace roots for sprite pickers.
    /// </summary>
    public ObservableCollection<WorkspaceImageEntry> WorkspaceImages { get; } = [];

    /// <summary>
    ///     Whether an asset document is currently selected in the editor.
    /// </summary>
    public bool HasSelectedAsset => SelectedAsset != null;

    /// <summary>
    ///     Whether no asset document is currently selected in the editor.
    /// </summary>
    public bool HasNoSelectedAsset => SelectedAsset == null;

    /// <summary>
    ///     Whether the workspace explorer tree currently has no visible groups to display.
    /// </summary>
    public bool ShowExplorerEmptyHint => TreeGroups.Count == 0;

    /// <summary>
    ///     Contextual empty-state hint shown inside the left workspace explorer when no groups are visible.
    /// </summary>
    public string ExplorerEmptyHintText => IsUninitializedFolder && string.IsNullOrWhiteSpace(TreeFilterText)
        ? "Initialize this folder as a pack to start adding assets."
        : (string.IsNullOrWhiteSpace(TreeFilterText)
            ? "No assets yet.\nClick + New to create one."
            : "No matching assets.");

    /// <summary>
    ///     Root directory path of the currently opened workspace.
    /// </summary>
    public string CurrentWorkspaceRootPath => _rootPath;

    /// <summary>
    ///     Read-only list of all loaded asset documents in the open workspace.
    /// </summary>
    public IReadOnlyList<AssetDocumentViewModel> AllDocuments => _allDocuments;

    /// <summary>
    ///     Ordered directional sprite frames available for the currently selected body document.
    /// </summary>
    public IReadOnlyList<BodyPreviewFrameItem> BodyPreviewFrames => _bodyPreviewFrames;

    /// <summary>
    ///     Search text filtering the workspace explorer tree, defensively coerced to non-null text.
    /// </summary>
    public string TreeFilterText
    {
        get => _treeFilterText;
        set
        {
            var safe = value ?? string.Empty;
            if (SetProperty(ref _treeFilterText, safe))
            {
                RebuildAssetTree();
            }
        }
    }

    /// <summary>
    ///     Active per-workspace default namespace prefix for newly authored assets.
    /// </summary>
    public string DefaultNamespace => _defaultNamespace;

    /// <summary>
    ///     Whether the main workbench layout is displayed.
    /// </summary>
    public bool IsWorkbenchVisible => !IsStartScreenVisible;

    /// <summary>
    ///     Whether the form editor view is currently displayed.
    /// </summary>
    public bool IsFormViewActive => !IsTomlViewActive;

    /// <summary>
    ///     Display text for the workspace mode pill in the top command bar.
    /// </summary>
    public string WorkspaceModeLabel => IsUninitializedFolder
        ? $"Folder: {WorkspaceName}"
        : (WorkspaceMode == WorkspaceMode.Pack
            ? $"Pack: {WorkspaceName}"
            : $"Repo: {WorkspaceName}");

    /// <summary>
    ///     Formatted percentage label for the bottom status bar zoom reset button.
    /// </summary>
    public string UiScalePercentLabel => $"{Math.Round(UiScale * 100):0}%";

    /// <summary>
    ///     Whether the back navigation button is enabled.
    /// </summary>
    public bool CanNavigateBack => _navigationHistory.CanGoBack;

    /// <summary>
    ///     Whether the forward navigation button is enabled.
    /// </summary>
    public bool CanNavigateForward => _navigationHistory.CanGoForward;

    /// <summary>
    ///     Whether an edit can currently be undone.
    /// </summary>
    public bool CanUndo => _undoStack.CanUndo;

    /// <summary>
    ///     Whether an undone edit can currently be redone.
    /// </summary>
    public bool CanRedo => _undoStack.CanRedo;

    /// <summary>
    ///     Dynamic tooltip for the top bar Undo button.
    /// </summary>
    public string UndoTooltip => _undoStack.CanUndo
        ? $"Undo {_undoStack.UndoDescription} (Ctrl+Z)"
        : "Undo (Ctrl+Z)";

    /// <summary>
    ///     Dynamic tooltip for the top bar Redo button.
    /// </summary>
    public string RedoTooltip => _undoStack.CanRedo
        ? $"Redo {_undoStack.RedoDescription} (Ctrl+Y)"
        : "Redo (Ctrl+Y)";

    /// <summary>
    ///     Primary save button label displaying unsaved draft count.
    /// </summary>
    public string SaveAllButtonLabel => DirtyCount > 0 ? $"Save All ({DirtyCount})" : "Saved";

    /// <summary>
    ///     Whether there are unsaved dirty documents.
    /// </summary>
    public bool HasDirtyDocuments => DirtyCount > 0;

    /// <summary>
    ///     Whether the currently selected asset has unsaved changes that can be saved individually.
    /// </summary>
    public bool CanSaveCurrentAsset => SelectedAsset != null && SelectedAsset.IsDirty;

    /// <summary>
    ///     Whether the currently selected asset has unsaved changes that can be discarded.
    /// </summary>
    public bool CanDiscardCurrentAsset => SelectedAsset != null && SelectedAsset.IsDirty;

    /// <summary>
    ///     Draft sync indicator shown in the bottom status bar.
    /// </summary>
    public string DraftStatusLabel => DirtyCount > 0 ? $"{DirtyCount} draft(s) synced" : "All drafts saved";

    /// <summary>
    ///     Summary counts string for the status bar.
    /// </summary>
    public string StatusSummaryLabel => $"{FatalCount} Fatal · {BrokenCount} Broken · {CrossPlatformCount} Cross-platform · {StyleCount} Style";

    /// <summary>
    ///     Whether workspace validation currently has zero findings overall.
    /// </summary>
    public bool HasNoValidationFindings => AllFindings.Count == 0;

    /// <summary>
    ///     Whether the Fatal validation severity filter is currently active.
    /// </summary>
    public bool IsFatalFilterActive => ActiveValidationSeverityFilter == ValidationSeverity.Fatal;

    /// <summary>
    ///     Whether the Broken validation severity filter is currently active.
    /// </summary>
    public bool IsBrokenFilterActive => ActiveValidationSeverityFilter == ValidationSeverity.Broken;

    /// <summary>
    ///     Whether the Cross-platform validation severity filter is currently active.
    /// </summary>
    public bool IsCrossPlatformFilterActive => ActiveValidationSeverityFilter == ValidationSeverity.CrossPlatform;

    /// <summary>
    ///     Whether the Style validation severity filter is currently active.
    /// </summary>
    public bool IsStyleFilterActive => ActiveValidationSeverityFilter == ValidationSeverity.Style;

    /// <summary>
    ///     Whether any validation severity filter pill is currently toggled on.
    /// </summary>
    public bool HasActiveValidationFilter => ActiveValidationSeverityFilter.HasValue;

    /// <summary>
    ///     Whether the validation dock should render its empty-state message.
    /// </summary>
    public bool ShowValidationEmptyState => ValidationGroups.Count == 0;

    /// <summary>
    ///     Whether the clean green badge should appear alongside the validation empty-state text.
    /// </summary>
    public bool ShowValidationCleanBadge => AllFindings.Count == 0;

    /// <summary>
    ///     Calm plain-language message displayed when no validation findings match the current view.
    /// </summary>
    public string ValidationEmptyStateMessage => HasActiveValidationFilter
        ? $"No {FormatSeverityLabel(ActiveValidationSeverityFilter!.Value)} findings in workspace."
        : "All workspace checks passed — 0 fatal, 0 broken, 0 cross-platform, and 0 style findings.";

    /// <summary>
    ///     Compact orientation code label for the preview toolbar.
    /// </summary>
    public string OrientationShortLabel => PreviewOrientation switch
    {
        BrickOrientation.North => "N",
        BrickOrientation.East => "E",
        BrickOrientation.South => "S",
        BrickOrientation.West => "W",
        _ => "N",
    };

    /// <summary>
    ///     Whether the full-workbench visual ViewModel/WorldModel editor is currently active.
    /// </summary>
    public bool IsModelEditorVisible => ActiveModelEditor != null;

    /// <summary>
    ///     Whether the standard 3-column workbench and validation dock are visible instead of the visual model editor.
    /// </summary>
    public bool IsStandardWorkbenchVisible => ActiveModelEditor == null;

    /// <summary>
    ///     Whether any modal overlay or dialog is currently open.
    /// </summary>
    public bool IsAnyOverlayOpen =>
        ActiveCommandPalette != null
        || ActiveNewAssetDialog != null
        || ActiveRenameDialog != null
        || ActiveDeleteAssetDialog != null
        || ActiveDeleteRecentWorkspaceDialog != null
        || ActiveCreateLocKeyDialog != null
        || ActiveSettingsDialog != null
        || ActiveAboutDialog != null
        || ActiveExportDialog != null
        || ActiveDraftRecoveryDialog != null;

    /// <summary>
    ///     Whether a toast banner is visible.
    /// </summary>
    public bool HasToast => !string.IsNullOrWhiteSpace(ToastMessage);

    /// <summary>
    ///     Whether the shape grid toggle button is applicable to the selected asset.
    /// </summary>
    public bool ShowShapeGridToggle => SelectedAsset != null && SelectedAsset.Kind == AssetKind.Brick;

    /// <summary>
    ///     Whether the connected-texture showcase toggle is applicable to the selected brick asset.
    /// </summary>
    public bool ShowConnectedShowcaseToggle => SelectedAsset != null
        && SelectedAsset.Kind == AssetKind.Brick
        && SelectedAsset.IsBrickConnected;

    /// <summary>
    ///     Whether the standard brick shape preview mode is active instead of the connected showcase.
    /// </summary>
    public bool IsShapePreviewMode => !IsConnectedShowcaseMode;

    /// <summary>
    ///     Whether the item framing preset toggle is applicable to the selected asset.
    /// </summary>
    public bool ShowItemFramingToggle => SelectedAsset != null && SelectedAsset.Kind == AssetKind.Item;

    /// <summary>
    ///     Label for the active item framing preset button.
    /// </summary>
    public string ItemFramingLabel => UseItemWorldModelPreset ? "WorldModel" : "ViewModel";

    /// <summary>
    ///     Whether the active item framing preset is first-person ViewModel.
    /// </summary>
    public bool UseItemViewModelPreset => !UseItemWorldModelPreset;

    /// <summary>
    ///     Total number of directional frames available for the selected body asset.
    /// </summary>
    public int BodyPreviewFrameCount => _bodyPreviewFrames.Count;

    /// <summary>
    ///     Maximum zero-based slider index for cycling body preview frames.
    /// </summary>
    public int BodyPreviewMaxIndex => Math.Max(0, _bodyPreviewFrames.Count - 1);

    /// <summary>
    ///     Whether the body sprite frame slider and stepper strip should be shown below the preview viewport.
    /// </summary>
    public bool ShowBodyPreviewControls => SelectedAsset != null
        && SelectedAsset.Kind == AssetKind.Body
        && _bodyPreviewFrames.Count > 0;

    /// <summary>
    ///     Whether more than one body sprite frame exists to step between.
    /// </summary>
    public bool CanStepBodyPreviewFrames => _bodyPreviewFrames.Count > 1;

    /// <summary>
    ///     Formatted state, direction, and index caption for the active body preview frame.
    /// </summary>
    public string BodyPreviewLabel
    {
        get
        {
            if (_bodyPreviewFrames.Count == 0)
            {
                return string.Empty;
            }

            var idx = Math.Clamp(BodyPreviewFrameIndex, 0, _bodyPreviewFrames.Count - 1);
            var frame = _bodyPreviewFrames[idx];
            return $"{frame.State} · {frame.Direction} ({idx + 1}/{_bodyPreviewFrames.Count})";
        }
    }

    /// <summary>
    ///     Whether the active asset is a non-3D data document showing the metadata summary card.
    /// </summary>
    public bool IsDataDocumentPreview => HasSelectedAsset && ActivePreviewDescriptor.Mode == PreviewSurfaceMode.DataDocument;

    /// <summary>
    ///     Whether the active asset is a recipe showing the interactive crafting flow preview.
    /// </summary>
    public bool IsRecipePreview => HasSelectedAsset && ActivePreviewDescriptor.Mode == PreviewSurfaceMode.RecipeFlow;

    /// <summary>
    ///     Whether the right inspector renders a graphical preview viewport.
    /// </summary>
    public bool HasVisualPreview => HasSelectedAsset && !IsDataDocumentPreview && !IsRecipePreview;

    /// <summary>
    ///     Whether the 3D viewport camera buttons apply to the active preview surface.
    /// </summary>
    public bool Has3DPreviewControls => HasSelectedAsset
        && ActivePreviewDescriptor.Mode != PreviewSurfaceMode.DataDocument
        && ActivePreviewDescriptor.Mode != PreviewSurfaceMode.RecipeFlow
        && ActivePreviewDescriptor.Mode != PreviewSurfaceMode.PaintSwatch
        && ActivePreviewDescriptor.Mode != PreviewSurfaceMode.BodySprite;

    /// <summary>
    ///     Whether the brick/item orientation button applies to the active preview surface.
    /// </summary>
    public bool ShowOrientationButton => Has3DPreviewControls
        && SelectedAsset?.Kind is not (AssetKind.Material or AssetKind.Blueprint);

    /// <summary>
    ///     Whether the currently selected asset uses the 3D Material preview switcher.
    /// </summary>
    public bool IsMaterialPreviewSelected => HasSelectedAsset && SelectedAsset?.Kind == AssetKind.Material;

    /// <summary>
    ///     Whether the Material preview shape is currently <see cref="MaterialPreviewShape.Sphere"/>.
    /// </summary>
    public bool IsMaterialShapeSphere => MaterialPreviewShape == MaterialPreviewShape.Sphere;

    /// <summary>
    ///     Whether the Material preview shape is currently <see cref="MaterialPreviewShape.Plane"/>.
    /// </summary>
    public bool IsMaterialShapePlane => MaterialPreviewShape == MaterialPreviewShape.Plane;

    /// <summary>
    ///     Whether the Material preview shape is currently <see cref="MaterialPreviewShape.Cube"/>.
    /// </summary>
    public bool IsMaterialShapeCube => MaterialPreviewShape == MaterialPreviewShape.Cube;

    /// <summary>
    ///     Gets the most recently copied asset clipboard payload, or null when empty.
    /// </summary>
    public ToolsEditing.AssetClipboardPayload? CopiedAssetClipboard => _copiedAssetClipboard;

    /// <summary>
    ///     Whether an asset payload is available to paste into the workspace explorer.
    /// </summary>
    public bool CanPasteAsset => _copiedAssetClipboard.HasValue && _allDocuments.Count > 0;

    /// <summary>
    ///     Gets the most recently copied skill level curve rows, or null when empty.
    /// </summary>
    public IReadOnlyList<(int Level, int Xp)>? CopiedSkillLevelCurve => _copiedSkillLevelCurve;

    /// <summary>
    ///     Whether a copied skill curve can be pasted onto the currently selected skill document.
    /// </summary>
    public bool CanPasteSkillLevelCurve => _copiedSkillLevelCurve != null
        && _copiedSkillLevelCurve.Count > 0
        && SelectedAsset?.Kind == AssetKind.Skill
        && !SelectedAsset.IsReadOnly;

    /// <summary>
    ///     Whether at least one validation finding has a safe automatic bulk fix available.
    /// </summary>
    public bool HasSafeFixes => SafeFixCount > 0;

    /// <summary>
    ///     Dynamic button label for the bulk safe-fix toolbar action.
    /// </summary>
    public string SafeFixButtonLabel => SafeFixCount > 0
        ? $"Fix All Safe ({SafeFixCount})"
        : "Fix All Safe";

    /// <summary>
    ///     Whether the System theme preference is active.
    /// </summary>
    public bool IsSystemTheme => ThemePreference == ToolsSettings.ToolTheme.System;

    /// <summary>
    ///     Whether the Light theme preference is active.
    /// </summary>
    public bool IsLightTheme => ThemePreference == ToolsSettings.ToolTheme.Light;

    /// <summary>
    ///     Gets or sets whether the explicit Dark theme preference is active.
    /// </summary>
    public bool IsDarkTheme
    {
        get => ThemePreference == ToolsSettings.ToolTheme.Dark;
        set => ThemePreference = value ? ToolsSettings.ToolTheme.Dark : ToolsSettings.ToolTheme.Light;
    }

    private bool SupportsLiveGlMode => ActivePreviewDescriptor.Mode is PreviewSurfaceMode.SingleBrick
        or PreviewSurfaceMode.BrickShapeGrid
        or PreviewSurfaceMode.BrickConnectedShowcase
        or PreviewSurfaceMode.ItemViewModel
        or PreviewSurfaceMode.ItemWorldModel
        or PreviewSurfaceMode.MaterialPreview
        or PreviewSurfaceMode.BlueprintVoxel;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeActiveModelEditor();
        StopToastTimer();
        _undoStack.StateChanged -= HandleUndoStateChanged;
        _fileWatcher.FilesChanged -= HandleExternalFilesChanged;
        _fileWatcher.Dispose();
    }

    partial void OnIsStartScreenVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsWorkbenchVisible));
    }

    partial void OnIsTomlViewActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(IsFormViewActive));
    }

    partial void OnWorkspaceModeChanged(WorkspaceMode value)
    {
        OnPropertyChanged(nameof(WorkspaceModeLabel));
    }

    partial void OnWorkspaceNameChanged(string value)
    {
        OnPropertyChanged(nameof(WorkspaceModeLabel));
    }

    partial void OnIsUninitializedFolderChanged(bool value)
    {
        OnPropertyChanged(nameof(WorkspaceModeLabel));
        OnPropertyChanged(nameof(ExplorerEmptyHintText));
    }

    partial void OnDirtyCountChanged(int value)
    {
        OnPropertyChanged(nameof(SaveAllButtonLabel));
        OnPropertyChanged(nameof(HasDirtyDocuments));
        OnPropertyChanged(nameof(DraftStatusLabel));
        OnPropertyChanged(nameof(CanSaveCurrentAsset));
        OnPropertyChanged(nameof(CanDiscardCurrentAsset));
    }

    partial void OnPreviewOrientationChanged(BrickOrientation value)
    {
        OnPropertyChanged(nameof(OrientationShortLabel));
        UpdatePreviewDescriptor();
    }

    partial void OnIsShapeGridModeChanged(bool value)
    {
        if (value && IsConnectedShowcaseMode)
        {
            IsConnectedShowcaseMode = false;
        }

        UpdatePreviewDescriptor();
    }

    partial void OnIsConnectedShowcaseModeChanged(bool value)
    {
        if (value && IsShapeGridMode)
        {
            IsShapeGridMode = false;
        }

        OnPropertyChanged(nameof(IsShapePreviewMode));
        UpdatePreviewDescriptor();
    }

    partial void OnIsPreviewOrbitingChanged(bool value) => UpdatePreviewDescriptor();

    partial void OnPreviewOrbitAngleChanged(double value) => UpdatePreviewDescriptor();

    partial void OnUseItemWorldModelPresetChanged(bool value)
    {
        OnPropertyChanged(nameof(ItemFramingLabel));
        OnPropertyChanged(nameof(UseItemViewModelPreset));
        UpdatePreviewDescriptor();
    }

    partial void OnMaterialPreviewShapeChanged(MaterialPreviewShape value)
    {
        OnPropertyChanged(nameof(IsMaterialShapeSphere));
        OnPropertyChanged(nameof(IsMaterialShapePlane));
        OnPropertyChanged(nameof(IsMaterialShapeCube));
        UpdatePreviewDescriptor();
    }

    partial void OnIsLiveGlViewportActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowFallbackPreviewViewport));
    }

    partial void OnThemePreferenceChanged(ToolsSettings.ToolTheme value)
    {
        ApplyThemeVariant(value);
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));

        if (!_isInitializingSettings)
        {
            PersistQuickSettings();
        }
    }

    partial void OnUiScaleChanged(double value)
    {
        var clamped = Math.Round(ToolsSettings.ToolSettings.ClampUiScale(value), 2);
        if (Math.Abs(clamped - value) > 0.0001)
        {
            UiScale = clamped;
            return;
        }

        OnPropertyChanged(nameof(UiScalePercentLabel));

        if (!_isInitializingSettings && Math.Abs(clamped - _lastPersistedUiScale) >= 0.005)
        {
            _lastPersistedUiScale = clamped;
            PersistQuickSettings();
        }
    }

    partial void OnBodyPreviewFrameIndexChanged(int value)
    {
        if (_isRebuildingBodyFrames)
        {
            return;
        }

        if (_bodyPreviewFrames.Count > 0)
        {
            var clamped = Math.Clamp(value, 0, _bodyPreviewFrames.Count - 1);
            if (clamped != value)
            {
                BodyPreviewFrameIndex = clamped;
                return;
            }
        }

        OnPropertyChanged(nameof(BodyPreviewLabel));
        UpdatePreviewDescriptor();
    }

    partial void OnActiveValidationSeverityFilterChanged(ValidationSeverity? value)
    {
        OnPropertyChanged(nameof(IsFatalFilterActive));
        OnPropertyChanged(nameof(IsBrokenFilterActive));
        OnPropertyChanged(nameof(IsCrossPlatformFilterActive));
        OnPropertyChanged(nameof(IsStyleFilterActive));
        OnPropertyChanged(nameof(HasActiveValidationFilter));
        OnPropertyChanged(nameof(ValidationEmptyStateMessage));
        RebuildValidationGroups();
    }

    partial void OnToastMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasToast));
        if (string.IsNullOrWhiteSpace(value))
        {
            StopToastTimer();
        }
    }

    partial void OnSelectedAssetChanged(AssetDocumentViewModel? value)
    {
        if (ActiveModelEditor != null && !ReferenceEquals(ActiveModelEditor.Document, value))
        {
            DisposeActiveModelEditor();
        }

        if (value != null && value.HasSyntaxError)
        {
            IsTomlViewActive = true;
        }

        if (value == null || value.Kind != AssetKind.Brick || !value.IsBrickConnected)
        {
            IsConnectedShowcaseMode = false;
        }

        OnPropertyChanged(nameof(HasSelectedAsset));
        OnPropertyChanged(nameof(HasNoSelectedAsset));
        OnPropertyChanged(nameof(HasVisualPreview));
        OnPropertyChanged(nameof(IsDataDocumentPreview));
        OnPropertyChanged(nameof(IsRecipePreview));
        OnPropertyChanged(nameof(Has3DPreviewControls));
        OnPropertyChanged(nameof(ShowOrientationButton));
        OnPropertyChanged(nameof(IsMaterialPreviewSelected));
        OnPropertyChanged(nameof(ShouldMountGlViewport));
        OnPropertyChanged(nameof(ShowShapeGridToggle));
        OnPropertyChanged(nameof(ShowConnectedShowcaseToggle));
        OnPropertyChanged(nameof(ShowItemFramingToggle));
        OnPropertyChanged(nameof(CanSaveCurrentAsset));
        OnPropertyChanged(nameof(CanDiscardCurrentAsset));
        OnPropertyChanged(nameof(CanPasteSkillLevelCurve));
        OnPropertyChanged(nameof(CanPasteAsset));

        RebuildBodyPreviewFrames(syncToSelectedState: true);
        UpdatePreviewDescriptor();
    }

    partial void OnActiveCommandPaletteChanged(CommandPaletteViewModel? value) => OnPropertyChanged(nameof(IsAnyOverlayOpen));

    partial void OnActiveNewAssetDialogChanged(NewAssetDialogViewModel? value) => OnPropertyChanged(nameof(IsAnyOverlayOpen));

    partial void OnActiveRenameDialogChanged(RenamePreviewDialogViewModel? value) => OnPropertyChanged(nameof(IsAnyOverlayOpen));

    partial void OnActiveDeleteAssetDialogChanged(DeleteAssetDialogViewModel? value) => OnPropertyChanged(nameof(IsAnyOverlayOpen));

    partial void OnActiveDeleteRecentWorkspaceDialogChanged(DeleteRecentWorkspaceDialogViewModel? value) => OnPropertyChanged(nameof(IsAnyOverlayOpen));

    partial void OnActiveCreateLocKeyDialogChanged(CreateLocalizationKeyDialogViewModel? value) => OnPropertyChanged(nameof(IsAnyOverlayOpen));

    partial void OnActiveSettingsDialogChanged(SettingsDialogViewModel? value) => OnPropertyChanged(nameof(IsAnyOverlayOpen));

    partial void OnActiveAboutDialogChanged(AboutDialogViewModel? value) => OnPropertyChanged(nameof(IsAnyOverlayOpen));

    partial void OnActiveExportDialogChanged(ExportDialogViewModel? value) => OnPropertyChanged(nameof(IsAnyOverlayOpen));

    partial void OnActiveDraftRecoveryDialogChanged(DraftRecoveryDialogViewModel? value) => OnPropertyChanged(nameof(IsAnyOverlayOpen));

    partial void OnActiveModelEditorChanged(ItemModelEditorViewModel? value)
    {
        OnPropertyChanged(nameof(IsModelEditorVisible));
        OnPropertyChanged(nameof(IsStandardWorkbenchVisible));
    }

    /// <summary>
    ///     Records successful initialization of the live <see cref="GlViewport"/> and updates driver telemetry.
    /// </summary>
    public void HandleGlViewportInitialized(GlInfo info)
    {
        _glContextReady = true;
        _hasGlError = false;
        _previewProvider.ReportLiveGlInfo(info.Vendor, info.Renderer, info.Version, info.IsOpenGlEs);
        OnPropertyChanged(nameof(ShouldMountGlViewport));
        PreviewSceneSyncRequested?.Invoke();
    }

    /// <summary>
    ///     Records a non-fatal <see cref="GlViewport"/> failure and seamlessly switches to the 2D fallback surface.
    /// </summary>
    public void HandleGlViewportError(string errorMessage)
    {
        _glContextReady = false;
        _hasGlError = true;
        IsLiveGlViewportActive = false;
        _previewProvider.ReportGlFallback(errorMessage);
        OnPropertyChanged(nameof(ShouldMountGlViewport));
    }

    /// <summary>
    ///     Synchronizes the active asset scene into <paramref name="viewport"/> when live OpenGL rendering is available.
    /// </summary>
    public void SyncLiveGlViewport(GlViewport viewport)
    {
        if (!ShouldMountGlViewport || !_glContextReady)
        {
            viewport.SetAutoOrbit(false);
            IsLiveGlViewportActive = false;
            return;
        }

        if (_previewProvider is StudioGlPreviewController glController)
        {
            if (_workspaceService is AssetToolsStudioWorkspaceService realService && realService.CurrentWorkspace != null)
            {
                glController.BindWorkspace(realService.CurrentWorkspace, viewport);
            }

            IsLiveGlViewportActive = glController.TrySyncViewportScene(
                viewport,
                SelectedAsset,
                PreviewOrientation,
                IsShapeGridMode,
                UseItemWorldModelPreset,
                IsConnectedShowcaseMode,
                materialShape: ActivePreviewDescriptor.MaterialShape);
            viewport.SetAutoOrbit(IsPreviewOrbiting && IsLiveGlViewportActive);
        }
        else
        {
            viewport.SetAutoOrbit(false);
            IsLiveGlViewportActive = false;
        }
    }

    /// <summary>
    ///     Returns whether a collapsible section for the given kind should start expanded.
    /// </summary>
    public bool GetSectionExpanded(AssetKind kind, string sectionKey, bool defaultExpanded = true)
    {
        var key = $"{kind}:{sectionKey}";
        return _sectionExpandedMemory.TryGetValue(key, out var expanded) ? expanded : defaultExpanded;
    }

    /// <summary>
    ///     Remembers collapsed or expanded section state per asset kind.
    /// </summary>
    public void SetSectionExpanded(AssetKind kind, string sectionKey, bool isExpanded)
    {
        _sectionExpandedMemory[$"{kind}:{sectionKey}"] = isExpanded;
    }

    /// <summary>
    ///     Opens a workspace in the requested mode and refreshes tree, validation, and preview state.
    /// </summary>
    public void OpenWorkspace(WorkspaceMode mode, string rootPath, string? displayName = null)
    {
        OpenWorkspace(mode, rootPath, displayName, showWorkbench: true);
    }

    /// <summary>
    ///     Selects an exact document instance, optionally pushing navigation history and highlighting a field.
    /// </summary>
    public bool SelectDocument(AssetDocumentViewModel target, bool pushHistory = true, string? highlightFieldKey = null)
    {
        if (target == null)
        {
            return false;
        }

        for (var i = 0; i < _allDocuments.Count; i++)
        {
            _allDocuments[i].IsSelected = ReferenceEquals(_allDocuments[i], target);
        }

        SelectedAsset = target;
        target.HighlightField(highlightFieldKey);

        if (!target.HasSyntaxError && highlightFieldKey != null)
        {
            IsTomlViewActive = false;
        }

        if (target.Kind == AssetKind.Brick && target.BrickShapeField != null)
        {
            IsShapeGridMode = string.Equals(target.BrickShapeField.Value, "Any", StringComparison.OrdinalIgnoreCase);
        }

        if (pushHistory)
        {
            var navTarget = new ToolsSearch.NavigationTarget(
                Key: target.DocumentKey,
                FilePath: target.RelativeFile,
                Line: 1,
                FieldKey: highlightFieldKey);
            _navigationHistory.Navigate(in navTarget);
            OnPropertyChanged(nameof(CanNavigateBack));
            OnPropertyChanged(nameof(CanNavigateForward));
        }

        SelectionScrollRequested?.Invoke(target);
        UpdatePreviewDescriptor();
        return true;
    }

    /// <summary>
    ///     Selects an asset matching optional kind, identifier or document key, and optional relative file path.
    /// </summary>
    public bool SelectAsset(
        AssetKind? kind,
        string assetId,
        string? relativeFile = null,
        bool pushHistory = true,
        string? highlightFieldKey = null)
    {
        var target = FindDocument(kind, assetId, relativeFile);
        return target != null && SelectDocument(target, pushHistory, highlightFieldKey);
    }

    /// <summary>
    ///     Selects an asset by ID or document key, optionally pushing navigation history and highlighting a field.
    /// </summary>
    public bool SelectAssetById(string assetId, bool pushHistory = true, string? highlightFieldKey = null)
    {
        return SelectAsset(null, assetId, null, pushHistory, highlightFieldKey);
    }

    /// <summary>
    ///     Processes external disk changes on the active workspace without overwriting unsaved dirty drafts.
    /// </summary>
    public void ReloadExternalChangesFromDisk()
    {
        ApplyExternalDiskReload();
    }

    /// <summary>
    ///     Expands all validation file groups so findings are immediately inspectable in tests or captures.
    /// </summary>
    public void ExpandAllValidationFiles()
    {
        for (var s = 0; s < ValidationGroups.Count; s++)
        {
            var sev = ValidationGroups[s];
            sev.IsExpanded = true;
            for (var r = 0; r < sev.Roots.Count; r++)
            {
                var root = sev.Roots[r];
                root.IsExpanded = true;
                for (var f = 0; f < root.Files.Count; f++)
                {
                    root.Files[f].IsExpanded = true;
                }
            }
        }
    }

    /// <summary>
    ///     Applies the requested Avalonia theme variant (System, Light, or Dark).
    /// </summary>
    public static void ApplyThemeVariant(ToolsSettings.ToolTheme theme)
    {
        if (Application.Current != null && Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Application.Current.RequestedThemeVariant = theme switch
            {
                ToolsSettings.ToolTheme.Light => ThemeVariant.Light,
                ToolsSettings.ToolTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }

    /// <summary>
    ///     Applies an explicit dark or light Avalonia theme variant.
    /// </summary>
    public static void ApplyThemeVariant(bool isDark)
    {
        ApplyThemeVariant(isDark ? ToolsSettings.ToolTheme.Dark : ToolsSettings.ToolTheme.Light);
    }

    [RelayCommand]
    private void ShowStartScreen()
    {
        CloseAllOverlays();
        ActiveModelEditor = null;
        StartScreen.RefreshRecents(_workspaceService.GetRecentWorkspaces());
        IsStartScreenVisible = true;
    }

    [RelayCommand]
    private void SelectAsset(AssetDocumentViewModel? document)
    {
        if (document != null)
        {
            SelectDocument(document, pushHistory: true);
        }
    }

    [RelayCommand]
    private void NavigateBack()
    {
        var previous = _navigationHistory.GoBack();
        if (!previous.Success)
        {
            return;
        }

        SelectAsset(null, previous.Value.Key, previous.Value.FilePath, pushHistory: false, highlightFieldKey: previous.Value.FieldKey);
        OnPropertyChanged(nameof(CanNavigateBack));
        OnPropertyChanged(nameof(CanNavigateForward));
    }

    [RelayCommand]
    private void NavigateForward()
    {
        var next = _navigationHistory.GoForward();
        if (!next.Success)
        {
            return;
        }

        SelectAsset(null, next.Value.Key, next.Value.FilePath, pushHistory: false, highlightFieldKey: next.Value.FieldKey);
        OnPropertyChanged(nameof(CanNavigateBack));
        OnPropertyChanged(nameof(CanNavigateForward));
    }

    [RelayCommand]
    private void Undo()
    {
        if (!_undoStack.CanUndo)
        {
            return;
        }

        var desc = _undoStack.UndoDescription;
        if (_undoStack.Undo())
        {
            UpdateDirtyCount();
            if (!_workspaceService.UsesRealBackend)
            {
                RevalidateWorkspace();
            }

            UpdatePreviewDescriptor();
            if (ActiveModelEditor != null && ReferenceEquals(ActiveModelEditor.Document, SelectedAsset))
            {
                ActiveModelEditor.RefreshFromDocument();
            }

            ShowToast($"Undid {desc}");
        }
    }

    [RelayCommand]
    private void Redo()
    {
        if (!_undoStack.CanRedo)
        {
            return;
        }

        var desc = _undoStack.RedoDescription;
        if (_undoStack.Redo())
        {
            UpdateDirtyCount();
            if (!_workspaceService.UsesRealBackend)
            {
                RevalidateWorkspace();
            }

            UpdatePreviewDescriptor();
            if (ActiveModelEditor != null && ReferenceEquals(ActiveModelEditor.Document, SelectedAsset))
            {
                ActiveModelEditor.RefreshFromDocument();
            }

            ShowToast($"Redid {desc}");
        }
    }

    [RelayCommand]
    private void ZoomIn()
    {
        UiScale = ToolsSettings.ToolSettings.ClampUiScale(Math.Round(UiScale + 0.10, 2));
    }

    [RelayCommand]
    private void ZoomOut()
    {
        UiScale = ToolsSettings.ToolSettings.ClampUiScale(Math.Round(UiScale - 0.10, 2));
    }

    [RelayCommand]
    private void ResetZoom()
    {
        UiScale = ToolsSettings.ToolSettings.DEFAULT_UI_SCALE;
    }

    [RelayCommand]
    private void ToggleWorkspaceMode()
    {
        WorkspaceMode = WorkspaceMode == WorkspaceMode.Pack ? WorkspaceMode.Repo : WorkspaceMode.Pack;
        for (var i = 0; i < _allDocuments.Count; i++)
        {
            _allDocuments[i].UpdateReadOnlyForMode(WorkspaceMode);
        }

        ShowToast($"Switched workspace mode to {WorkspaceModeLabel}");
    }

    [RelayCommand]
    private void ShowFormView()
    {
        if (SelectedAsset != null && SelectedAsset.HasSyntaxError)
        {
            ShowToast("Form view is disabled while file has a TOML syntax error.");
            return;
        }

        IsTomlViewActive = false;
    }

    [RelayCommand]
    private void ShowTomlView()
    {
        IsTomlViewActive = true;
    }

    [RelayCommand]
    private void SelectSystemTheme() => ThemePreference = ToolsSettings.ToolTheme.System;

    [RelayCommand]
    private void SelectLightTheme() => ThemePreference = ToolsSettings.ToolTheme.Light;

    [RelayCommand]
    private void SelectDarkTheme() => ThemePreference = ToolsSettings.ToolTheme.Dark;

    [RelayCommand]
    private void ToggleTheme()
    {
        ThemePreference = ThemePreference switch
        {
            ToolsSettings.ToolTheme.System => ToolsSettings.ToolTheme.Light,
            ToolsSettings.ToolTheme.Light => ToolsSettings.ToolTheme.Dark,
            _ => ToolsSettings.ToolTheme.System,
        };
    }

    [RelayCommand]
    private void ToggleExplorer() => IsExplorerVisible = !IsExplorerVisible;

    [RelayCommand]
    private void ToggleInspector() => IsInspectorVisible = !IsInspectorVisible;

    [RelayCommand]
    private void ToggleValidationDock() => IsValidationDockVisible = !IsValidationDockVisible;

    [RelayCommand]
    private void SaveCurrentAsset()
    {
        var doc = SelectedAsset;
        if (doc == null || !doc.IsDirty)
        {
            ShowToast("Current asset has no unsaved changes.");
            return;
        }

        var result = _workspaceService.SaveDocuments([doc.BuildSavePayload()]);
        if (!result.Success)
        {
            ShowToast(result.Message);
            return;
        }

        string? nextHash = null;
        string? nextText = null;
        if (!string.IsNullOrWhiteSpace(doc.AbsoluteFilePath))
        {
            result.UpdatedFileHashes?.TryGetValue(doc.AbsoluteFilePath, out nextHash);
            result.UpdatedFileTexts?.TryGetValue(doc.AbsoluteFilePath, out nextText);
        }

        doc.MarkSaved(nextHash, nextText);
        UpdateDirtyCount();
        RevalidateWorkspace();
        ShowToast($"Saved '{doc.Id}'.");
    }

    [RelayCommand]
    private void SaveAll()
    {
        var dirtyDocs = new List<AssetDocumentViewModel>();
        var payloads = new List<DocumentSavePayload>();
        for (var i = 0; i < _allDocuments.Count; i++)
        {
            if (_allDocuments[i].IsDirty)
            {
                dirtyDocs.Add(_allDocuments[i]);
                payloads.Add(_allDocuments[i].BuildSavePayload());
            }
        }

        if (dirtyDocs.Count == 0)
        {
            ShowToast("All documents are already up to date.");
            return;
        }

        var result = _workspaceService.SaveDocuments(payloads);
        if (!result.Success)
        {
            ShowToast(result.Message);
            return;
        }

        for (var i = 0; i < dirtyDocs.Count; i++)
        {
            var doc = dirtyDocs[i];
            string? nextHash = null;
            string? nextText = null;
            if (!string.IsNullOrWhiteSpace(doc.AbsoluteFilePath))
            {
                result.UpdatedFileHashes?.TryGetValue(doc.AbsoluteFilePath, out nextHash);
                result.UpdatedFileTexts?.TryGetValue(doc.AbsoluteFilePath, out nextText);
            }

            doc.MarkSaved(nextHash, nextText);
        }

        UpdateDirtyCount();
        RevalidateWorkspace();
        ShowToast(result.Message);
    }

    [RelayCommand]
    private void DiscardCurrentAsset()
    {
        var target = SelectedAsset;
        if (target == null || !target.IsDirty)
        {
            ShowToast("Current asset has no unsaved changes.");
            return;
        }

        var resumeMode = ActiveModelEditor != null && ReferenceEquals(ActiveModelEditor.Document, target)
            ? (ItemModelEditorMode?)ActiveModelEditor.Mode
            : null;
        _undoStack.RemoveForDocument(target.DocumentKey);
        if (!HasOtherDirtyDocumentForFile(target, target.AbsoluteFilePath))
        {
            _workspaceService.DiscardDraft(target.AbsoluteFilePath);
        }

        StudioAssetSeed? freshSeed = null;
        var refreshed = _workspaceService.ReloadCurrentWorkspaceFromDisk();
        if (refreshed != null)
        {
            freshSeed = FindMatchingSeed(refreshed.Assets, target.Kind, target.Id, target.RelativeFile, target.EntryIndex);
        }

        freshSeed ??= target.InitialSeed;
        var restored = CreateDocumentViewModel(freshSeed, WorkspaceMode, forceClean: true);

        var idx = _allDocuments.IndexOf(target);
        if (idx >= 0)
        {
            DetachDocumentEvents(target);
            _allDocuments[idx] = restored;
        }

        RebuildAssetTree();
        SelectDocument(restored, pushHistory: false);
        UpdateDirtyCount();
        RevalidateWorkspace();
        UpdatePreviewDescriptor();
        if (resumeMode.HasValue && restored.Kind == AssetKind.Item && !restored.HasSyntaxError)
        {
            OpenModelEditor(resumeMode.Value);
        }

        ShowToast($"Discarded unsaved changes to '{target.Id}'.");
    }

    [RelayCommand]
    private void DiscardAllChanges()
    {
        if (!HasDirtyDocuments)
        {
            ShowToast("No unsaved changes to discard.");
            return;
        }

        var resumeMode = ActiveModelEditor?.Mode;
        var selectedKey = SelectedAsset?.DocumentKey;
        _workspaceService.DiscardAllDrafts();
        _undoStack.Clear();

        var refreshed = _workspaceService.ReloadCurrentWorkspaceFromDisk();
        if (refreshed != null)
        {
            ReplaceWorkspaceImages(refreshed.WorkspaceImages);
            ReplaceAllDocumentsFromSeeds(refreshed.Assets, refreshed.Mode, refreshed.Candidates, refreshed.AvailableShaders, forceClean: true);
        }
        else
        {
            var seeds = new List<StudioAssetSeed>(_allDocuments.Count);
            for (var i = 0; i < _allDocuments.Count; i++)
            {
                seeds.Add(_allDocuments[i].InitialSeed);
            }

            ReplaceAllDocumentsFromSeeds(seeds, WorkspaceMode, _candidates, _availableShaders, forceClean: true);
        }

        RebuildAssetTree();
        UpdateDirtyCount();
        RevalidateWorkspace();

        if (!string.IsNullOrWhiteSpace(selectedKey) && SelectAsset(null, selectedKey, null, pushHistory: false))
        {
            // Restored active selection.
        }
        else if (_allDocuments.Count > 0)
        {
            SelectDocument(_allDocuments[0], pushHistory: false);
        }
        else
        {
            SelectedAsset = null;
        }

        if (resumeMode.HasValue && SelectedAsset != null && SelectedAsset.Kind == AssetKind.Item && !SelectedAsset.HasSyntaxError)
        {
            OpenModelEditor(resumeMode.Value);
        }

        ShowToast("Discarded all unsaved changes.");
    }

    [RelayCommand]
    private void CycleOrientation()
    {
        PreviewOrientation = PreviewOrientation switch
        {
            BrickOrientation.North => BrickOrientation.East,
            BrickOrientation.East => BrickOrientation.South,
            BrickOrientation.South => BrickOrientation.West,
            _ => BrickOrientation.North,
        };
    }

    [RelayCommand]
    private void ToggleShapeGridMode()
    {
        IsShapeGridMode = !IsShapeGridMode;
    }

    [RelayCommand]
    private void ToggleConnectedShowcaseMode()
    {
        if (!ShowConnectedShowcaseToggle)
        {
            IsConnectedShowcaseMode = false;
            return;
        }

        IsConnectedShowcaseMode = !IsConnectedShowcaseMode;
    }

    [RelayCommand]
    private void SelectStandardBrickPreview()
    {
        IsConnectedShowcaseMode = false;
    }

    [RelayCommand]
    private void SelectConnectedShowcasePreview()
    {
        if (ShowConnectedShowcaseToggle)
        {
            IsConnectedShowcaseMode = true;
        }
    }

    [RelayCommand]
    private void SetMaterialPreviewShape(MaterialPreviewShape shape)
    {
        MaterialPreviewShape = shape;
    }

    [RelayCommand]
    private void PreviousBodyPreviewFrame()
    {
        if (_bodyPreviewFrames.Count <= 1)
        {
            return;
        }

        BodyPreviewFrameIndex = (BodyPreviewFrameIndex - 1 + _bodyPreviewFrames.Count) % _bodyPreviewFrames.Count;
    }

    [RelayCommand]
    private void NextBodyPreviewFrame()
    {
        if (_bodyPreviewFrames.Count <= 1)
        {
            return;
        }

        BodyPreviewFrameIndex = (BodyPreviewFrameIndex + 1) % _bodyPreviewFrames.Count;
    }

    [RelayCommand]
    private void ToggleOrbit()
    {
        IsPreviewOrbiting = !IsPreviewOrbiting;
        PreviewOrbitAngle = IsPreviewOrbiting ? 24 : 0;
    }

    [RelayCommand]
    private void ResetPreviewCamera()
    {
        PreviewOrientation = BrickOrientation.North;
        PreviewOrbitAngle = 0;
        UseItemWorldModelPreset = false;
        PreviewCameraResetRequested?.Invoke();
        UpdatePreviewDescriptor();
    }

    [RelayCommand]
    private async Task BrowseBodySpriteFileAsync(BodySpriteSlotViewModel? slot)
    {
        if (_pathPicker == null || SelectedAsset == null || SelectedAsset.Kind != AssetKind.Body || SelectedAsset.IsReadOnly)
        {
            return;
        }

        var startDir = !string.IsNullOrWhiteSpace(_lastSpritePickerDirectory) && Directory.Exists(_lastSpritePickerDirectory)
            ? _lastSpritePickerDirectory
            : (!string.IsNullOrWhiteSpace(_rootPath) && Directory.Exists(_rootPath) ? _rootPath : null);

        var pickedPath = await _pathPicker.PickImageFileAsync("Select Sprite Image", startDir);
        if (string.IsNullOrWhiteSpace(pickedPath))
        {
            return;
        }

        var pickedDir = Path.GetDirectoryName(pickedPath);
        if (!string.IsNullOrWhiteSpace(pickedDir))
        {
            _lastSpritePickerDirectory = pickedDir;
        }

        var resolved = _workspaceService.ResolveImagePathForWorkspace(pickedPath);
        if (slot != null)
        {
            slot.TexturePath = resolved;
            if (string.IsNullOrWhiteSpace(slot.ResolvedAbsolutePath) && File.Exists(pickedPath))
            {
                slot.UpdateResolvedPath(Path.GetFullPath(pickedPath));
            }
        }
        else
        {
            SelectedAsset.NewBodySpriteTexturePath = resolved;
        }
    }

    [RelayCommand]
    private void NavigatePaletteItem(BlueprintPaletteItem item)
    {
        if (!SelectAsset(item.Kind, item.AssetId, relativeFile: null, pushHistory: true))
        {
            ShowToast($"Palette entry '{item.AssetId}' ({item.Kind}) is not defined in the current workspace.");
        }
    }

    [RelayCommand]
    private void InitializeCurrentFolderAsPack()
    {
        if (string.IsNullOrWhiteSpace(_rootPath) || !Directory.Exists(_rootPath))
        {
            ShowToast("No valid folder is open to initialize as a content pack.");
            return;
        }

        var folderName = Path.GetFileName(_rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var slug = SanitizePackSlug(folderName, "new_pack");
        var configuredNamespace = _workspaceService.GetWorkspaceDefaultNamespace(_rootPath);
        if (string.IsNullOrWhiteSpace(configuredNamespace))
        {
            configuredNamespace = _workspaceService.GetSettings().DefaultNamespace;
        }

        var ns = SanitizePackSlug(configuredNamespace, slug);
        var displayName = string.IsNullOrWhiteSpace(folderName) ? slug : folderName;
        var author = string.IsNullOrWhiteSpace(Environment.UserName) ? "Author" : Environment.UserName;

        var created = _workspaceService.CreatePackWorkspace(_rootPath, slug, ns, displayName, author);
        ShowToast(created.Message);
        if (created.Success)
        {
            OpenWorkspace(WorkspaceMode.Pack, _rootPath, displayName, showWorkbench: true);
        }
    }

    [RelayCommand]
    private void ToggleItemFramingPreset()
    {
        UseItemWorldModelPreset = !UseItemWorldModelPreset;
    }

    [RelayCommand]
    private void SelectItemViewModelPreset()
    {
        UseItemWorldModelPreset = false;
    }

    [RelayCommand]
    private void SelectItemWorldModelPreset()
    {
        UseItemWorldModelPreset = true;
    }

    [RelayCommand]
    private void OpenViewModelEditor() => OpenModelEditor(ItemModelEditorMode.ViewModel);

    [RelayCommand]
    private void OpenWorldModelEditor() => OpenModelEditor(ItemModelEditorMode.WorldModel);

    [RelayCommand]
    private void CloseModelEditor()
    {
        if (ActiveModelEditor == null)
        {
            return;
        }

        var wasWorldModel = ActiveModelEditor.IsWorldModelMode;
        DisposeActiveModelEditor();
        if (UseItemWorldModelPreset != wasWorldModel)
        {
            UseItemWorldModelPreset = wasWorldModel;
        }
        else
        {
            UpdatePreviewDescriptor();
        }
    }

    private void DisposeActiveModelEditor()
    {
        var previous = ActiveModelEditor;
        ActiveModelEditor = null;
        previous?.Dispose();
    }

    [RelayCommand]
    private void HandleEscapeKey()
    {
        if (IsAnyOverlayOpen)
        {
            CloseAllOverlays();
            return;
        }

        if (IsModelEditorVisible)
        {
            CloseModelEditor();
        }
    }

    private void OpenModelEditor(ItemModelEditorMode mode)
    {
        if (SelectedAsset == null || SelectedAsset.Kind != AssetKind.Item)
        {
            return;
        }

        if (SelectedAsset.HasSyntaxError)
        {
            ShowToast("Visual model editor is disabled while file has a TOML syntax error.");
            return;
        }

        CloseAllOverlays();
        if (ActiveModelEditor != null && ReferenceEquals(ActiveModelEditor.Document, SelectedAsset))
        {
            ActiveModelEditor.Mode = mode;
            return;
        }

        DisposeActiveModelEditor();

        var glController = _previewProvider as StudioGlPreviewController;
        if (glController != null && _workspaceService is AssetToolsStudioWorkspaceService realService && realService.CurrentWorkspace != null)
        {
            glController.BindWorkspace(realService.CurrentWorkspace);
        }

        ActiveModelEditor = new ItemModelEditorViewModel(
            this,
            SelectedAsset,
            mode,
            glController,
            EnableLiveGlViewport,
            CloseModelEditor,
            transformClipboard: _transformClipboard,
            onStatus: ShowToast);
    }

    [RelayCommand]
    private void SwitchPaintEntry()
    {
        if (SelectedAsset == null || SelectedAsset.Kind != AssetKind.Paint)
        {
            return;
        }

        var nextId = string.Equals(SelectedAsset.Id, "wb:red", StringComparison.OrdinalIgnoreCase)
            ? "wb:panel_paint"
            : "wb:red";
        SelectAsset(AssetKind.Paint, nextId, relativeFile: null, pushHistory: true);
    }

    [RelayCommand]
    private void NavigateInboundReference(InboundReferenceItem reference)
    {
        if (!SelectAsset(reference.SourceKind, reference.SourceAssetId, reference.SourceFile, pushHistory: true))
        {
            ShowToast($"Reference source: {reference.SourceFile} ({reference.FieldPath})");
        }
    }

    [RelayCommand]
    private void NavigateRecipeTarget(string? rawTarget)
    {
        var trimmed = rawTarget?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        if (trimmed.StartsWith("tag:", StringComparison.Ordinal))
        {
            var tagName = trimmed[4..].Trim();
            if (string.IsNullOrEmpty(tagName)
                || !SelectAsset(AssetKind.Tag, tagName, relativeFile: null, pushHistory: true))
            {
                ShowToast($"Tag '{tagName}' was not found in the workspace.");
            }

            return;
        }

        if (SelectAsset(AssetKind.Item, trimmed, relativeFile: null, pushHistory: true))
        {
            return;
        }

        if (SelectAsset(AssetKind.Tag, trimmed, relativeFile: null, pushHistory: true))
        {
            return;
        }

        ShowToast($"Target '{trimmed}' was not found in the workspace.");
    }

    [RelayCommand]
    private void FocusFinding(ValidationFinding finding)
    {
        if (SelectAsset(null, finding.AssetId, finding.RelativeFile, pushHistory: true, highlightFieldKey: finding.FieldKey))
        {
            if (finding.Severity == ValidationSeverity.Fatal)
            {
                IsTomlViewActive = true;
            }

            ShowToast($"Jumped to {finding.RelativeFile}:{finding.LineNumber}");
        }
    }

    [RelayCommand]
    private void CopySkillLevelCurve()
    {
        var doc = SelectedAsset;
        if (doc == null || doc.Kind != AssetKind.Skill)
        {
            return;
        }

        var curve = doc.GetSkillLevelCurve();
        if (curve.Count == 0)
        {
            ShowToast($"Skill '{doc.Id}' has no level rows to copy.");
            return;
        }

        _copiedSkillLevelCurve = curve;
        OnPropertyChanged(nameof(CopiedSkillLevelCurve));
        OnPropertyChanged(nameof(CanPasteSkillLevelCurve));
        ShowToast($"Copied {curve.Count} skill level(s) from '{doc.Id}'.");
    }

    [RelayCommand]
    private void PasteSkillLevelCurve()
    {
        if (_copiedSkillLevelCurve == null || _copiedSkillLevelCurve.Count == 0)
        {
            return;
        }

        var doc = SelectedAsset;
        if (doc == null || doc.Kind != AssetKind.Skill)
        {
            return;
        }

        if (doc.IsReadOnly)
        {
            ShowToast($"Cannot paste skill level curve onto read-only base asset '{doc.Id}'.");
            return;
        }

        if (doc.ReplaceSkillLevels(_copiedSkillLevelCurve))
        {
            ShowToast($"Pasted {_copiedSkillLevelCurve.Count} skill level(s) onto '{doc.Id}'.");
        }
    }

    [RelayCommand]
    private void CopyAsset(object? target = null)
    {
        var doc = ResolveTargetDocument(target);
        if (doc == null)
        {
            return;
        }

        var captured = _workspaceService.CaptureAssetClipboard(
            doc.Kind,
            doc.Id,
            doc.AbsoluteFilePath,
            doc.TableIndex);
        if (!captured.Success)
        {
            ShowToast(captured.Message ?? $"Could not copy '{doc.Id}'.");
            return;
        }

        _copiedAssetClipboard = captured.Value;
        OnPropertyChanged(nameof(CopiedAssetClipboard));
        OnPropertyChanged(nameof(CanPasteAsset));
        ShowToast($"Copied {doc.Kind} '{doc.Id}' to studio clipboard.");
    }

    [RelayCommand]
    private void PasteAsset(object? target = null)
    {
        if (!_copiedAssetClipboard.HasValue)
        {
            ShowToast("Studio asset clipboard is empty.");
            return;
        }

        var payload = _copiedAssetClipboard.Value;
        var copiedKind = AssetSeedProjector.ToStudioAssetKind(payload.AssetKind);
        string? targetFilePath = null;

        if (target is AssetDocumentViewModel clickedDoc)
        {
            if (clickedDoc.IsReadOnly)
            {
                ShowToast($"Cannot paste into read-only base file '{clickedDoc.RelativeFile}'.");
                return;
            }

            if (clickedDoc.Kind == copiedKind)
            {
                targetFilePath = !string.IsNullOrWhiteSpace(clickedDoc.AbsoluteFilePath)
                    ? clickedDoc.AbsoluteFilePath
                    : clickedDoc.RelativeFile;
            }
        }
        else if (target is not AssetTreeGroupViewModel
                 && SelectedAsset != null
                 && !SelectedAsset.IsReadOnly
                 && SelectedAsset.Kind == copiedKind)
        {
            targetFilePath = !string.IsNullOrWhiteSpace(SelectedAsset.AbsoluteFilePath)
                ? SelectedAsset.AbsoluteFilePath
                : SelectedAsset.RelativeFile;
        }

        var result = _workspaceService.PasteAsset(payload, targetFilePath);
        if (!result.Success)
        {
            ShowToast(result.Message ?? $"Failed pasting '{payload.SourceAssetId}'.");
            return;
        }

        CompleteAssetCloneOperation(copiedKind, result.Value, isDuplicate: false);
    }

    [RelayCommand]
    private void DuplicateAsset(object? target = null)
    {
        var doc = ResolveTargetDocument(target);
        if (doc == null)
        {
            return;
        }

        if (doc.IsReadOnly)
        {
            ShowToast($"Cannot duplicate read-only base asset '{doc.Id}'.");
            return;
        }

        var result = _workspaceService.DuplicateAsset(
            doc.Kind,
            doc.Id,
            doc.AbsoluteFilePath,
            doc.TableIndex);
        if (!result.Success)
        {
            ShowToast(result.Message ?? $"Failed duplicating '{doc.Id}'.");
            return;
        }

        CompleteAssetCloneOperation(doc.Kind, result.Value, isDuplicate: true);
    }

    [RelayCommand]
    private void ToggleValidationSeverityFilter(ValidationSeverity severity)
    {
        ActiveValidationSeverityFilter = ActiveValidationSeverityFilter == severity ? null : severity;
    }

    [RelayCommand]
    private void FixAllSafeIssues()
    {
        var result = _workspaceService.FixAllSafeIssues();
        if (!result.Success)
        {
            ShowToast(result.Message ?? "Fix all safe issues failed.");
            return;
        }

        var bulk = result.Value;
        HashSet<string>? modifiedPaths = null;
        if (bulk.ModifiedFiles.Count > 0)
        {
            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            modifiedPaths = new HashSet<string>(bulk.ModifiedFiles, comparer);
        }

        RefreshWorkspaceFromDiskPreservingDirty(modifiedPaths);

        if (bulk.AppliedCount > 0)
        {
            var suffix = bulk.SkippedReadOnlyCount > 0
                ? $" ({bulk.SkippedReadOnlyCount} read-only skipped)."
                : ".";
            ShowToast($"Applied {bulk.AppliedCount} safe fix(es) across {bulk.ModifiedFileCount} file(s){suffix}");
        }
        else if (bulk.SkippedReadOnlyCount > 0)
        {
            ShowToast($"Skipped {bulk.SkippedReadOnlyCount} safe fix(es) in read-only roots.");
        }
        else
        {
            ShowToast("No safe automatic fixes remain.");
        }
    }

    [RelayCommand]
    private void ApplyValidationQuickFix(ValidationFinding finding)
    {
        if (!finding.HasQuickFix)
        {
            return;
        }

        switch (finding.QuickFixKind)
        {
            case ValidationQuickFixKind.CreateLocalizationKey:
            {
                SelectAsset(null, finding.AssetId, finding.RelativeFile, pushHistory: true, highlightFieldKey: finding.FieldKey);
                var field = SelectedAsset?.FindField(finding.FieldKey);
                OpenCreateLocalizationKeyDialogWithSuggestedKey(field, finding.TargetKey);
                break;
            }

            case ValidationQuickFixKind.FixPathCase:
            case ValidationQuickFixKind.ReplaceReference:
            case ValidationQuickFixKind.ReplaceValue:
            {
                if (TryApplyLiveScalarValidationFix(in finding, out var handled))
                {
                    break;
                }

                if (handled)
                {
                    break;
                }

                ApplyBackendValidationFix(in finding);
                break;
            }

            case ValidationQuickFixKind.FormatFile:
            {
                if (SelectAsset(null, finding.AssetId, finding.RelativeFile, pushHistory: true))
                {
                    FormatCurrentFile();
                }

                break;
            }

            case ValidationQuickFixKind.StripUtf8Bom:
            case ValidationQuickFixKind.NormalizeLineEndings:
            case ValidationQuickFixKind.RemoveArrayElement:
            case ValidationQuickFixKind.InsertDefaultSkillLevels:
            case ValidationQuickFixKind.RemoveField:
            {
                ApplyBackendValidationFix(in finding);
                break;
            }
        }
    }

    private bool TryApplyLiveScalarValidationFix(in ValidationFinding finding, out bool handled)
    {
        handled = false;
        if (string.IsNullOrWhiteSpace(finding.SuggestedFix))
        {
            return false;
        }

        if (!_workspaceService.UsesRealBackend)
        {
            if (!SelectAsset(null, finding.AssetId, finding.RelativeFile, pushHistory: true, highlightFieldKey: finding.FieldKey)
                || SelectedAsset == null)
            {
                return false;
            }

            if (SelectedAsset.IsReadOnly)
            {
                ShowToast($"Cannot apply quick fix to read-only base asset '{SelectedAsset.Id}'.");
                handled = true;
                return false;
            }

            var sampleField = SelectedAsset.FindField(finding.FieldKey);
            if (sampleField != null)
            {
                sampleField.Value = finding.SuggestedFix;
                sampleField.SetValidationState(false, null);
                RevalidateWorkspace();
                ShowToast($"Applied quick fix to {sampleField.Label}.");
                return true;
            }

            return false;
        }

        var targetDoc = FindDocument(null, finding.AssetId, finding.RelativeFile);
        if (targetDoc == null)
        {
            return false;
        }

        if (targetDoc.IsReadOnly)
        {
            ShowToast($"Cannot apply quick fix to read-only base asset '{targetDoc.Id}'.");
            handled = true;
            return false;
        }

        var field = targetDoc.FindField(finding.FieldKey);
        if (field == null || field.IsReadOnly || IsNonScalarFixTarget(in finding, field))
        {
            return false;
        }

        SelectDocument(targetDoc, pushHistory: true, highlightFieldKey: field.FieldKey);
        field.Value = finding.SuggestedFix;
        field.SetValidationState(false, null);
        RemoveFindingFromLiveList(finding);
        ShowToast($"Applied quick fix to {field.Label}. Save to write changes to disk.");
        return true;
    }

    private static bool IsNonScalarFixTarget(in ValidationFinding finding, StudioFieldViewModel field)
    {
        if (string.Equals(field.FieldKey, "Variants", StringComparison.OrdinalIgnoreCase)
            || string.Equals(field.FieldKey, "SoundPlace", StringComparison.OrdinalIgnoreCase)
            || string.Equals(field.FieldKey, "SoundBreak", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (field.Value.Contains(','))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(finding.Code)
            && (finding.Code.StartsWith("TAG", StringComparison.OrdinalIgnoreCase)
                || finding.Code.StartsWith("SND", StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyBackendValidationFix(in ValidationFinding finding)
    {
        var existingDoc = FindDocument(null, finding.AssetId, finding.RelativeFile);
        if (existingDoc != null && existingDoc.IsReadOnly)
        {
            ShowToast($"Cannot apply quick fix to read-only base asset '{existingDoc.Id}'.");
            return;
        }

        var fixResult = _workspaceService.ApplyValidationFix(finding);
        if (!fixResult.Success)
        {
            ShowToast(fixResult.Message ?? $"Quick fix for {finding.Code} failed.");
            return;
        }

        if (!fixResult.Value)
        {
            ShowToast($"No change applied for {finding.Code} (target may be read-only or already fixed).");
            return;
        }

        HashSet<string>? modifiedPaths = null;
        if (!string.IsNullOrWhiteSpace(finding.AbsoluteFilePath))
        {
            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            modifiedPaths = new HashSet<string>(comparer) { Path.GetFullPath(finding.AbsoluteFilePath) };
        }
        else if (existingDoc != null && !string.IsNullOrWhiteSpace(existingDoc.AbsoluteFilePath))
        {
            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            modifiedPaths = new HashSet<string>(comparer) { Path.GetFullPath(existingDoc.AbsoluteFilePath) };
        }

        RefreshWorkspaceFromDiskPreservingDirty(
            modifiedPaths,
            selectKind: null,
            selectAssetId: finding.AssetId,
            selectFilePath: finding.RelativeFile,
            pushSelectionHistory: true);
        ShowToast(finding.QuickFixLabel ?? $"Applied {finding.Code} quick fix.");
    }

    private AssetDocumentViewModel? ResolveTargetDocument(object? target) =>
        target switch
        {
            AssetDocumentViewModel doc => doc,
            _ => SelectedAsset,
        };

    private void CompleteAssetCloneOperation(
        AssetKind kind,
        ToolsEditing.AssetCloneResult cloneResult,
        bool isDuplicate)
    {
        RefreshWorkspaceFromDiskPreservingDirty(
            modifiedAbsolutePaths: null,
            selectKind: kind,
            selectAssetId: cloneResult.NewAssetId,
            selectFilePath: cloneResult.TargetFilePath,
            pushSelectionHistory: true);

        var actionVerb = isDuplicate ? "Duplicated" : "Pasted";
        ShowToast($"{actionVerb} '{cloneResult.SourceAssetId}' -> '{cloneResult.NewAssetId}'.");
    }

    [RelayCommand]
    private void FixCaseIssues()
    {
        if (_workspaceService.UsesRealBackend)
        {
            var result = _workspaceService.FixCaseIssues();
            RevalidateWorkspace();
            ShowToast(result.Message);
            return;
        }

        var fixedCount = 0;
        for (var i = 0; i < AllFindings.Count; i++)
        {
            var finding = AllFindings[i];
            if (!finding.CanFixCase || string.IsNullOrWhiteSpace(finding.SuggestedFix))
            {
                continue;
            }

            var doc = FindDocument(null, finding.AssetId, finding.RelativeFile);
            var field = doc?.FindField(finding.FieldKey);
            if (field != null)
            {
                field.Value = finding.SuggestedFix;
                fixedCount++;
            }
        }

        RevalidateWorkspace();
        ShowToast(fixedCount > 0
            ? $"Fixed {fixedCount} cross-platform path casing mismatch(es)."
            : "No case-only path mismatches remain.");
    }

    [RelayCommand]
    private void FormatCurrentFile()
    {
        if (SelectedAsset == null)
        {
            return;
        }

        if (SelectedAsset.HasSyntaxError)
        {
            ShowToast("Cannot format file while TOML syntax errors are present.");
            return;
        }

        if (_workspaceService.UsesRealBackend && !string.IsNullOrWhiteSpace(SelectedAsset.AbsoluteFilePath))
        {
            var res = _workspaceService.FormatDocument(SelectedAsset.AbsoluteFilePath);
            if (res.Success && File.Exists(SelectedAsset.AbsoluteFilePath))
            {
                SelectedAsset.MarkSaved(null, File.ReadAllText(SelectedAsset.AbsoluteFilePath));
                UpdateDirtyCount();
            }

            ShowToast(res.Message);
            return;
        }

        SelectedAsset.FormatToml();
        UpdateDirtyCount();
        ShowToast($"Formatted {SelectedAsset.RelativeFile} with house alignment.");
    }

    [RelayCommand]
    private void RevalidateWorkspace()
    {
        if (_workspaceService.UsesRealBackend)
        {
            var synced = _workspaceService.RevalidateWorkspace();
            if (synced.HasBackendFindings)
            {
                AllFindings.Clear();
                for (var i = 0; i < synced.Findings.Count; i++)
                {
                    AllFindings.Add(synced.Findings[i]);
                }

                RebuildValidationGroups();
                RefreshDocumentInboundReferences();
                UpdatePreviewDescriptor();
                return;
            }
        }

        var fresh = ComputeLiveFindings();
        AllFindings.Clear();
        for (var i = 0; i < fresh.Count; i++)
        {
            AllFindings.Add(fresh[i]);
        }

        RebuildValidationGroups();
        UpdatePreviewDescriptor();
    }

    private void RefreshDocumentInboundReferences()
    {
        if (!_workspaceService.UsesRealBackend)
        {
            return;
        }

        for (var i = 0; i < _allDocuments.Count; i++)
        {
            var doc = _allDocuments[i];
            if (doc.Kind == AssetKind.Blueprint)
            {
                continue;
            }

            doc.SyncInboundReferences(_workspaceService.GetInboundReferences(doc.Kind, doc.Id));
        }
    }

    [RelayCommand]
    private void CopyAllValidation()
    {
        LastCopiedValidationText = _workspaceService.FormatValidationPlainText(AllFindings);
        ShowToast($"Copied {AllFindings.Count} validation finding(s) as plain text.");
    }

    [RelayCommand]
    private void OpenCommandPalette()
    {
        CloseAllOverlays();
        Func<string, string, string, IReadOnlyList<CommandPaletteEntry>>? backendSearch =
            _workspaceService.UsesRealBackend
                ? (q, category, scope) => _workspaceService.SearchCommandPalette(q, category, scope)
                : null;

        ActiveCommandPalette = new CommandPaletteViewModel(
            BuildCommandPaletteEntries(),
            HandlePaletteExecute,
            CloseAllOverlays,
            backendSearch);
    }

    [RelayCommand]
    private void OpenNewAssetDialog()
    {
        CloseAllOverlays();
        ActiveNewAssetDialog = new NewAssetDialogViewModel(
            _workspaceService,
            HandleCreateAsset,
            CloseAllOverlays,
            _defaultNamespace);
    }

    [RelayCommand]
    private void OpenRenameDialog()
    {
        OpenRenameDialogForAsset(SelectedAsset);
    }

    [RelayCommand]
    private void OpenRenameDialogForAsset(AssetDocumentViewModel? document)
    {
        var target = document ?? SelectedAsset;
        if (target == null || !target.CanRenameId)
        {
            return;
        }

        if (!ReferenceEquals(SelectedAsset, target))
        {
            SelectDocument(target, pushHistory: true);
        }

        CloseAllOverlays();
        ActiveRenameDialog = new RenamePreviewDialogViewModel(
            target.Id,
            target.Kind,
            _workspaceService,
            HandleApplyRename,
            CloseAllOverlays);
    }

    [RelayCommand]
    private void RequestDeleteAsset(AssetDocumentViewModel? document = null)
    {
        var target = document ?? SelectedAsset;
        if (target == null)
        {
            return;
        }

        if (target.IsReadOnly)
        {
            ShowToast($"Cannot delete read-only base asset '{target.Id}'.");
            return;
        }

        CloseAllOverlays();
        ActiveDeleteAssetDialog = new DeleteAssetDialogViewModel(
            target,
            () => HandleConfirmDeleteAsset(target),
            CloseAllOverlays);
    }

    [RelayCommand]
    private void OpenCreateLocalizationKeyDialog(StudioFieldViewModel? targetField = null)
    {
        OpenCreateLocalizationKeyDialogWithSuggestedKey(targetField, explicitSuggestedKey: null);
    }

    [RelayCommand]
    private void OpenSettingsDialog()
    {
        CloseAllOverlays();
        var current = _workspaceService.GetSettings() with
        {
            IsDarkTheme = ThemePreference == ToolsSettings.ToolTheme.Dark,
            Theme = ThemePreference,
            UiScale = UiScale,
            DefaultNamespace = _defaultNamespace,
        };
        ActiveSettingsDialog = new SettingsDialogViewModel(
            current,
            HandleSaveSettings,
            HandleResetPanelLayout,
            CloseAllOverlays);
    }

    [RelayCommand]
    private void OpenAboutDialog()
    {
        CloseAllOverlays();
        var info = _workspaceService.GetDiagnosticsInfo(
            WorkspaceMode,
            _rootPath,
            _referenceRootPath,
            _previewProvider.VendorRendererLabel);
        ActiveAboutDialog = new AboutDialogViewModel(info, _workspaceService, CloseAllOverlays);
    }

    [RelayCommand]
    private void OpenExportDialog()
    {
        CloseAllOverlays();
        RevalidateWorkspace();
        var stagedToml = _workspaceService.BuildStagedPackToml(_packId, _packNamespace, ContentVersion);
        ActiveExportDialog = new ExportDialogViewModel(
            _packId,
            ContentVersion,
            FatalCount,
            BrokenCount,
            CrossPlatformCount,
            StyleCount,
            stagedToml,
            HandleExportPack,
            CloseAllOverlays);
    }

    [RelayCommand]
    private void OpenDraftRecoveryDialog()
    {
        CloseAllOverlays();
        ActiveDraftRecoveryDialog = new DraftRecoveryDialogViewModel(
            PendingRecoveryDrafts,
            ShowToast,
            () =>
            {
                HasPendingRecoveryDrafts = false;
                CloseAllOverlays();
            },
            entry => _workspaceService.RecoverDraft(entry),
            entry => _workspaceService.DiscardDraft(entry));
    }

    [RelayCommand]
    private void CloseAllOverlays()
    {
        ActiveCommandPalette = null;
        ActiveNewAssetDialog = null;
        ActiveRenameDialog = null;
        ActiveDeleteAssetDialog = null;
        ActiveDeleteRecentWorkspaceDialog = null;
        ActiveCreateLocKeyDialog = null;
        ActiveSettingsDialog = null;
        ActiveAboutDialog = null;
        ActiveExportDialog = null;
        ActiveDraftRecoveryDialog = null;
    }

    private void RequestDeleteRecentWorkspace(RecentWorkspaceEntry entry)
    {
        var canDeleteFromDisk = true;
        string? disabledReason = null;
        if (_workspaceService.UsesRealBackend && AssetToolsStudioWorkspaceService.IsUnsafeDiskDeleteTarget(entry.RootPath))
        {
            canDeleteFromDisk = false;
            disabledReason = "Recursive disk deletion is blocked for home, root, or system directories.";
        }

        CloseAllOverlays();
        ActiveDeleteRecentWorkspaceDialog = new DeleteRecentWorkspaceDialogViewModel(
            entry,
            canDeleteFromDisk,
            disabledReason,
            deleteFromDisk => HandleConfirmDeleteRecentWorkspace(entry, deleteFromDisk),
            CloseAllOverlays);
    }

    private void HandleConfirmDeleteRecentWorkspace(RecentWorkspaceEntry entry, bool deleteFromDisk)
    {
        var matchesActiveWorkspace = IsSameWorkspaceRoot(_rootPath, entry.RootPath);
        if (deleteFromDisk && matchesActiveWorkspace)
        {
            _fileWatcher.Stop();
        }

        var result = _workspaceService.RemoveRecentWorkspace(entry.RootPath, deleteFromDisk);
        StartScreen.RefreshRecents(_workspaceService.GetRecentWorkspaces());

        if (deleteFromDisk && result.Success && matchesActiveWorkspace)
        {
            ClearLoadedWorkspaceState();
        }

        CloseAllOverlays();
        ShowToast(result.Message);
    }

    private void ClearLoadedWorkspaceState()
    {
        DisposeActiveModelEditor();
        _undoStack.Clear();
        _navigationHistory.Clear();
        OnPropertyChanged(nameof(CanNavigateBack));
        OnPropertyChanged(nameof(CanNavigateForward));

        SelectedAsset = null;
        for (var i = 0; i < _allDocuments.Count; i++)
        {
            DetachDocumentEvents(_allDocuments[i]);
        }

        _allDocuments.Clear();
        _candidates.Clear();
        _availableShaders.Clear();
        RebuildAssetTree();
        AllFindings.Clear();
        RebuildValidationGroups();
        PendingRecoveryDrafts.Clear();
        HasPendingRecoveryDrafts = false;
        UpdateDirtyCount();

        _rootPath = string.Empty;
        WorkspaceName = string.Empty;
        IsUninitializedFolder = false;
        _referenceRootPath = null;
        _packId = string.Empty;
        _packNamespace = string.Empty;
        _defaultNamespace = string.Empty;
        OnPropertyChanged(nameof(DefaultNamespace));
        OnPropertyChanged(nameof(CurrentWorkspaceRootPath));
        OnPropertyChanged(nameof(CanPasteAsset));
        OnPropertyChanged(nameof(CanPasteSkillLevelCurve));
    }

    private static bool IsSameWorkspaceRoot(string? leftPath, string? rightPath)
    {
        if (string.IsNullOrWhiteSpace(leftPath) || string.IsNullOrWhiteSpace(rightPath))
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        try
        {
            var left = Path.GetFullPath(leftPath.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var right = Path.GetFullPath(rightPath.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(left, right, comparison);
        }
        catch
        {
            return string.Equals(leftPath.Trim(), rightPath.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private void DismissToast()
    {
        StopToastTimer();
        ToastMessage = null;
    }

    private void OpenCreateLocalizationKeyDialogWithSuggestedKey(StudioFieldViewModel? targetField, string? explicitSuggestedKey)
    {
        CloseAllOverlays();
        _pendingLocTargetField = targetField;
        var kind = SelectedAsset?.Kind ?? AssetKind.Item;
        var suggested = !string.IsNullOrWhiteSpace(explicitSuggestedKey)
            ? explicitSuggestedKey.Trim()
            : (targetField != null && !string.IsNullOrWhiteSpace(targetField.Value)
                ? targetField.Value
                : $"item.{_defaultNamespace}.new_entry");

        ActiveCreateLocKeyDialog = new CreateLocalizationKeyDialogViewModel(
            suggested,
            _workspaceService.GetDefaultLocalizationFile(kind),
            _workspaceService.GetLocalizationFiles(),
            HandleCreateLocalizationKey,
            CloseAllOverlays);
    }

    private void OpenWorkspace(WorkspaceMode mode, string rootPath, string? displayName, bool showWorkbench)
    {
        CloseAllOverlays();
        var snapshot = _workspaceService.OpenWorkspace(mode, rootPath, displayName);
        ApplyWorkspaceSnapshot(snapshot, showWorkbench);
    }

    private void ApplyWorkspaceSnapshot(StudioWorkspaceSnapshot snapshot, bool showWorkbench)
    {
        DisposeActiveModelEditor();
        WorkspaceMode = snapshot.Mode;
        WorkspaceName = snapshot.WorkspaceName;
        IsUninitializedFolder = snapshot.IsUninitializedFolder;
        ContentVersion = snapshot.ContentVersion;
        _rootPath = snapshot.RootPath;
        _lastSpritePickerDirectory = null;
        _lastMeshPickerDirectory = null;
        _referenceRootPath = snapshot.ReferenceRootPath;
        _packId = snapshot.PackId;
        _packNamespace = snapshot.PackNamespace;
        _defaultNamespace = !string.IsNullOrWhiteSpace(snapshot.DefaultNamespace)
            ? snapshot.DefaultNamespace
            : (!string.IsNullOrWhiteSpace(snapshot.PackNamespace) ? snapshot.PackNamespace : "wb");
        OnPropertyChanged(nameof(DefaultNamespace));
        OnPropertyChanged(nameof(CurrentWorkspaceRootPath));

        if (_previewProvider is StudioGlPreviewController glController
            && _workspaceService is AssetToolsStudioWorkspaceService realService)
        {
            glController.BindWorkspace(realService.CurrentWorkspace);
        }

        _undoStack.Clear();
        ActiveValidationSeverityFilter = null;

        ReplaceWorkspaceImages(snapshot.WorkspaceImages);
        ReplaceAllDocumentsFromSeeds(snapshot.Assets, snapshot.Mode, snapshot.Candidates, snapshot.AvailableShaders, forceClean: false);

        PendingRecoveryDrafts.Clear();
        for (var i = 0; i < snapshot.PendingDrafts.Count; i++)
        {
            PendingRecoveryDrafts.Add(snapshot.PendingDrafts[i]);
        }

        HasPendingRecoveryDrafts = PendingRecoveryDrafts.Count > 0;

        _navigationHistory.Clear();
        OnPropertyChanged(nameof(CanNavigateBack));
        OnPropertyChanged(nameof(CanNavigateForward));

        RebuildAssetTree();

        AllFindings.Clear();
        if (_workspaceService.UsesRealBackend)
        {
            for (var i = 0; i < snapshot.Findings.Count; i++)
            {
                AllFindings.Add(snapshot.Findings[i]);
            }

            RebuildValidationGroups();
        }
        else
        {
            RevalidateWorkspace();
        }

        UpdateDirtyCount();

        if (_allDocuments.Count > 0)
        {
            SelectDocument(_allDocuments[0], pushHistory: true);
        }
        else
        {
            SelectedAsset = null;
        }

        if (_workspaceService.UsesRealBackend)
        {
            _fileWatcher.Watch(_workspaceService.GetWatchedDirectories());
        }
        else
        {
            _fileWatcher.Stop();
        }

        if (showWorkbench)
        {
            IsStartScreenVisible = false;
        }

        StartScreen.RefreshRecents(_workspaceService.GetRecentWorkspaces());
        OnPropertyChanged(nameof(CanPasteAsset));
        OnPropertyChanged(nameof(CanPasteSkillLevelCurve));
    }

    private void ReplaceAllDocumentsFromSeeds(
        IReadOnlyList<StudioAssetSeed> seeds,
        WorkspaceMode mode,
        IReadOnlyList<ReferenceCandidate> candidates,
        IReadOnlyList<string> availableShaders,
        bool forceClean)
    {
        for (var i = 0; i < _allDocuments.Count; i++)
        {
            DetachDocumentEvents(_allDocuments[i]);
        }

        _candidates.Clear();
        _candidates.AddRange(candidates);

        _availableShaders.Clear();
        _availableShaders.AddRange(availableShaders);

        _allDocuments.Clear();
        for (var i = 0; i < seeds.Count; i++)
        {
            _allDocuments.Add(CreateDocumentViewModel(seeds[i], mode, forceClean));
        }
    }

    private AssetDocumentViewModel CreateDocumentViewModel(StudioAssetSeed seed, WorkspaceMode mode, bool forceClean)
    {
        Func<DocumentSavePayload, string?>? previewEditFn = _workspaceService.UsesRealBackend
            ? _workspaceService.PreviewEditedToml
            : null;
        Action<AssetDocumentViewModel, string, string?>? blueprintFn = _workspaceService.UsesRealBackend
            ? HandleBlueprintAction
            : null;

        var doc = new AssetDocumentViewModel(
            seed,
            mode,
            _candidates,
            HandleDocumentMutated,
            HandleDocumentCreateLocKeyRequested,
            ShowToast,
            previewEditFn,
            blueprintFn,
            _availableShaders,
            HandleDocumentRecordUndo,
            () => _undoStack.IsReplaying,
            ResolveTextureAbsolutePathForDocument,
            forceClean,
            WorkspaceImages,
            HandleDocumentBrowseTextureFileRequested);
        doc.BodySpritesChanged += HandleDocumentBodySpritesChanged;
        return doc;
    }

    private void DetachDocumentEvents(AssetDocumentViewModel doc)
    {
        doc.BodySpritesChanged -= HandleDocumentBodySpritesChanged;
    }

    private void HandleDocumentBodySpritesChanged(AssetDocumentViewModel doc, bool syncToSelectedState)
    {
        if (ReferenceEquals(SelectedAsset, doc))
        {
            RebuildBodyPreviewFrames(syncToSelectedState);
            UpdatePreviewDescriptor();
        }
    }

    private void HandleDocumentRecordUndo(AssetDocumentViewModel doc, StudioUndoAction action)
    {
        var wrapped = new StudioUndoAction(
            action.Description,
            action.DocumentKey,
            undo: () =>
            {
                if (!ReferenceEquals(SelectedAsset, doc) && _allDocuments.Contains(doc))
                {
                    SelectDocument(doc, pushHistory: false);
                }

                action.Undo();
            },
            redo: () =>
            {
                if (!ReferenceEquals(SelectedAsset, doc) && _allDocuments.Contains(doc))
                {
                    SelectDocument(doc, pushHistory: false);
                }

                action.Redo();
            },
            action.CoalesceKey,
            action.TimestampUtc);
        _undoStack.Push(wrapped);
    }

    private void HandleUndoStateChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoTooltip));
        OnPropertyChanged(nameof(RedoTooltip));
    }

    private void HandleExternalFilesChanged(IReadOnlyList<string> changedPaths)
    {
        if (_disposed || !_workspaceService.UsesRealBackend || changedPaths.Count == 0)
        {
            return;
        }

        var hasExternal = false;
        for (var i = 0; i < changedPaths.Count; i++)
        {
            if (!_workspaceService.WasRecentlySavedByStudio(changedPaths[i]))
            {
                hasExternal = true;
                break;
            }
        }

        if (!hasExternal)
        {
            return;
        }

        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                ApplyExternalDiskReload();
            }
            else
            {
                Dispatcher.UIThread.Post(ApplyExternalDiskReload);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed dispatching external workspace reload onto UI thread: {ex.Message}");
        }
    }

    private void ApplyExternalDiskReload()
    {
        RefreshWorkspaceFromDiskPreservingDirty(
            modifiedAbsolutePaths: null,
            toastMessage: "Updated workspace from external disk changes.");
    }

    private void RefreshWorkspaceFromDiskPreservingDirty(
        IReadOnlySet<string>? modifiedAbsolutePaths,
        AssetKind? selectKind = null,
        string? selectAssetId = null,
        string? selectFilePath = null,
        bool pushSelectionHistory = false,
        string? toastMessage = null)
    {
        if (_disposed)
        {
            return;
        }

        var refreshed = _workspaceService.ReloadCurrentWorkspaceFromDisk();
        if (refreshed == null)
        {
            return;
        }

        var resumeMode = ActiveModelEditor?.Mode;
        var selectedKey = SelectedAsset?.DocumentKey;

        var dirtyByKey = new Dictionary<string, AssetDocumentViewModel>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _allDocuments.Count; i++)
        {
            var doc = _allDocuments[i];
            var rewrittenByStudio = modifiedAbsolutePaths != null
                && (!string.IsNullOrWhiteSpace(doc.AbsoluteFilePath) && modifiedAbsolutePaths.Contains(Path.GetFullPath(doc.AbsoluteFilePath))
                    || modifiedAbsolutePaths.Contains(doc.RelativeFile));
            if (doc.IsDirty && !rewrittenByStudio)
            {
                dirtyByKey[doc.DocumentKey] = doc;
            }
            else
            {
                DetachDocumentEvents(doc);
            }
        }

        IsUninitializedFolder = refreshed.IsUninitializedFolder;
        ReplaceWorkspaceImages(refreshed.WorkspaceImages);

        _candidates.Clear();
        _candidates.AddRange(refreshed.Candidates);

        _availableShaders.Clear();
        _availableShaders.AddRange(refreshed.AvailableShaders);

        var merged = new List<AssetDocumentViewModel>(refreshed.Assets.Count + dirtyByKey.Count);
        var placedDirty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < refreshed.Assets.Count; i++)
        {
            var seed = refreshed.Assets[i];
            var seedKey = $"{seed.Kind}|{seed.Id}|{seed.RelativeFile}";
            if (dirtyByKey.TryGetValue(seedKey, out var dirtyDoc))
            {
                dirtyDoc.UpdateWorkspaceImages(WorkspaceImages);
                merged.Add(dirtyDoc);
                placedDirty.Add(seedKey);
            }
            else
            {
                merged.Add(CreateDocumentViewModel(seed, refreshed.Mode, forceClean: false));
            }
        }

        foreach (var kvp in dirtyByKey)
        {
            if (!placedDirty.Contains(kvp.Key))
            {
                kvp.Value.UpdateWorkspaceImages(WorkspaceImages);
                merged.Add(kvp.Value);
            }
        }

        _allDocuments.Clear();
        _allDocuments.AddRange(merged);

        if (_previewProvider is StudioGlPreviewController glController
            && _workspaceService is AssetToolsStudioWorkspaceService realService)
        {
            glController.BindWorkspace(realService.CurrentWorkspace);
        }

        RebuildAssetTree();

        AllFindings.Clear();
        for (var i = 0; i < refreshed.Findings.Count; i++)
        {
            AllFindings.Add(refreshed.Findings[i]);
        }

        RebuildValidationGroups();
        UpdateDirtyCount();
        OnPropertyChanged(nameof(CanPasteAsset));

        if (!string.IsNullOrWhiteSpace(selectAssetId)
            && TrySelectClonedOrTargetDocument(selectKind, selectAssetId, selectFilePath, pushSelectionHistory))
        {
            // Selected explicit target document cleanly.
        }
        else if (!string.IsNullOrWhiteSpace(selectedKey) && SelectAsset(null, selectedKey, null, pushHistory: false))
        {
            // Preserved current selection cleanly.
        }
        else if (_allDocuments.Count > 0)
        {
            SelectDocument(_allDocuments[0], pushHistory: false);
        }
        else
        {
            SelectedAsset = null;
        }

        if (resumeMode.HasValue && SelectedAsset != null && SelectedAsset.Kind == AssetKind.Item && !SelectedAsset.HasSyntaxError)
        {
            OpenModelEditor(resumeMode.Value);
        }

        if (!string.IsNullOrWhiteSpace(toastMessage))
        {
            ShowToast(toastMessage);
        }
    }

    private bool TrySelectClonedOrTargetDocument(
        AssetKind? kind,
        string assetId,
        string? filePath,
        bool pushHistory)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            string? fullTargetPath = null;
            try
            {
                if (Path.IsPathRooted(filePath))
                {
                    fullTargetPath = Path.GetFullPath(filePath);
                }
            }
            catch
            {
                fullTargetPath = null;
            }

            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            for (var i = 0; i < _allDocuments.Count; i++)
            {
                var doc = _allDocuments[i];
                if (kind.HasValue && doc.Kind != kind.Value)
                {
                    continue;
                }

                if (!string.Equals(doc.Id, assetId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var absMatch = fullTargetPath != null
                    && !string.IsNullOrWhiteSpace(doc.AbsoluteFilePath)
                    && string.Equals(Path.GetFullPath(doc.AbsoluteFilePath), fullTargetPath, comparison);
                var relMatch = string.Equals(doc.RelativeFile, filePath, StringComparison.OrdinalIgnoreCase);
                if (absMatch || relMatch)
                {
                    SelectDocument(doc, pushHistory);
                    return true;
                }
            }
        }

        return SelectAsset(kind, assetId, filePath, pushHistory);
    }

    private void RebuildAssetTree()
    {
        var query = TreeFilterText.Trim();
        var groupMap = new Dictionary<string, AssetTreeGroupViewModel>(StringComparer.Ordinal);
        var orderedGroups = new List<AssetTreeGroupViewModel>();

        for (var i = 0; i < _allDocuments.Count; i++)
        {
            var doc = _allDocuments[i];
            if (query.Length > 0
                && !doc.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !doc.RelativeFile.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !doc.Kind.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!groupMap.TryGetValue(doc.GroupTitle, out var group))
            {
                group = new AssetTreeGroupViewModel(doc.GroupTitle, doc.Kind);
                groupMap[doc.GroupTitle] = group;
                orderedGroups.Add(group);
            }

            group.Items.Add(doc);
        }

        TreeGroups.Clear();
        for (var i = 0; i < orderedGroups.Count; i++)
        {
            TreeGroups.Add(orderedGroups[i]);
        }

        OnPropertyChanged(nameof(ShowExplorerEmptyHint));
        OnPropertyChanged(nameof(ExplorerEmptyHintText));
    }

    private List<ValidationFinding> ComputeLiveFindings()
    {
        var list = new List<ValidationFinding>();
        for (var i = 0; i < _allDocuments.Count; i++)
        {
            var doc = _allDocuments[i];
            if (doc.HasSyntaxError)
            {
                list.Add(new ValidationFinding(
                    Id: $"F-{i + 1:000}",
                    Severity: ValidationSeverity.Fatal,
                    RootName: doc.RootName,
                    RelativeFile: doc.RelativeFile,
                    LineNumber: doc.SyntaxErrorLineNumber > 0 ? doc.SyntaxErrorLineNumber : 4,
                    AssetId: doc.Id,
                    FieldKey: "Shape",
                    Message: doc.SyntaxErrorMessage ?? "TOML syntax error (form disabled)",
                    Code: "TOML001"));
            }

            if (doc.Kind == AssetKind.Brick && doc.BrickTexDefaultField != null && doc.BrickTexDefaultField.IsDanger)
            {
                var suggested = doc.BrickTexDefaultField.QuickFixValue;
                list.Add(new ValidationFinding(
                    Id: $"B-{i + 1:000}",
                    Severity: ValidationSeverity.Broken,
                    RootName: doc.RootName,
                    RelativeFile: doc.RelativeFile,
                    LineNumber: 12,
                    AssetId: doc.Id,
                    FieldKey: "TextureDefault",
                    Message: doc.BrickTexDefaultField.DangerMessage ?? "Missing brick texture layer",
                    SuggestedFix: suggested,
                    Code: "REF001",
                    QuickFixKind: !string.IsNullOrWhiteSpace(suggested) ? ValidationQuickFixKind.ReplaceReference : ValidationQuickFixKind.None,
                    QuickFixLabel: !string.IsNullOrWhiteSpace(suggested) ? $"Replace with \"{suggested}\"" : null));
            }

            if (doc.Kind == AssetKind.Item && doc.IconField != null && doc.IconField.IsDanger)
            {
                var suggested = doc.IconField.QuickFixValue;
                list.Add(new ValidationFinding(
                    Id: $"X-{i + 1:000}",
                    Severity: ValidationSeverity.CrossPlatform,
                    RootName: doc.RootName,
                    RelativeFile: doc.RelativeFile,
                    LineNumber: 5,
                    AssetId: doc.Id,
                    FieldKey: "Icon",
                    Message: doc.IconField.DangerMessage ?? "Cross-platform path casing mismatch",
                    CanFixCase: true,
                    SuggestedFix: suggested,
                    Code: "PATH001",
                    QuickFixKind: !string.IsNullOrWhiteSpace(suggested) ? ValidationQuickFixKind.FixPathCase : ValidationQuickFixKind.None,
                    QuickFixLabel: !string.IsNullOrWhiteSpace(suggested) ? $"Fix path casing (\"{suggested}\")" : null));
            }

            if (doc.Kind == AssetKind.Tag)
            {
                list.Add(new ValidationFinding(
                    Id: $"S-{i + 1:000}",
                    Severity: ValidationSeverity.Style,
                    RootName: doc.RootName,
                    RelativeFile: doc.RelativeFile,
                    LineNumber: 5,
                    AssetId: doc.Id,
                    FieldKey: null,
                    Message: doc.HasDuplicateTags
                        ? "Duplicate tag entry detected in file order (flagged without auto-merging)"
                        : "Tag order matters; prefer appending new entries at the end",
                    Code: "STYLE001"));
            }
        }

        return list;
    }

    private void RebuildValidationGroups()
    {
        FatalCount = 0;
        BrokenCount = 0;
        CrossPlatformCount = 0;
        StyleCount = 0;

        var safeCount = 0;
        var bySeverity = new Dictionary<ValidationSeverity, List<ValidationFinding>>();
        for (var i = 0; i < AllFindings.Count; i++)
        {
            var f = AllFindings[i];
            if (f.IsSafeBulkFix && f.HasQuickFix)
            {
                safeCount++;
            }

            switch (f.Severity)
            {
                case ValidationSeverity.Fatal:
                    FatalCount++;
                    break;
                case ValidationSeverity.Broken:
                    BrokenCount++;
                    break;
                case ValidationSeverity.CrossPlatform:
                    CrossPlatformCount++;
                    break;
                case ValidationSeverity.Style:
                    StyleCount++;
                    break;
            }

            if (!bySeverity.TryGetValue(f.Severity, out var bucket))
            {
                bucket = [];
                bySeverity[f.Severity] = bucket;
            }

            bucket.Add(f);
        }

        ValidationGroups.Clear();
        ValidationSeverity[] severityOrder =
        [
            ValidationSeverity.Fatal,
            ValidationSeverity.Broken,
            ValidationSeverity.CrossPlatform,
            ValidationSeverity.Style,
        ];

        for (var s = 0; s < severityOrder.Length; s++)
        {
            var severity = severityOrder[s];
            if (ActiveValidationSeverityFilter.HasValue && ActiveValidationSeverityFilter.Value != severity)
            {
                continue;
            }

            if (!bySeverity.TryGetValue(severity, out var findings) || findings.Count == 0)
            {
                continue;
            }

            var sevGroup = new ValidationSeverityGroupViewModel(severity, findings.Count);
            var rootMap = new Dictionary<string, ValidationRootGroupViewModel>(StringComparer.Ordinal);

            for (var i = 0; i < findings.Count; i++)
            {
                var finding = findings[i];
                if (!rootMap.TryGetValue(finding.RootName, out var rootGroup))
                {
                    rootGroup = new ValidationRootGroupViewModel(finding.RootName);
                    rootMap[finding.RootName] = rootGroup;
                    sevGroup.Roots.Add(rootGroup);
                }

                ValidationFileGroupViewModel? fileGroup = null;
                for (var fIdx = 0; fIdx < rootGroup.Files.Count; fIdx++)
                {
                    if (string.Equals(rootGroup.Files[fIdx].RelativeFile, finding.RelativeFile, StringComparison.Ordinal))
                    {
                        fileGroup = rootGroup.Files[fIdx];
                        break;
                    }
                }

                if (fileGroup == null)
                {
                    fileGroup = new ValidationFileGroupViewModel(finding.RelativeFile, startExpanded: ActiveValidationSeverityFilter.HasValue);
                    rootGroup.Files.Add(fileGroup);
                }

                fileGroup.Findings.Add(finding);
            }

            ValidationGroups.Add(sevGroup);
        }

        SafeFixCount = safeCount;
        OnPropertyChanged(nameof(HasSafeFixes));
        OnPropertyChanged(nameof(SafeFixButtonLabel));
        OnPropertyChanged(nameof(StatusSummaryLabel));
        OnPropertyChanged(nameof(HasNoValidationFindings));
        OnPropertyChanged(nameof(ShowValidationEmptyState));
        OnPropertyChanged(nameof(ShowValidationCleanBadge));
        OnPropertyChanged(nameof(ValidationEmptyStateMessage));
    }

    private void RemoveFindingFromLiveList(ValidationFinding finding)
    {
        for (var i = AllFindings.Count - 1; i >= 0; i--)
        {
            var item = AllFindings[i];
            if (string.Equals(item.Id, finding.Id, StringComparison.Ordinal)
                && string.Equals(item.RelativeFile, finding.RelativeFile, StringComparison.OrdinalIgnoreCase))
            {
                AllFindings.RemoveAt(i);
            }
        }

        RebuildValidationGroups();
    }

    private List<CommandPaletteEntry> BuildCommandPaletteEntries()
    {
        var entries = new List<CommandPaletteEntry>();
        for (var i = 0; i < _allDocuments.Count; i++)
        {
            var doc = _allDocuments[i];
            var resolution = doc.HasSyntaxError ? "Syntax Error" : (doc.IsDirty ? "Dirty Draft" : "Resolved");
            entries.Add(new CommandPaletteEntry(
                Title: doc.Id,
                Subtitle: $"{doc.Kind} · {doc.RelativeFile}",
                Category: "Assets",
                Kind: doc.Kind,
                TargetAssetId: doc.Id,
                ActionId: null,
                Provenance: doc.Provenance,
                ResolutionLabel: resolution,
                ShadowedFile: doc.ShadowedRoot != null ? $"{doc.ShadowedRoot}/{doc.RelativeFile}" : null,
                Score: 100 - i));
        }

        for (var i = 0; i < _candidates.Count && i < 8; i++)
        {
            var cand = _candidates[i];
            entries.Add(new CommandPaletteEntry(
                Title: cand.Value,
                Subtitle: $"{cand.Kind} · {cand.Detail}",
                Category: "Resources",
                Kind: null,
                TargetAssetId: null,
                ActionId: $"resource:{cand.Value}",
                Provenance: cand.Provenance,
                ResolutionLabel: cand.ResolutionState.ToString(),
                ShadowedFile: null,
                Score: 50 - i));
        }

        entries.Add(new CommandPaletteEntry("New Asset...", "Create a new asset or add to an existing file", "Actions", null, null, "action:new-asset", null, "Command", null, 85));
        if (SelectedAsset != null && SelectedAsset.Kind == AssetKind.Item && !SelectedAsset.HasSyntaxError)
        {
            entries.Add(new CommandPaletteEntry("Edit Item View Model Visually", "Open first-person 3D transform and FOV editor for this item", "Actions", null, null, "action:edit-view-model", null, "Command", null, 84));
            entries.Add(new CommandPaletteEntry("Edit Item World Model Visually", "Open 3D World Model editor with 1 m grid & 1.8 m body scale", "Actions", null, null, "action:edit-world-model", null, "Command", null, 83));
        }

        entries.Add(new CommandPaletteEntry("Revalidate Workspace", "Run validation checks across all workspace roots", "Actions", null, null, "action:revalidate", null, "Command", null, 80));
        entries.Add(new CommandPaletteEntry("Export Pack Archive...", "Validate and export a pack archive", "Actions", null, null, "action:export", null, "Command", null, 78));
        entries.Add(new CommandPaletteEntry("Cycle Theme (System / Light / Dark)", "Switch studio theme immediately", "Actions", null, null, "action:theme", null, "Command", null, 76));
        entries.Add(new CommandPaletteEntry("Open Settings", "Configure theme, namespace, language, renderer, and log level", "Actions", null, null, "action:settings", null, "Command", null, 74));
        entries.Add(new CommandPaletteEntry("Draft Recovery...", "Inspect unsaved recovery snapshots", "Actions", null, null, "action:recovery", null, "Command", null, 72));
        return entries;
    }

    private void HandlePaletteExecute(CommandPaletteEntry entry, bool openShadowed)
    {
        CloseAllOverlays();
        if (entry.TargetAssetId != null)
        {
            SelectAsset(entry.Kind, entry.TargetAssetId, relativeFile: null, pushHistory: true);
            if (openShadowed && entry.ShadowedFile != null)
            {
                ShowToast($"Opened shadowed base source: {entry.ShadowedFile}");
            }

            return;
        }

        switch (entry.ActionId)
        {
            case "action:new-asset":
                OpenNewAssetDialog();
                break;
            case "action:edit-view-model":
                OpenViewModelEditor();
                break;
            case "action:edit-world-model":
                OpenWorldModelEditor();
                break;
            case "action:revalidate":
                RevalidateWorkspace();
                ShowToast("Workspace revalidated.");
                break;
            case "action:export":
                OpenExportDialog();
                break;
            case "action:theme":
                ToggleTheme();
                break;
            case "action:settings":
                OpenSettingsDialog();
                break;
            case "action:recovery":
                OpenDraftRecoveryDialog();
                break;
            default:
                ShowToast($"Selected resource {entry.Title}");
                break;
        }
    }

    private void HandleCreateAsset(NewAssetRequest request)
    {
        CloseAllOverlays();
        if (_workspaceService.UsesRealBackend)
        {
            var created = _workspaceService.CreateAssetDocument(request);
            ShowToast(created.Message);
            if (created.Success)
            {
                OpenWorkspace(WorkspaceMode, _rootPath, WorkspaceName, showWorkbench: true);
                SelectAsset(request.Kind, request.AssetId, request.TargetRelativeFile, pushHistory: true);
            }

            return;
        }

        var seed = new StudioAssetSeed
        {
            Id = request.AssetId,
            Kind = request.Kind,
            GroupTitle = request.Kind switch
            {
                AssetKind.Brick => "Bricks",
                AssetKind.Item => "Items",
                AssetKind.Paint => "Paints",
                AssetKind.Recipe => "Recipes",
                AssetKind.Skill => "Skills",
                AssetKind.Body => "Bodies",
                AssetKind.Material => "Materials",
                AssetKind.Tag => "Tags",
                AssetKind.Localization => "Localization",
                _ => "Blueprints",
            },
            RelativeFile = request.TargetRelativeFile,
            RootName = WorkspaceMode == WorkspaceMode.Repo ? "repo" : WorkspaceName,
            Provenance = WorkspaceMode == WorkspaceMode.Repo ? AssetProvenance.Repo : AssetProvenance.Pack,
            MultiEntryLabel = request.UseExistingFile ? $"Appended to {Path.GetFileName(request.TargetRelativeFile)}" : null,
            IsDirty = true,
            ScalarValues = new Dictionary<string, string>
            {
                ["Shape"] = "Block",
                ["Color"] = "#5A7D9A",
                ["Alpha"] = "Opaque",
                ["CastShadows"] = "true",
                ["Passable"] = "false",
                ["TextureDefault"] = "panel.0",
                ["Name"] = $"asset.{request.AssetId.Replace(':', '.')}",
            },
        };

        var doc = CreateDocumentViewModel(seed, WorkspaceMode, forceClean: false);
        _allDocuments.Add(doc);
        RebuildAssetTree();
        SelectDocument(doc, pushHistory: true);
        UpdateDirtyCount();
        ShowToast($"Created {request.Kind} '{request.AssetId}' in {request.TargetRelativeFile}.");
    }

    private void HandleApplyRename(string newId)
    {
        CloseAllOverlays();
        if (SelectedAsset == null)
        {
            return;
        }

        var oldId = SelectedAsset.Id;
        var kind = SelectedAsset.Kind;
        if (_workspaceService.UsesRealBackend)
        {
            var result = _workspaceService.ApplyRenameToDrafts(oldId, newId, kind);
            ShowToast(result.Message);
            if (result.Success)
            {
                OpenWorkspace(WorkspaceMode, _rootPath, WorkspaceName, showWorkbench: true);
                SelectAsset(kind, newId, relativeFile: null, pushHistory: true);
            }

            return;
        }

        SelectedAsset.Id = newId;
        SelectedAsset.IsDirty = true;
        RebuildAssetTree();
        UpdateDirtyCount();
        ShowToast($"Renamed '{oldId}' → '{newId}' in workspace drafts.");
    }

    private void HandleConfirmDeleteAsset(AssetDocumentViewModel target)
    {
        CloseAllOverlays();
        var result = _workspaceService.DeleteAsset(
            target.Kind,
            target.Id,
            target.AbsoluteFilePath,
            target.TableIndex,
            target.EntryIndex);
        if (!result.Success)
        {
            ShowToast(result.Message);
            return;
        }

        var removedIdx = _allDocuments.IndexOf(target);
        if (removedIdx >= 0)
        {
            DetachDocumentEvents(target);
            _allDocuments.RemoveAt(removedIdx);
        }

        _undoStack.Clear();
        RebuildAssetTree();
        UpdateDirtyCount();
        RevalidateWorkspace();

        if (_allDocuments.Count > 0)
        {
            var nextIdx = Math.Clamp(removedIdx, 0, _allDocuments.Count - 1);
            SelectDocument(_allDocuments[nextIdx], pushHistory: true);
        }
        else
        {
            SelectedAsset = null;
        }

        if (ActiveModelEditor != null && ReferenceEquals(ActiveModelEditor.Document, target))
        {
            DisposeActiveModelEditor();
        }

        ShowToast(result.Message);
    }

    private void HandleCreateLocalizationKey(string keyName, string targetFile, string initialText)
    {
        CloseAllOverlays();
        string? createdAbsolutePath = null;
        if (_workspaceService.UsesRealBackend)
        {
            var created = _workspaceService.CreateLocalizationKey(keyName, targetFile, initialText);
            if (!created.Success)
            {
                ShowToast(created.Message);
                return;
            }

            createdAbsolutePath = created.Detail;
        }

        _candidates.Add(new ReferenceCandidate(
            keyName,
            ReferenceKind.Localization,
            WorkspaceMode == WorkspaceMode.Repo ? AssetProvenance.Repo : AssetProvenance.Pack,
            ReferenceResolutionState.Resolved,
            WorkspaceName,
            initialText));

        var normalizedTargetFile = (targetFile ?? string.Empty).Trim().Replace('\\', '/').TrimStart('/');
        var targetId = normalizedTargetFile.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
            ? normalizedTargetFile.Substring("assets/".Length)
            : normalizedTargetFile;

        var langDoc = FindExactLocalizationDocument(targetId, normalizedTargetFile, createdAbsolutePath);
        if (_workspaceService.UsesRealBackend)
        {
            if (langDoc != null)
            {
                string? updatedSha = null;
                string? updatedText = null;
                var diskPath = !string.IsNullOrWhiteSpace(createdAbsolutePath)
                    ? createdAbsolutePath
                    : langDoc.AbsoluteFilePath;
                if (!string.IsNullOrWhiteSpace(diskPath) && File.Exists(diskPath))
                {
                    var bytes = File.ReadAllBytes(diskPath);
                    updatedSha = ToolsStorage.ContentHash.ComputeSha256Hex(bytes);
                    updatedText = File.ReadAllText(diskPath);
                }

                langDoc.AddPersistedLocalizationEntry(keyName, initialText, updatedSha, updatedText);
            }
        }
        else
        {
            langDoc?.AddLocalizationEntry(keyName, initialText);
        }

        if (_pendingLocTargetField != null)
        {
            _pendingLocTargetField.Value = keyName;
            _pendingLocTargetField.SetValidationState(false, null);
            _pendingLocTargetField = null;
        }

        UpdateDirtyCount();
        RevalidateWorkspace();
        ShowToast($"Created key '{keyName}' in {targetFile}.");
    }

    private void HandleBlueprintAction(AssetDocumentViewModel doc, string action, string? argument)
    {
        var result = _workspaceService.ExecuteBlueprintAction(action, doc.Id, argument);
        ShowToast(result.Message);
        if (result.Success && (action == "duplicate" || action == "delete" || action == "move"))
        {
            OpenWorkspace(WorkspaceMode, _rootPath, WorkspaceName, showWorkbench: true);
        }
    }

    private void HandleSaveSettings(StudioSettingsState updated)
    {
        CloseAllOverlays();

        _isInitializingSettings = true;
        try
        {
            ThemePreference = updated.Theme;
            UiScale = ToolsSettings.ToolSettings.ClampUiScale(updated.UiScale);
            _lastPersistedUiScale = UiScale;
        }
        finally
        {
            _isInitializingSettings = false;
        }

        _defaultNamespace = !string.IsNullOrWhiteSpace(updated.DefaultNamespace)
            ? updated.DefaultNamespace.Trim().TrimEnd(':').Trim()
            : (!string.IsNullOrWhiteSpace(_packNamespace) ? _packNamespace : "wb");
        OnPropertyChanged(nameof(DefaultNamespace));

        var normalized = updated with
        {
            IsDarkTheme = ThemePreference == ToolsSettings.ToolTheme.Dark,
            Theme = ThemePreference,
            UiScale = UiScale,
        };
        var result = _workspaceService.SaveSettings(normalized);
        ShowToast(result.Message);
    }

    private void PersistQuickSettings()
    {
        var current = _workspaceService.GetSettings();
        var updated = current with
        {
            IsDarkTheme = ThemePreference == ToolsSettings.ToolTheme.Dark,
            Theme = ThemePreference,
            UiScale = UiScale,
            DefaultNamespace = _defaultNamespace,
        };
        _workspaceService.SaveSettings(updated);
    }

    private void HandleResetPanelLayout()
    {
        IsExplorerVisible = true;
        IsInspectorVisible = true;
        IsValidationDockVisible = true;
        ShowToast("Panel layout reset to defaults.");
    }

    private void HandleExportPack(ExportPackRequest request)
    {
        CloseAllOverlays();
        var result = _workspaceService.ExportPack(request);
        ShowToast(result.Message);
    }

    private void HandleDocumentMutated(AssetDocumentViewModel doc)
    {
        if (!_allDocuments.Contains(doc))
        {
            return;
        }

        if (_workspaceService.UsesRealBackend)
        {
            if (doc.IsDirty)
            {
                _workspaceService.SaveDraft(doc.BuildSavePayload());
            }
            else if (!HasOtherDirtyDocumentForFile(doc, doc.AbsoluteFilePath))
            {
                _workspaceService.DiscardDraft(doc.AbsoluteFilePath);
            }
        }

        UpdateDirtyCount();
        OnPropertyChanged(nameof(ShowConnectedShowcaseToggle));
        if (!ShowConnectedShowcaseToggle && IsConnectedShowcaseMode)
        {
            IsConnectedShowcaseMode = false;
        }

        if (!_workspaceService.UsesRealBackend)
        {
            RevalidateWorkspace();
        }

        UpdatePreviewDescriptor();
        if (ActiveModelEditor != null && ReferenceEquals(ActiveModelEditor.Document, doc))
        {
            ActiveModelEditor.RefreshFromDocument();
        }
    }

    private void HandleDocumentCreateLocKeyRequested(AssetDocumentViewModel doc, StudioFieldViewModel field)
    {
        OpenCreateLocalizationKeyDialog(field);
    }

    private void HandleDocumentBrowseTextureFileRequested(AssetDocumentViewModel doc, StudioFieldViewModel field)
    {
        _ = BrowseFileForFieldAsync(doc, field);
    }

    private async Task BrowseFileForFieldAsync(AssetDocumentViewModel doc, StudioFieldViewModel field)
    {
        if (_pathPicker == null || doc.IsReadOnly || field.IsReadOnly)
        {
            return;
        }

        try
        {
            if (field.ReferenceKind == ReferenceKind.Mesh)
            {
                var defaultMeshesDir = !string.IsNullOrWhiteSpace(_rootPath)
                    ? Path.Combine(_rootPath, "assets", "meshes")
                    : null;
                var startDir = !string.IsNullOrWhiteSpace(_lastMeshPickerDirectory) && Directory.Exists(_lastMeshPickerDirectory)
                    ? _lastMeshPickerDirectory
                    : (!string.IsNullOrWhiteSpace(defaultMeshesDir) && Directory.Exists(defaultMeshesDir)
                        ? defaultMeshesDir
                        : (!string.IsNullOrWhiteSpace(_rootPath) && Directory.Exists(_rootPath) ? _rootPath : null));

                var pickedMeshPath = await _pathPicker.PickMeshFileAsync("Select Mesh (.obj)", startDir);
                if (string.IsNullOrWhiteSpace(pickedMeshPath))
                {
                    return;
                }

                var pickedMeshDir = Path.GetDirectoryName(pickedMeshPath);
                if (!string.IsNullOrWhiteSpace(pickedMeshDir))
                {
                    _lastMeshPickerDirectory = pickedMeshDir;
                }

                field.Value = _workspaceService.ResolveMeshPathForWorkspace(pickedMeshPath);
                return;
            }

            var texStartDir = !string.IsNullOrWhiteSpace(_lastSpritePickerDirectory) && Directory.Exists(_lastSpritePickerDirectory)
                ? _lastSpritePickerDirectory
                : (!string.IsNullOrWhiteSpace(_rootPath) && Directory.Exists(_rootPath) ? _rootPath : null);

            var pickedPath = await _pathPicker.PickImageFileAsync("Select Texture Image", texStartDir);
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            var pickedDir = Path.GetDirectoryName(pickedPath);
            if (!string.IsNullOrWhiteSpace(pickedDir))
            {
                _lastSpritePickerDirectory = pickedDir;
            }

            var resolved = field.ReferenceKind == ReferenceKind.BrickFace
                ? _workspaceService.ResolveBlockLayerNameForWorkspace(pickedPath)
                : _workspaceService.ResolveImagePathForWorkspace(pickedPath);

            field.Value = resolved;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed browsing file for '{field.FieldKey}': {ex.Message}");
            ShowToast($"Failed picking file: {ex.Message}");
        }
    }

    private void UpdateDirtyCount()
    {
        var count = 0;
        for (var i = 0; i < _allDocuments.Count; i++)
        {
            if (_allDocuments[i].IsDirty)
            {
                count++;
            }
        }

        DirtyCount = count;
        OnPropertyChanged(nameof(CanSaveCurrentAsset));
        OnPropertyChanged(nameof(CanDiscardCurrentAsset));
    }

    private void RebuildBodyPreviewFrames(bool syncToSelectedState)
    {
        var prevState = _bodyPreviewFrames.Count > 0 && BodyPreviewFrameIndex >= 0 && BodyPreviewFrameIndex < _bodyPreviewFrames.Count
            ? _bodyPreviewFrames[BodyPreviewFrameIndex].State
            : null;
        var prevDir = _bodyPreviewFrames.Count > 0 && BodyPreviewFrameIndex >= 0 && BodyPreviewFrameIndex < _bodyPreviewFrames.Count
            ? _bodyPreviewFrames[BodyPreviewFrameIndex].Direction
            : null;

        _bodyPreviewFrames.Clear();
        var doc = SelectedAsset;
        if (doc != null && doc.Kind == AssetKind.Body)
        {
            string[] canonicalDirs = ["front", "back", "left", "right"];
            var visited = new HashSet<BodySpriteSlotViewModel>();

            for (var s = 0; s < doc.BodyStates.Count; s++)
            {
                var stateName = doc.BodyStates[s];
                for (var d = 0; d < canonicalDirs.Length; d++)
                {
                    var dir = canonicalDirs[d];
                    for (var i = 0; i < doc.AllBodySprites.Count; i++)
                    {
                        var slot = doc.AllBodySprites[i];
                        if (string.Equals(slot.StateName, stateName, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(slot.Direction, dir, StringComparison.OrdinalIgnoreCase)
                            && visited.Add(slot))
                        {
                            _bodyPreviewFrames.Add(new BodyPreviewFrameItem(
                                slot.StateName,
                                slot.Direction,
                                slot.TexturePath,
                                slot.ResolvedAbsolutePath ?? ResolveTextureAbsolutePathForDocument(slot.TexturePath),
                                slot.SwatchHex));
                        }
                    }
                }

                for (var i = 0; i < doc.AllBodySprites.Count; i++)
                {
                    var slot = doc.AllBodySprites[i];
                    if (string.Equals(slot.StateName, stateName, StringComparison.OrdinalIgnoreCase)
                        && visited.Add(slot))
                    {
                        _bodyPreviewFrames.Add(new BodyPreviewFrameItem(
                            slot.StateName,
                            slot.Direction,
                            slot.TexturePath,
                            slot.ResolvedAbsolutePath ?? ResolveTextureAbsolutePathForDocument(slot.TexturePath),
                            slot.SwatchHex));
                    }
                }
            }

            // Include any stray sprite slots whose state was not in BodyStates.
            for (var i = 0; i < doc.AllBodySprites.Count; i++)
            {
                var slot = doc.AllBodySprites[i];
                if (visited.Add(slot))
                {
                    _bodyPreviewFrames.Add(new BodyPreviewFrameItem(
                        slot.StateName,
                        slot.Direction,
                        slot.TexturePath,
                        slot.ResolvedAbsolutePath ?? ResolveTextureAbsolutePathForDocument(slot.TexturePath),
                        slot.SwatchHex));
                }
            }
        }

        var nextIndex = 0;
        if (_bodyPreviewFrames.Count > 0)
        {
            if (syncToSelectedState && doc != null && !string.IsNullOrWhiteSpace(doc.SelectedBodyState))
            {
                var matchIdx = FindBodyFrameIndex(doc.SelectedBodyState, null);
                if (matchIdx >= 0)
                {
                    nextIndex = matchIdx;
                }
            }
            else if (prevState != null)
            {
                var exactIdx = FindBodyFrameIndex(prevState, prevDir);
                if (exactIdx >= 0)
                {
                    nextIndex = exactIdx;
                }
                else
                {
                    nextIndex = Math.Clamp(BodyPreviewFrameIndex, 0, _bodyPreviewFrames.Count - 1);
                }
            }
        }

        _isRebuildingBodyFrames = true;
        try
        {
            BodyPreviewFrameIndex = nextIndex;
        }
        finally
        {
            _isRebuildingBodyFrames = false;
        }

        OnPropertyChanged(nameof(BodyPreviewFrameCount));
        OnPropertyChanged(nameof(BodyPreviewMaxIndex));
        OnPropertyChanged(nameof(ShowBodyPreviewControls));
        OnPropertyChanged(nameof(CanStepBodyPreviewFrames));
        OnPropertyChanged(nameof(BodyPreviewLabel));
    }

    private int FindBodyFrameIndex(string stateName, string? direction)
    {
        for (var i = 0; i < _bodyPreviewFrames.Count; i++)
        {
            var frame = _bodyPreviewFrames[i];
            if (!string.Equals(frame.State, stateName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (direction == null || string.Equals(frame.Direction, direction, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void UpdatePreviewDescriptor()
    {
        var doc = SelectedAsset;
        if (doc == null)
        {
            ActivePreviewDescriptor = new PreviewRenderDescriptor(
                PreviewSurfaceMode.DataDocument,
                "No Asset Selected",
                "Select an entry in the explorer",
                "#4F8CFF",
                "Block",
                PreviewOrientation,
                false,
                IsPreviewOrbiting,
                PreviewOrbitAngle,
                "standing",
                "front",
                "");
            IsLiveGlViewportActive = false;
            OnPropertyChanged(nameof(Has3DPreviewControls));
            OnPropertyChanged(nameof(IsDataDocumentPreview));
            OnPropertyChanged(nameof(IsRecipePreview));
            OnPropertyChanged(nameof(HasVisualPreview));
            OnPropertyChanged(nameof(ShouldMountGlViewport));
            return;
        }

        var shape = doc.BrickShapeField?.Value ?? "Block";
        var effectiveConnectedShowcase = IsConnectedShowcaseMode && doc.Kind == AssetKind.Brick && doc.IsBrickConnected;
        var mode = _previewProvider is StudioGlPreviewController glController
            ? glController.ResolveSurfaceMode(doc.Kind, IsShapeGridMode ? "Any" : shape, UseItemWorldModelPreset, effectiveConnectedShowcase)
            : (effectiveConnectedShowcase
                ? PreviewSurfaceMode.BrickConnectedShowcase
                : _previewProvider.ResolveSurfaceMode(doc.Kind, IsShapeGridMode ? "Any" : shape, UseItemWorldModelPreset));

        var accent = doc.BrickColorField?.Value
            ?? doc.PaintColorField?.Value
            ?? (doc.Kind == AssetKind.Body ? "#5282B8" : "#4F8CFF");
        var missingTex = doc.Kind == AssetKind.Brick && doc.BrickTexDefaultField != null && doc.BrickTexDefaultField.IsDanger;

        string? bodySpritePath = null;
        var spriteState = doc.SelectedBodyState;
        var spriteDirection = "front";
        if (doc.Kind == AssetKind.Body)
        {
            if (_bodyPreviewFrames.Count > 0)
            {
                var frameIdx = Math.Clamp(BodyPreviewFrameIndex, 0, _bodyPreviewFrames.Count - 1);
                var frame = _bodyPreviewFrames[frameIdx];
                spriteState = frame.State;
                spriteDirection = frame.Direction;
                accent = frame.SwatchHex;
                bodySpritePath = frame.AbsolutePath ?? ResolveTextureAbsolutePathForDocument(frame.TexturePath);
            }
            else
            {
                bodySpritePath = doc.ResolvedBodyFrontTexturePath;
            }
        }

        string? materialTexturePath = null;
        if (doc.Kind == AssetKind.Material)
        {
            var firstTex = ExtractFirstTextureEntry(doc.MaterialTexturesField?.Value);
            materialTexturePath = ResolveTextureAbsolutePathForDocument(firstTex) ?? doc.ResolvedMaterialTexturePath;
        }

        var isTexturePaint = doc.Kind == AssetKind.Paint && doc.IsPaintTextureKind;
        string? paintTexturePath = null;
        string? paintFirstVariant = null;
        if (isTexturePaint)
        {
            paintFirstVariant = ExtractFirstTextureEntry(doc.PaintVariantsField?.Value);
            var connected = doc.PaintConnectedField?.BoolValue ?? false;
            paintTexturePath = ResolveBlockAlbedoLayerPathForDocument(paintFirstVariant, connected);
            if (string.IsNullOrWhiteSpace(paintTexturePath))
            {
                var seedVariants = doc.InitialSeed.ScalarValues.TryGetValue("Variants", out var initialVar)
                    ? ExtractFirstTextureEntry(initialVar)
                    : null;
                if (string.IsNullOrWhiteSpace(paintFirstVariant)
                    || string.Equals(paintFirstVariant, seedVariants, StringComparison.OrdinalIgnoreCase))
                {
                    paintTexturePath = doc.ResolvedPaintTexturePath;
                }
            }
        }

        var subtitle = mode switch
        {
            PreviewSurfaceMode.BrickConnectedShowcase => $"Connected Block · {OrientationShortLabel}",
            PreviewSurfaceMode.BrickShapeGrid => $"All 6 Shapes · {OrientationShortLabel}",
            PreviewSurfaceMode.SingleBrick => $"{shape} · {OrientationShortLabel}",
            PreviewSurfaceMode.ItemViewModel => $"ViewModel · {OrientationShortLabel}",
            PreviewSurfaceMode.ItemWorldModel => "WorldModel · Unit Cube",
            PreviewSurfaceMode.PaintSwatch => isTexturePaint
                ? (string.IsNullOrWhiteSpace(paintFirstVariant) ? "Texture Paint" : $"Texture · {paintFirstVariant}")
                : accent,
            PreviewSurfaceMode.BodySprite => _bodyPreviewFrames.Count > 0 ? BodyPreviewLabel : $"{spriteState} · {spriteDirection}",
            PreviewSurfaceMode.MaterialPreview => $"{doc.MaterialAlphaModeField?.Value ?? "Opaque"}",
            PreviewSurfaceMode.BlueprintVoxel => doc.BlueprintSizeLabel,
            PreviewSurfaceMode.RecipeFlow => $"{doc.RecipeTargetTagLabel} · {doc.RecipeModeBadge}",
            _ => $"{doc.Kind} Document",
        };

        ActivePreviewDescriptor = new PreviewRenderDescriptor(
            mode,
            doc.Id,
            subtitle,
            accent,
            shape,
            PreviewOrientation,
            missingTex,
            IsPreviewOrbiting,
            PreviewOrbitAngle,
            spriteState,
            spriteDirection,
            doc.BlueprintSizeLabel,
            bodySpritePath,
            materialTexturePath,
            paintTexturePath,
            isTexturePaint,
            MaterialPreviewShape);
        OnPropertyChanged(nameof(Has3DPreviewControls));
        OnPropertyChanged(nameof(ShowOrientationButton));
        OnPropertyChanged(nameof(IsMaterialPreviewSelected));
        OnPropertyChanged(nameof(IsDataDocumentPreview));
        OnPropertyChanged(nameof(IsRecipePreview));
        OnPropertyChanged(nameof(HasVisualPreview));
        OnPropertyChanged(nameof(ShouldMountGlViewport));

        if (!ShouldMountGlViewport)
        {
            IsLiveGlViewportActive = false;
        }

        PreviewSceneSyncRequested?.Invoke();
    }

    private bool HasOtherDirtyDocumentForFile(AssetDocumentViewModel excludedDoc, string? absoluteFilePath)
    {
        if (string.IsNullOrWhiteSpace(absoluteFilePath))
        {
            return false;
        }

        for (var i = 0; i < _allDocuments.Count; i++)
        {
            var candidate = _allDocuments[i];
            if (!ReferenceEquals(candidate, excludedDoc)
                && candidate.IsDirty
                && string.Equals(candidate.AbsoluteFilePath, absoluteFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private AssetDocumentViewModel? FindExactLocalizationDocument(
        string normalizedTargetId,
        string normalizedTargetFile,
        string? createdAbsolutePath)
    {
        string? normalizedAbs = null;
        if (!string.IsNullOrWhiteSpace(createdAbsolutePath))
        {
            try
            {
                normalizedAbs = Path.GetFullPath(createdAbsolutePath);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed normalizing created localization path '{createdAbsolutePath}': {ex.Message}");
            }
        }

        for (var i = 0; i < _allDocuments.Count; i++)
        {
            var doc = _allDocuments[i];
            if (doc.Kind != AssetKind.Localization)
            {
                continue;
            }

            if (normalizedAbs != null && !string.IsNullOrWhiteSpace(doc.AbsoluteFilePath))
            {
                try
                {
                    if (string.Equals(Path.GetFullPath(doc.AbsoluteFilePath), normalizedAbs, StringComparison.OrdinalIgnoreCase))
                    {
                        return doc;
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Failed normalizing document path '{doc.AbsoluteFilePath}': {ex.Message}");
                }
            }

            var docRel = doc.RelativeFile.Replace('\\', '/').TrimStart('/');
            var docId = doc.Id.Replace('\\', '/').TrimStart('/');
            if ((normalizedTargetFile.Length > 0 && string.Equals(docRel, normalizedTargetFile, StringComparison.OrdinalIgnoreCase))
                || (normalizedTargetId.Length > 0 && string.Equals(docId, normalizedTargetId, StringComparison.OrdinalIgnoreCase)))
            {
                return doc;
            }
        }

        return null;
    }

    private string? ResolveTextureAbsolutePathForDocument(string? relativeTexturePath)
    {
        if (string.IsNullOrWhiteSpace(relativeTexturePath))
        {
            return null;
        }

        var trimmed = relativeTexturePath.Trim();
        var normalized = trimmed.Replace('\\', '/').TrimStart('/');
        var stripped = normalized.StartsWith("textures/", StringComparison.OrdinalIgnoreCase)
            ? normalized["textures/".Length..]
            : normalized;

        for (var i = 0; i < WorkspaceImages.Count; i++)
        {
            var img = WorkspaceImages[i];
            if ((string.Equals(img.RelativePath, normalized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(img.RelativePath, stripped, StringComparison.OrdinalIgnoreCase))
                && File.Exists(img.AbsolutePath))
            {
                return img.AbsolutePath;
            }
        }

        if (_previewProvider is StudioGlPreviewController glController)
        {
            var resolved = glController.ResolveTextureAbsolutePath(trimmed);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        for (var i = 0; i < _candidates.Count; i++)
        {
            var cand = _candidates[i];
            if (cand.Kind == ReferenceKind.Texture
                && !string.IsNullOrWhiteSpace(cand.ThumbnailPath)
                && (string.Equals(cand.Value, normalized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cand.Value, stripped, StringComparison.OrdinalIgnoreCase)))
            {
                return cand.ThumbnailPath;
            }
        }

        if (File.Exists(trimmed))
        {
            return Path.GetFullPath(trimmed);
        }

        return null;
    }

    private string? ResolveBlockAlbedoLayerPathForDocument(string? layerName, bool connected)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return null;
        }

        if (_previewProvider is StudioGlPreviewController glController)
        {
            var resolved = glController.ResolveBlockAlbedoLayerPath(layerName, connected);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        var trimmed = layerName.Trim().Replace('\\', '/');
        if (trimmed.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["textures/".Length..];
        }

        if (trimmed.StartsWith("block/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["block/".Length..];
        }

        if (trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        var variants = new List<string>(3) { trimmed };
        if (connected && !trimmed.EndsWith(".0", StringComparison.Ordinal))
        {
            variants.Insert(0, $"{trimmed}.0");
        }
        else if (!trimmed.EndsWith(".0", StringComparison.Ordinal))
        {
            variants.Add($"{trimmed}.0");
        }

        if (trimmed.EndsWith(".0", StringComparison.Ordinal) && trimmed.Length > 2)
        {
            variants.Add(trimmed[..^2]);
        }

        for (var v = 0; v < variants.Count; v++)
        {
            var candidateName = variants[v];
            for (var i = 0; i < _candidates.Count; i++)
            {
                var cand = _candidates[i];
                if (cand.Kind == ReferenceKind.BrickFace
                    && !string.IsNullOrWhiteSpace(cand.ThumbnailPath)
                    && string.Equals(cand.Value, candidateName, StringComparison.OrdinalIgnoreCase))
                {
                    return cand.ThumbnailPath;
                }
            }
        }

        return null;
    }

    private void ReplaceWorkspaceImages(IReadOnlyList<WorkspaceImageEntry> images)
    {
        WorkspaceImages.Clear();
        for (var i = 0; i < images.Count; i++)
        {
            WorkspaceImages.Add(images[i]);
        }
    }

    private static string SanitizePackSlug(string? raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        var trimmed = raw.Trim().TrimEnd(':').Trim().ToLowerInvariant();
        var chars = new List<char>(trimmed.Length);
        for (var i = 0; i < trimmed.Length; i++)
        {
            var ch = trimmed[i];
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_' || ch == '-')
            {
                chars.Add(ch);
            }
            else if (ch == ' ' || ch == '.')
            {
                chars.Add('_');
            }
        }

        var cleaned = new string([.. chars]).Trim('_', '-');
        return cleaned.Length > 0 ? cleaned : fallback;
    }

    private AssetDocumentViewModel? FindDocument(AssetKind? kind, string assetIdOrKey, string? relativeFile)
    {
        if (string.IsNullOrWhiteSpace(assetIdOrKey))
        {
            return null;
        }

        // 1. Exact DocumentKey match
        for (var i = 0; i < _allDocuments.Count; i++)
        {
            if (string.Equals(_allDocuments[i].DocumentKey, assetIdOrKey, StringComparison.OrdinalIgnoreCase))
            {
                return _allDocuments[i];
            }
        }

        // 2. Kind + ID + RelativeFile match
        if (kind.HasValue && !string.IsNullOrWhiteSpace(relativeFile))
        {
            for (var i = 0; i < _allDocuments.Count; i++)
            {
                var doc = _allDocuments[i];
                if (doc.Kind == kind.Value
                    && string.Equals(doc.Id, assetIdOrKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(doc.RelativeFile, relativeFile, StringComparison.OrdinalIgnoreCase))
                {
                    return doc;
                }
            }
        }

        // 3. Kind + ID match
        if (kind.HasValue)
        {
            for (var i = 0; i < _allDocuments.Count; i++)
            {
                var doc = _allDocuments[i];
                if (doc.Kind == kind.Value
                    && string.Equals(doc.Id, assetIdOrKey, StringComparison.OrdinalIgnoreCase))
                {
                    return doc;
                }
            }
        }

        // 4. RelativeFile + ID match
        if (!string.IsNullOrWhiteSpace(relativeFile))
        {
            for (var i = 0; i < _allDocuments.Count; i++)
            {
                var doc = _allDocuments[i];
                if (string.Equals(doc.Id, assetIdOrKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(doc.RelativeFile, relativeFile, StringComparison.OrdinalIgnoreCase))
                {
                    return doc;
                }
            }
        }

        // 5. Fallback: ID match
        for (var i = 0; i < _allDocuments.Count; i++)
        {
            if (string.Equals(_allDocuments[i].Id, assetIdOrKey, StringComparison.OrdinalIgnoreCase))
            {
                return _allDocuments[i];
            }
        }

        // 6. Fallback: RelativeFile match (for findings targeting a file without matching ID)
        if (!string.IsNullOrWhiteSpace(relativeFile))
        {
            for (var i = 0; i < _allDocuments.Count; i++)
            {
                if (string.Equals(_allDocuments[i].RelativeFile, relativeFile, StringComparison.OrdinalIgnoreCase))
                {
                    return _allDocuments[i];
                }
            }
        }

        return null;
    }

    private static StudioAssetSeed? FindMatchingSeed(
        IReadOnlyList<StudioAssetSeed> seeds,
        AssetKind kind,
        string id,
        string relativeFile,
        int entryIndex)
    {
        for (var i = 0; i < seeds.Count; i++)
        {
            var s = seeds[i];
            if (s.Kind == kind
                && string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.RelativeFile, relativeFile, StringComparison.OrdinalIgnoreCase))
            {
                return s;
            }
        }

        for (var i = 0; i < seeds.Count; i++)
        {
            var s = seeds[i];
            if (s.Kind == kind
                && string.Equals(s.RelativeFile, relativeFile, StringComparison.OrdinalIgnoreCase)
                && s.EntryIndex == entryIndex)
            {
                return s;
            }
        }

        return null;
    }

    private static string? ExtractFirstTextureEntry(string? rawList)
    {
        if (string.IsNullOrWhiteSpace(rawList))
        {
            return null;
        }

        var parts = rawList.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[0].Trim('"', '\'') : null;
    }

    private static string FormatSeverityLabel(ValidationSeverity severity)
    {
        return severity switch
        {
            ValidationSeverity.Fatal => "Fatal",
            ValidationSeverity.Broken => "Broken",
            ValidationSeverity.CrossPlatform => "Cross-platform",
            _ => "Style",
        };
    }

    private void ShowToast(string message)
    {
        ToastMessage = message;
        if (!string.IsNullOrWhiteSpace(message))
        {
            RestartToastAutoDismissTimer();
        }
    }

    private void RestartToastAutoDismissTimer()
    {
        StopToastTimer();
        if (_disposed || ToastAutoDismissDelay <= TimeSpan.Zero)
        {
            return;
        }

        var delayMs = Math.Max(1, (int)Math.Min(int.MaxValue, ToastAutoDismissDelay.TotalMilliseconds));
        using (_toastTimerLock.EnterScope())
        {
            if (_disposed)
            {
                return;
            }

            var generation = ++_toastGeneration;
            _toastTimer = new Timer(HandleToastTimerCallback, generation, delayMs, Timeout.Infinite);
        }
    }

    private void HandleToastTimerCallback(object? state)
    {
        if (state is not long generation || _disposed)
        {
            return;
        }

        using (_toastTimerLock.EnterScope())
        {
            if (_disposed || generation != _toastGeneration)
            {
                return;
            }
        }

        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                ClearToastIfGenerationMatches(generation);
            }
            else
            {
                Dispatcher.UIThread.Post(() => ClearToastIfGenerationMatches(generation));
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Clearing toast without UI dispatcher: {ex.Message}");
            ClearToastIfGenerationMatches(generation);
        }
    }

    private void ClearToastIfGenerationMatches(long generation)
    {
        using (_toastTimerLock.EnterScope())
        {
            if (_disposed || generation != _toastGeneration)
            {
                return;
            }
        }

        StopToastTimer();
        ToastMessage = null;
    }

    private void StopToastTimer()
    {
        Timer? timerToDispose;
        using (_toastTimerLock.EnterScope())
        {
            _toastGeneration++;
            timerToDispose = _toastTimer;
            _toastTimer = null;
        }

        timerToDispose?.Dispose();
    }
}
