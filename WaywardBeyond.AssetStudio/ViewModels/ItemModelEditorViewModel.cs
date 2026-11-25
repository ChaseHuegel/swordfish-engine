using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.Bricks;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>View model coordinating interactive 3D ViewModel and WorldModel editing for an item asset.</summary>
public sealed partial class ItemModelEditorViewModel : ObservableObject, IDisposable
{
    private const double MIN_FOV = 60.0;
    private const double MAX_FOV = 120.0;
    private const double MIN_POS = -2.0;
    private const double MAX_POS = 2.0;
    private const double MIN_ROT = -180.0;
    private const double MAX_ROT = 180.0;
    private const double MIN_SCALE = 0.05;
    private const double MAX_SCALE = 2.0;

    private static readonly IReadOnlyList<BrickShape> PREVIEW_SHAPES =
    [
        BrickShape.Block,
        BrickShape.Slab,
        BrickShape.Stair,
        BrickShape.Slope,
        BrickShape.Column,
        BrickShape.Plate,
    ];

    private readonly MainWindowViewModel _shell;
    private readonly StudioGlPreviewController? _glController;
    private readonly Action _onClose;
    private readonly bool _enableLiveGlViewport;
    private readonly StudioTransformClipboard _transformClipboard;
    private readonly Action<string>? _onStatus;
    private bool _disposed;

    private Vector3 _position;
    private Vector3 _rotationDeg;
    private Vector3 _scale = Vector3.One;
    private bool _suppressSync;
    private bool _isApplyingToDocument;

    private Vector3 _dragBeforePos;
    private Vector3 _dragBeforeRot;
    private Vector3 _dragBeforeScale;
    private bool _dragBeforeDirty;
    private HashSet<string>? _dragBeforeModifiedKeys;

    [ObservableProperty]
    private ItemModelEditorMode _mode;

    [ObservableProperty]
    private double _fovDegrees = 90.0;

    [ObservableProperty]
    private string _fovText = "90";

    [ObservableProperty]
    private ItemModelAspectPreset _aspectPreset = ItemModelAspectPreset.Ratio16x9;

    [ObservableProperty]
    private bool _showCrosshair = true;

    [ObservableProperty]
    private bool _showFloorPlane = true;

    [ObservableProperty]
    private bool _showBodyReference = true;

    [ObservableProperty]
    private TransformGizmoMode _gizmoMode = TransformGizmoMode.Translate;

    [ObservableProperty]
    private TransformGizmoSpace _gizmoSpace = TransformGizmoSpace.Local;

    [ObservableProperty]
    private bool _snapEnabled;

    [ObservableProperty]
    private bool _uniformScale = true;

    [ObservableProperty]
    private string? _activeDragReadout;

    [ObservableProperty]
    private BrickShape _selectedPreviewBrickShape = BrickShape.Block;

    [ObservableProperty]
    private bool _showBrickShapeSelector;

    [ObservableProperty]
    private string _modelSourceSummary = string.Empty;

    [ObservableProperty]
    private bool _showPlaceholderNotice;

    [ObservableProperty]
    private string _referenceBodyLabel = "No valid body sprite found";

    [ObservableProperty]
    private bool _hasReferenceBodySprite;

    [ObservableProperty]
    private bool _isLiveGlViewportActive;

    [ObservableProperty]
    private double _posX;

    [ObservableProperty]
    private double _posY;

    [ObservableProperty]
    private double _posZ;

    [ObservableProperty]
    private string _posXText = "0";

    [ObservableProperty]
    private string _posYText = "0";

    [ObservableProperty]
    private string _posZText = "0";

    [ObservableProperty]
    private double _rotX;

    [ObservableProperty]
    private double _rotY;

    [ObservableProperty]
    private double _rotZ;

    [ObservableProperty]
    private string _rotXText = "0";

    [ObservableProperty]
    private string _rotYText = "0";

    [ObservableProperty]
    private string _rotZText = "0";

    [ObservableProperty]
    private double _scaleX = 1.0;

    [ObservableProperty]
    private double _scaleY = 1.0;

    [ObservableProperty]
    private double _scaleZ = 1.0;

    [ObservableProperty]
    private string _scaleXText = "1";

    [ObservableProperty]
    private string _scaleYText = "1";

    [ObservableProperty]
    private string _scaleZText = "1";

    [ObservableProperty]
    private string? _statusText;

    /// <summary>Initializes a visual model editor session for <paramref name="document"/>.</summary>
    public ItemModelEditorViewModel(
        MainWindowViewModel shell,
        AssetDocumentViewModel document,
        ItemModelEditorMode initialMode,
        StudioGlPreviewController? glController,
        bool enableLiveGlViewport,
        Action onClose,
        StudioTransformClipboard? transformClipboard = null,
        Action<string>? onStatus = null)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _glController = glController;
        _enableLiveGlViewport = enableLiveGlViewport;
        _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));
        _transformClipboard = transformClipboard ?? StudioTransformClipboard.Shared;
        _onStatus = onStatus;
        _mode = initialMode;

        _transformClipboard.PropertyChanged += HandleTransformClipboardPropertyChanged;

        SyncTransformFromDocument();
        RebuildCurrentState();
    }

    /// <summary>Raised whenever the 3D scene state changes and needs synchronization into the viewport.</summary>
    public event Action? RenderStateChanged;

    /// <summary>Raised when the user clicks Reset Camera in WorldModel orbit mode.</summary>
    public event Action? ResetCameraRequested;

    /// <summary>Item document currently being edited visually.</summary>
    public AssetDocumentViewModel Document { get; }

    /// <summary>Latest snapshot of the full 3D render state for live GL and fallback controls.</summary>
    public ModelEditorRenderState CurrentRenderState { get; private set; } = ModelEditorRenderState.Default;

    /// <summary>Block albedo array shared with <see cref="ModelEditorViewport"/>.</summary>
    public AlbedoTextureArrayData? ActiveAlbedoArray => _glController?.ActiveAlbedoArray;

    /// <summary>True when the active item document is read-only.</summary>
    public bool IsReadOnly => Document.IsReadOnly;

    /// <summary>True when editing fields (inverted helper for XAML bindings).</summary>
    public bool IsEditable => !Document.IsReadOnly;

    /// <summary>True when the active item document allows transform edits.</summary>
    public bool CanEdit => !Document.IsReadOnly;

    /// <summary>True when a copied position vector can be pasted onto this item.</summary>
    public bool CanPastePosition => CanEdit && _transformClipboard.HasPosition;

    /// <summary>True when a copied rotation vector can be pasted onto this item.</summary>
    public bool CanPasteRotation => CanEdit && _transformClipboard.HasRotation;

    /// <summary>True when a copied scale vector can be pasted onto this item.</summary>
    public bool CanPasteScale => CanEdit && _transformClipboard.HasScale;

    /// <summary>True when a transient transform copy, paste, or reset status message is active.</summary>
    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    /// <summary>Unsubscribes from the shared transform clipboard when the editor session closes.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _transformClipboard.PropertyChanged -= HandleTransformClipboardPropertyChanged;
    }

    /// <summary>True when editing first-person <c>[Items.ViewModel]</c>.</summary>
    public bool IsViewModelMode => Mode == ItemModelEditorMode.ViewModel;

    /// <summary>True when editing third-person <c>[Items.WorldModel]</c>.</summary>
    public bool IsWorldModelMode => Mode == ItemModelEditorMode.WorldModel;

    /// <summary>Human-readable header title indicating active ViewModel or WorldModel editing mode.</summary>
    public string ModeDisplayTitle => IsViewModelMode ? "View Model Editor" : "World Model Editor";

    /// <summary>Tooltip and label for the cross-mode transform copy button.</summary>
    public string CopyFromOtherModelLabel => IsViewModelMode ? "Copy from World" : "Copy from View";

    /// <summary>Target width/height aspect ratio for <c>AspectRatioFrameControl</c> (0 fills full bounds).</summary>
    public double ActiveViewportAspectRatio => !IsViewModelMode
        ? 0.0
        : AspectPreset switch
        {
            ItemModelAspectPreset.Ratio16x9 => 16.0 / 9.0,
            ItemModelAspectPreset.Ratio16x10 => 16.0 / 10.0,
            ItemModelAspectPreset.Ratio4x3 => 4.0 / 3.0,
            _ => 0.0,
        };

    /// <summary>Compact top-right viewport overlay summary showing active camera/reference context.</summary>
    public string ViewportOverlayBadge
    {
        get
        {
            if (IsViewModelMode)
            {
                string aspectText = AspectPreset switch
                {
                    ItemModelAspectPreset.Ratio16x9 => "16:9",
                    ItemModelAspectPreset.Ratio16x10 => "16:10",
                    ItemModelAspectPreset.Ratio4x3 => "4:3",
                    _ => "Fill",
                };
                return string.Create(CultureInfo.InvariantCulture, $"{aspectText} · {Math.Round(FovDegrees):0}° FOV");
            }

            if (ShowFloorPlane && ShowBodyReference && HasReferenceBodySprite)
            {
                return "1.0 m Grid · 1.8 m Body";
            }

            if (ShowFloorPlane)
            {
                return "1.0 m Origin Grid";
            }

            return ShowBodyReference && HasReferenceBodySprite ? "1.8 m Body Scale" : "World Orbit";
        }
    }

    /// <summary>True when the ViewModel 16:9 aspect preset is selected.</summary>
    public bool IsRatio16x9 => AspectPreset == ItemModelAspectPreset.Ratio16x9;

    /// <summary>True when the ViewModel 16:10 aspect preset is selected.</summary>
    public bool IsRatio16x10 => AspectPreset == ItemModelAspectPreset.Ratio16x10;

    /// <summary>True when the ViewModel 4:3 aspect preset is selected.</summary>
    public bool IsRatio4x3 => AspectPreset == ItemModelAspectPreset.Ratio4x3;

    /// <summary>True when the ViewModel unconstrained Fill aspect preset is selected.</summary>
    public bool IsRatioFill => AspectPreset == ItemModelAspectPreset.Fill;

    /// <summary>True when the ViewModel center reticle overlay should display.</summary>
    public bool ShowCrosshairOverlay => IsViewModelMode && ShowCrosshair;

    /// <summary>True when the 60° FOV preset pill is highlighted.</summary>
    public bool IsFov60 => Math.Abs(FovDegrees - 60.0) < 0.5;

    /// <summary>True when the 75° FOV preset pill is highlighted.</summary>
    public bool IsFov75 => Math.Abs(FovDegrees - 75.0) < 0.5;

    /// <summary>True when the 90° FOV preset pill is highlighted.</summary>
    public bool IsFov90 => Math.Abs(FovDegrees - 90.0) < 0.5;

    /// <summary>True when the 110° FOV preset pill is highlighted.</summary>
    public bool IsFov110 => Math.Abs(FovDegrees - 110.0) < 0.5;

    /// <summary>True when the Translate gizmo tool is active.</summary>
    public bool IsTranslateMode => GizmoMode == TransformGizmoMode.Translate;

    /// <summary>True when the Rotate gizmo tool is active.</summary>
    public bool IsRotateMode => GizmoMode == TransformGizmoMode.Rotate;

    /// <summary>True when the Scale gizmo tool is active.</summary>
    public bool IsScaleMode => GizmoMode == TransformGizmoMode.Scale;

    /// <summary>True when the coordinate space toggle applies to the active gizmo mode.</summary>
    public bool ShowSpaceToggle => !IsScaleMode;

    /// <summary>True when the gizmo uses object-local axes.</summary>
    public bool IsLocalSpace => GizmoSpace == TransformGizmoSpace.Local;

    /// <summary>True when the gizmo uses world axes.</summary>
    public bool IsWorldSpace => GizmoSpace == TransformGizmoSpace.World;

    /// <summary>Label displaying the active gizmo coordinate space.</summary>
    public string GizmoSpaceLabel => IsLocalSpace ? "Local" : "World";

    /// <summary>True when an interactive gizmo drag readout badge should be shown.</summary>
    public bool HasActiveDragReadout => !string.IsNullOrWhiteSpace(ActiveDragReadout);

    /// <summary>Active Mesh field corresponding to ViewModel or WorldModel mode.</summary>
    public StudioFieldViewModel? ActiveMeshField =>
        IsViewModelMode ? Document.ItemViewModelMeshField : Document.ItemWorldModelMeshField;

    /// <summary>Active Material field corresponding to ViewModel or WorldModel mode.</summary>
    public StudioFieldViewModel? ActiveMaterialField =>
        IsViewModelMode ? Document.ItemViewModelMaterialField : Document.ItemWorldModelMaterialField;

    /// <summary>Selectable brick shapes offered when previewing a <c>BrickShape.Any</c> placeable item.</summary>
    public IReadOnlyList<BrickShape> PreviewBrickShapes => PREVIEW_SHAPES;

    /// <summary>True when the OpenGL control should be mounted in the visual tree.</summary>
    public bool ShowLiveGlViewport => _enableLiveGlViewport;

    /// <summary>True when the 2D software projected preview should render instead of live OpenGL.</summary>
    public bool ShowFallbackViewport => !ShowLiveGlViewport || !IsLiveGlViewportActive;

    /// <summary>Re-reads transform values and visual geometry after external updates (Undo/Redo, Mesh/Material edits).</summary>
    public void RefreshFromDocument()
    {
        if (_isApplyingToDocument)
        {
            return;
        }

        SyncTransformFromDocument();
        RebuildCurrentState();
    }

    /// <summary>Called when the live OpenGL viewport initializes cleanly so the 2D fallback can hide.</summary>
    public void ReportGlInitialized()
    {
        IsLiveGlViewportActive = true;
    }

    /// <summary>Called when OpenGL reports an initialization or draw failure to seamlessly activate software fallback.</summary>
    public void ReportGlFailed()
    {
        IsLiveGlViewportActive = false;
    }

    /// <summary>Begins an interactive 3D gizmo drag, recording a baseline snapshot for one atomic undo entry.</summary>
    public void BeginGizmoDrag()
    {
        if (IsReadOnly)
        {
            return;
        }

        _dragBeforePos = _position;
        _dragBeforeRot = _rotationDeg;
        _dragBeforeScale = _scale;
        _dragBeforeDirty = Document.IsDirty;

        string prefix = IsWorldModelMode ? "WorldModel" : "ViewModel";
        _dragBeforeModifiedKeys = new HashSet<string>(StringComparer.Ordinal);
        string[] keys = [$"{prefix}Position", $"{prefix}Rotation", $"{prefix}Scale"];
        for (var i = 0; i < keys.Length; i++)
        {
            if (Document.IsFieldModified(keys[i]))
            {
                _dragBeforeModifiedKeys.Add(keys[i]);
            }
        }
    }

    /// <summary>Applies live transform coordinates from an active 3D gizmo drag without spanning multiple undo steps.</summary>
    public void ApplyGizmoDragTransform(Vector3 position, Vector3 rotationDegrees, Vector3 scale, string? readout)
    {
        if (IsReadOnly)
        {
            return;
        }

        _position = position;
        _rotationDeg = rotationDegrees;
        _scale = scale;
        ActiveDragReadout = readout;

        PushVectorsToUiProperties();
        ApplyTransformToDocument(
            _position,
            _rotationDeg,
            _scale,
            $"Gizmo {GizmoMode} {ModeDisplayTitle}",
            coalesceKey: null,
            recordUndo: false);
        UpdateRenderStateOnly();
    }

    /// <summary>Completes an active 3D gizmo drag and pushes one atomic undo/redo action if any component changed.</summary>
    public void EndGizmoDrag(Vector3 finalPosition, Vector3 finalRotationDegrees, Vector3 finalScale)
    {
        ActiveDragReadout = null;
        if (IsReadOnly)
        {
            return;
        }

        _position = finalPosition;
        _rotationDeg = finalRotationDegrees;
        _scale = finalScale;

        PushVectorsToUiProperties();
        ApplyTransformToDocument(
            _position,
            _rotationDeg,
            _scale,
            $"Gizmo {GizmoMode} {ModeDisplayTitle}",
            coalesceKey: null,
            recordUndo: false);

        if (_dragBeforeModifiedKeys != null)
        {
            _isApplyingToDocument = true;
            try
            {
                Document.CommitItemModelTransformUndo(
                    IsWorldModelMode,
                    _dragBeforePos,
                    _dragBeforeRot,
                    _dragBeforeScale,
                    _position,
                    _rotationDeg,
                    _scale,
                    _dragBeforeDirty,
                    _dragBeforeModifiedKeys,
                    $"{GizmoMode} {(IsViewModelMode ? "ViewModel" : "WorldModel")}");
            }
            finally
            {
                _isApplyingToDocument = false;
            }
        }

        _dragBeforeModifiedKeys = null;
        UpdateRenderStateOnly();
    }

    partial void OnModeChanged(ItemModelEditorMode value)
    {
        OnPropertyChanged(nameof(IsViewModelMode));
        OnPropertyChanged(nameof(IsWorldModelMode));
        OnPropertyChanged(nameof(ModeDisplayTitle));
        OnPropertyChanged(nameof(CopyFromOtherModelLabel));
        OnPropertyChanged(nameof(ActiveViewportAspectRatio));
        OnPropertyChanged(nameof(ViewportOverlayBadge));
        OnPropertyChanged(nameof(ShowCrosshairOverlay));
        OnPropertyChanged(nameof(ActiveMeshField));
        OnPropertyChanged(nameof(ActiveMaterialField));

        SyncTransformFromDocument();
        RebuildCurrentState();
    }

    partial void OnFovDegreesChanged(double value)
    {
        double clamped = Math.Clamp(double.IsFinite(value) ? value : 90.0, MIN_FOV, MAX_FOV);
        if (Math.Abs(clamped - value) > 0.001)
        {
            FovDegrees = clamped;
            return;
        }

        if (!_suppressSync)
        {
            _suppressSync = true;
            try
            {
                FovText = clamped.ToString("0.#", CultureInfo.InvariantCulture);
            }
            finally
            {
                _suppressSync = false;
            }

            UpdateRenderStateOnly();
        }

        OnPropertyChanged(nameof(ViewportOverlayBadge));
        OnPropertyChanged(nameof(IsFov60));
        OnPropertyChanged(nameof(IsFov75));
        OnPropertyChanged(nameof(IsFov90));
        OnPropertyChanged(nameof(IsFov110));
    }

    partial void OnFovTextChanged(string value)
    {
        if (_suppressSync)
        {
            return;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) || !double.IsFinite(parsed))
        {
            return;
        }

        double clamped = Math.Clamp(parsed, MIN_FOV, MAX_FOV);
        _suppressSync = true;
        try
        {
            FovDegrees = clamped;
        }
        finally
        {
            _suppressSync = false;
        }

        OnPropertyChanged(nameof(ViewportOverlayBadge));
        OnPropertyChanged(nameof(IsFov60));
        OnPropertyChanged(nameof(IsFov75));
        OnPropertyChanged(nameof(IsFov90));
        OnPropertyChanged(nameof(IsFov110));
        UpdateRenderStateOnly();
    }

    partial void OnAspectPresetChanged(ItemModelAspectPreset value)
    {
        OnPropertyChanged(nameof(ActiveViewportAspectRatio));
        OnPropertyChanged(nameof(ViewportOverlayBadge));
        OnPropertyChanged(nameof(IsRatio16x9));
        OnPropertyChanged(nameof(IsRatio16x10));
        OnPropertyChanged(nameof(IsRatio4x3));
        OnPropertyChanged(nameof(IsRatioFill));
        UpdateRenderStateOnly();
    }

    partial void OnShowCrosshairChanged(bool value) => OnPropertyChanged(nameof(ShowCrosshairOverlay));

    partial void OnShowFloorPlaneChanged(bool value)
    {
        OnPropertyChanged(nameof(ViewportOverlayBadge));
        UpdateRenderStateOnly();
    }

    partial void OnShowBodyReferenceChanged(bool value)
    {
        OnPropertyChanged(nameof(ViewportOverlayBadge));
        UpdateRenderStateOnly();
    }

    partial void OnGizmoModeChanged(TransformGizmoMode value)
    {
        OnPropertyChanged(nameof(IsTranslateMode));
        OnPropertyChanged(nameof(IsRotateMode));
        OnPropertyChanged(nameof(IsScaleMode));
        OnPropertyChanged(nameof(ShowSpaceToggle));
        UpdateRenderStateOnly();
    }

    partial void OnGizmoSpaceChanged(TransformGizmoSpace value)
    {
        OnPropertyChanged(nameof(IsLocalSpace));
        OnPropertyChanged(nameof(IsWorldSpace));
        OnPropertyChanged(nameof(GizmoSpaceLabel));
        UpdateRenderStateOnly();
    }

    partial void OnSnapEnabledChanged(bool value) => UpdateRenderStateOnly();

    partial void OnUniformScaleChanged(bool value) => UpdateRenderStateOnly();

    partial void OnActiveDragReadoutChanged(string? value) => OnPropertyChanged(nameof(HasActiveDragReadout));

    partial void OnSelectedPreviewBrickShapeChanged(BrickShape value) => RebuildCurrentState();

    partial void OnIsLiveGlViewportActiveChanged(bool value) => OnPropertyChanged(nameof(ShowFallbackViewport));

    partial void OnStatusTextChanged(string? value) => OnPropertyChanged(nameof(HasStatusText));

    partial void OnPosXChanged(double value) => HandleSliderVectorComponent(0, 0, value);
    partial void OnPosYChanged(double value) => HandleSliderVectorComponent(0, 1, value);
    partial void OnPosZChanged(double value) => HandleSliderVectorComponent(0, 2, value);

    partial void OnPosXTextChanged(string value) => HandleTextVectorComponent(0, 0, value);
    partial void OnPosYTextChanged(string value) => HandleTextVectorComponent(0, 1, value);
    partial void OnPosZTextChanged(string value) => HandleTextVectorComponent(0, 2, value);

    partial void OnRotXChanged(double value) => HandleSliderVectorComponent(1, 0, value);
    partial void OnRotYChanged(double value) => HandleSliderVectorComponent(1, 1, value);
    partial void OnRotZChanged(double value) => HandleSliderVectorComponent(1, 2, value);

    partial void OnRotXTextChanged(string value) => HandleTextVectorComponent(1, 0, value);
    partial void OnRotYTextChanged(string value) => HandleTextVectorComponent(1, 1, value);
    partial void OnRotZTextChanged(string value) => HandleTextVectorComponent(1, 2, value);

    partial void OnScaleXChanged(double value) => HandleSliderVectorComponent(2, 0, value);
    partial void OnScaleYChanged(double value) => HandleSliderVectorComponent(2, 1, value);
    partial void OnScaleZChanged(double value) => HandleSliderVectorComponent(2, 2, value);

    partial void OnScaleXTextChanged(string value) => HandleTextVectorComponent(2, 0, value);
    partial void OnScaleYTextChanged(string value) => HandleTextVectorComponent(2, 1, value);
    partial void OnScaleZTextChanged(string value) => HandleTextVectorComponent(2, 2, value);

    [RelayCommand]
    private void Exit()
    {
        try
        {
            _onClose();
        }
        finally
        {
            Dispose();
        }
    }

    [RelayCommand]
    private void SelectViewModelMode() => Mode = ItemModelEditorMode.ViewModel;

    [RelayCommand]
    private void SelectWorldModelMode() => Mode = ItemModelEditorMode.WorldModel;

    [RelayCommand]
    private void SetGizmoMode(TransformGizmoMode mode) => GizmoMode = mode;

    [RelayCommand]
    private void SelectLocalSpace() => GizmoSpace = TransformGizmoSpace.Local;

    [RelayCommand]
    private void SelectWorldSpace() => GizmoSpace = TransformGizmoSpace.World;

    [RelayCommand]
    private void ToggleGizmoSpace() =>
        GizmoSpace = GizmoSpace == TransformGizmoSpace.Local ? TransformGizmoSpace.World : TransformGizmoSpace.Local;

    [RelayCommand]
    private void ToggleSnap() => SnapEnabled = !SnapEnabled;

    [RelayCommand]
    private void ToggleCrosshair() => ShowCrosshair = !ShowCrosshair;

    [RelayCommand]
    private void ToggleFloorPlane() => ShowFloorPlane = !ShowFloorPlane;

    [RelayCommand]
    private void ToggleBodyReference() => ShowBodyReference = !ShowBodyReference;

    [RelayCommand]
    private void ToggleUniformScale() => UniformScale = !UniformScale;

    [RelayCommand]
    private void SetFovPreset(string? presetDegrees)
    {
        if (double.TryParse(presetDegrees, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            && double.IsFinite(parsed))
        {
            FovDegrees = Math.Clamp(parsed, MIN_FOV, MAX_FOV);
        }
    }

    [RelayCommand]
    private void SetAspectPreset(ItemModelAspectPreset preset)
    {
        AspectPreset = preset;
    }

    [RelayCommand]
    private void ResetCamera() => ResetCameraRequested?.Invoke();

    [RelayCommand]
    private void ResetTransform()
    {
        if (IsReadOnly)
        {
            return;
        }

        _position = GetDefaultPosition();
        _rotationDeg = GetDefaultRotation();
        _scale = GetDefaultScale();
        CommitTransformUpdate($"Reset {ModeName} transform", $"Reset {ModeName} transform to defaults");
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void ResetPosition()
    {
        if (IsReadOnly)
        {
            return;
        }

        _position = GetDefaultPosition();
        CommitTransformUpdate(
            $"Reset {ModeName} Position",
            $"Reset Position to ({ModelTransformMath.FormatFloat3(_position)})");
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void ResetRotation()
    {
        if (IsReadOnly)
        {
            return;
        }

        _rotationDeg = GetDefaultRotation();
        CommitTransformUpdate(
            $"Reset {ModeName} Rotation",
            $"Reset Rotation to ({ModelTransformMath.FormatFloat3(_rotationDeg)}°)");
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void ResetScale()
    {
        if (IsReadOnly)
        {
            return;
        }

        _scale = GetDefaultScale();
        CommitTransformUpdate(
            $"Reset {ModeName} Scale",
            $"Reset Scale to ({ModelTransformMath.FormatFloat3(_scale)})");
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void ResetAxis(string? axisKey)
    {
        if (IsReadOnly || string.IsNullOrWhiteSpace(axisKey))
        {
            return;
        }

        (int groupIndex, int axisIndex, string label) = axisKey.Trim().ToUpperInvariant() switch
        {
            "POSX" => (0, 0, "Position X"),
            "POSY" => (0, 1, "Position Y"),
            "POSZ" => (0, 2, "Position Z"),
            "ROTX" => (1, 0, "Rotation X"),
            "ROTY" => (1, 1, "Rotation Y"),
            "ROTZ" => (1, 2, "Rotation Z"),
            "SCALEX" => (2, 0, "Scale X"),
            "SCALEY" => (2, 1, "Scale Y"),
            "SCALEZ" => (2, 2, "Scale Z"),
            _ => (-1, -1, string.Empty),
        };
        if (groupIndex < 0)
        {
            return;
        }

        float defaultValue = groupIndex switch
        {
            0 => GetComponent(GetDefaultPosition(), axisIndex),
            1 => GetComponent(GetDefaultRotation(), axisIndex),
            _ => GetComponent(GetDefaultScale(), axisIndex),
        };

        switch (groupIndex)
        {
            case 0:
                _position = WithComponent(_position, axisIndex, defaultValue);
                break;
            case 1:
                _rotationDeg = WithComponent(_rotationDeg, axisIndex, ModelTransformMath.NormalizeAngleDegrees(defaultValue));
                break;
            default:
                Vector3 nextScale = WithComponent(_scale, axisIndex, Math.Clamp(defaultValue, 0.01f, 20f));
                if (UniformScale && !IsScaleUniform(nextScale))
                {
                    UniformScale = false;
                }

                _scale = nextScale;
                break;
        }

        string formattedValue = ModelTransformMath.FormatComponent(defaultValue, groupIndex == 1 ? 2 : 3);
        CommitTransformUpdate(
            $"Reset {ModeName} {label}",
            $"Reset {label} to {(groupIndex == 1 ? $"{formattedValue}°" : formattedValue)}");
    }

    [RelayCommand]
    private void CopyPosition()
    {
        _transformClipboard.CopyPosition(_position);
        PublishStatus($"Copied Position ({ModelTransformMath.FormatFloat3(_position)})");
    }

    [RelayCommand(CanExecute = nameof(CanPastePosition))]
    private void PastePosition()
    {
        if (IsReadOnly || _transformClipboard.Position is not Vector3 copied)
        {
            return;
        }

        _position = new Vector3(
            Math.Clamp(float.IsFinite(copied.X) ? copied.X : 0f, -25f, 25f),
            Math.Clamp(float.IsFinite(copied.Y) ? copied.Y : 0f, -25f, 25f),
            Math.Clamp(float.IsFinite(copied.Z) ? copied.Z : 0f, -25f, 25f));
        CommitTransformUpdate(
            $"Paste {ModeName} Position",
            $"Pasted Position ({ModelTransformMath.FormatFloat3(_position)})");
    }

    [RelayCommand]
    private void CopyRotation()
    {
        _transformClipboard.CopyRotation(_rotationDeg);
        PublishStatus($"Copied Rotation ({ModelTransformMath.FormatFloat3(_rotationDeg)}°)");
    }

    [RelayCommand(CanExecute = nameof(CanPasteRotation))]
    private void PasteRotation()
    {
        if (IsReadOnly || _transformClipboard.Rotation is not Vector3 copied)
        {
            return;
        }

        _rotationDeg = new Vector3(
            ModelTransformMath.NormalizeAngleDegrees(copied.X),
            ModelTransformMath.NormalizeAngleDegrees(copied.Y),
            ModelTransformMath.NormalizeAngleDegrees(copied.Z));
        CommitTransformUpdate(
            $"Paste {ModeName} Rotation",
            $"Pasted Rotation ({ModelTransformMath.FormatFloat3(_rotationDeg)}°)");
    }

    [RelayCommand]
    private void CopyScale()
    {
        _transformClipboard.CopyScale(_scale);
        PublishStatus($"Copied Scale ({ModelTransformMath.FormatFloat3(_scale)})");
    }

    [RelayCommand(CanExecute = nameof(CanPasteScale))]
    private void PasteScale()
    {
        if (IsReadOnly || _transformClipboard.Scale is not Vector3 copied)
        {
            return;
        }

        Vector3 clamped = new(
            Math.Clamp(float.IsFinite(copied.X) ? copied.X : 1f, 0.01f, 20f),
            Math.Clamp(float.IsFinite(copied.Y) ? copied.Y : 1f, 0.01f, 20f),
            Math.Clamp(float.IsFinite(copied.Z) ? copied.Z : 1f, 0.01f, 20f));
        if (UniformScale && !IsScaleUniform(clamped))
        {
            UniformScale = false;
        }

        _scale = clamped;
        CommitTransformUpdate(
            $"Paste {ModeName} Scale",
            $"Pasted Scale ({ModelTransformMath.FormatFloat3(_scale)})");
    }

    [RelayCommand]
    private void CopyFromOtherModel()
    {
        if (IsReadOnly)
        {
            return;
        }

        (Vector3 sourcePos, Vector3 sourceRot, Vector3 sourceScale) = Document.ReadItemModelTransform(!IsWorldModelMode);
        _position = sourcePos;
        _rotationDeg = sourceRot;
        _scale = sourceScale;
        CommitTransformUpdate(CopyFromOtherModelLabel);
    }

    private void HandleSliderVectorComponent(int groupIndex, int axisIndex, double value)
    {
        if (_suppressSync || IsReadOnly || !double.IsFinite(value))
        {
            return;
        }

        CommitComponentEdit(groupIndex, axisIndex, (float)value, updateText: true, updateSlider: false);
    }

    private void HandleTextVectorComponent(int groupIndex, int axisIndex, string text)
    {
        if (_suppressSync || IsReadOnly || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) || !float.IsFinite(parsed))
        {
            return;
        }

        if (groupIndex == 2)
        {
            parsed = Math.Clamp(parsed, 0.01f, 20f);
        }
        else if (groupIndex == 0)
        {
            parsed = Math.Clamp(parsed, -25f, 25f);
        }
        else
        {
            parsed = ModelTransformMath.NormalizeAngleDegrees(parsed);
        }

        CommitComponentEdit(groupIndex, axisIndex, parsed, updateText: false, updateSlider: true);
    }

    private void CommitComponentEdit(int groupIndex, int axisIndex, float newVal, bool updateText, bool updateSlider)
    {
        switch (groupIndex)
        {
            case 0:
                _position = WithComponent(_position, axisIndex, newVal);
                break;
            case 1:
                _rotationDeg = WithComponent(_rotationDeg, axisIndex, ModelTransformMath.NormalizeAngleDegrees(newVal));
                break;
            default:
                if (UniformScale)
                {
                    float oldComp = GetComponent(_scale, axisIndex);
                    float ratio = oldComp > 1e-4f ? newVal / oldComp : 1f;
                    float sx = oldComp > 1e-4f ? Math.Clamp(_scale.X * ratio, 0.01f, 20f) : newVal;
                    float sy = oldComp > 1e-4f ? Math.Clamp(_scale.Y * ratio, 0.01f, 20f) : newVal;
                    float sz = oldComp > 1e-4f ? Math.Clamp(_scale.Z * ratio, 0.01f, 20f) : newVal;
                    _scale = WithComponent(new Vector3(sx, sy, sz), axisIndex, Math.Clamp(newVal, 0.01f, 20f));
                }
                else
                {
                    _scale = WithComponent(_scale, axisIndex, Math.Clamp(newVal, 0.01f, 20f));
                }

                break;
        }

        _suppressSync = true;
        try
        {
            if (updateText)
            {
                SyncTextsFromVectors();
            }
            else if (groupIndex == 2 && UniformScale)
            {
                // When scaling uniformly via one TextBox, update the OTHER two TextBoxes cleanly.
                if (axisIndex != 0)
                {
                    ScaleXText = ModelTransformMath.FormatComponent(_scale.X);
                }

                if (axisIndex != 1)
                {
                    ScaleYText = ModelTransformMath.FormatComponent(_scale.Y);
                }

                if (axisIndex != 2)
                {
                    ScaleZText = ModelTransformMath.FormatComponent(_scale.Z);
                }
            }

            if (updateSlider || (groupIndex == 2 && UniformScale))
            {
                SyncSlidersFromVectors();
            }
        }
        finally
        {
            _suppressSync = false;
        }

        string groupName = groupIndex switch
        {
            0 => "Position",
            1 => "Rotation",
            _ => "Scale",
        };
        string axisChar = axisIndex switch
        {
            0 => "X",
            1 => "Y",
            _ => "Z",
        };

        ApplyTransformToDocument(
            _position,
            _rotationDeg,
            _scale,
            $"Edit {(IsViewModelMode ? "ViewModel" : "WorldModel")} {groupName} {axisChar}",
            coalesceKey: $"model-editor:{Document.DocumentKey}:{Mode}:{groupName}:{axisChar}",
            recordUndo: true);
        UpdateRenderStateOnly();
    }

    private void ApplyTransformToDocument(
        Vector3 position,
        Vector3 rotationDegrees,
        Vector3 scale,
        string undoDescription,
        string? coalesceKey,
        bool recordUndo)
    {
        _isApplyingToDocument = true;
        try
        {
            Document.ApplyItemModelTransform(
                IsWorldModelMode,
                position,
                rotationDegrees,
                scale,
                undoDescription,
                coalesceKey,
                recordUndo);
        }
        finally
        {
            _isApplyingToDocument = false;
        }
    }

    private void SyncTransformFromDocument()
    {
        (_position, _rotationDeg, _scale) = Document.ReadItemModelTransform(IsWorldModelMode);
        PushVectorsToUiProperties();
    }

    private void PushVectorsToUiProperties()
    {
        _suppressSync = true;
        try
        {
            SyncSlidersFromVectors();
            SyncTextsFromVectors();
        }
        finally
        {
            _suppressSync = false;
        }
    }

    private void SyncSlidersFromVectors()
    {
        PosX = Math.Clamp(_position.X, MIN_POS, MAX_POS);
        PosY = Math.Clamp(_position.Y, MIN_POS, MAX_POS);
        PosZ = Math.Clamp(_position.Z, MIN_POS, MAX_POS);

        RotX = Math.Clamp(_rotationDeg.X, MIN_ROT, MAX_ROT);
        RotY = Math.Clamp(_rotationDeg.Y, MIN_ROT, MAX_ROT);
        RotZ = Math.Clamp(_rotationDeg.Z, MIN_ROT, MAX_ROT);

        ScaleX = Math.Clamp(_scale.X, MIN_SCALE, MAX_SCALE);
        ScaleY = Math.Clamp(_scale.Y, MIN_SCALE, MAX_SCALE);
        ScaleZ = Math.Clamp(_scale.Z, MIN_SCALE, MAX_SCALE);
    }

    private void SyncTextsFromVectors()
    {
        PosXText = ModelTransformMath.FormatComponent(_position.X);
        PosYText = ModelTransformMath.FormatComponent(_position.Y);
        PosZText = ModelTransformMath.FormatComponent(_position.Z);

        RotXText = ModelTransformMath.FormatComponent(_rotationDeg.X, 2);
        RotYText = ModelTransformMath.FormatComponent(_rotationDeg.Y, 2);
        RotZText = ModelTransformMath.FormatComponent(_rotationDeg.Z, 2);

        ScaleXText = ModelTransformMath.FormatComponent(_scale.X);
        ScaleYText = ModelTransformMath.FormatComponent(_scale.Y);
        ScaleZText = ModelTransformMath.FormatComponent(_scale.Z);
    }

    private void RebuildCurrentState()
    {
        ResolvedItemModelVisual visual = _glController != null
            ? _glController.ResolveItemModelVisual(Document, IsWorldModelMode, SelectedPreviewBrickShape)
            : new ResolvedItemModelVisual(
                PreviewGeometryFactory.CreateTexturedCube(1f),
                PreviewTextureMode.VertexColorOnly,
                BrickAlpha.Opaque,
                0.5f,
                DoubleSided: false,
                StandaloneTexture: null,
                IsPlaceableBrickAny: false,
                IsPlaceholder: true,
                SourceLabel: "Fallback Cube",
                AccentHex: "#60A5FA");

        BodyReferenceSpriteInfo? bodyRef = _glController?.ResolveDefaultBodyReference(_shell.AllDocuments)
            ?? WorkspaceModelReferenceResolver.ResolveDeterministicBodySprite(null, _shell.AllDocuments, _ => null);

        ShowBrickShapeSelector = visual.IsPlaceableBrickAny;
        ModelSourceSummary = visual.SourceLabel;
        ShowPlaceholderNotice = visual.IsPlaceholder;

        float bodyAspect = 0.6f;
        if (bodyRef.HasValue)
        {
            HasReferenceBodySprite = true;
            ReferenceBodyLabel = $"{bodyRef.Value.BodyId} · {bodyRef.Value.StateName}/{bodyRef.Value.Direction} · 1.8 m";
            if (bodyRef.Value.Texture.HasValue && bodyRef.Value.Texture.Value.Height > 0)
            {
                bodyAspect = bodyRef.Value.Texture.Value.Width / (float)bodyRef.Value.Texture.Value.Height;
            }
        }
        else
        {
            HasReferenceBodySprite = false;
            ReferenceBodyLabel = "1.8 m Scale Guide (no body sprite found)";
        }

        OnPropertyChanged(nameof(ViewportOverlayBadge));

        CurrentRenderState = new ModelEditorRenderState
        {
            ModePreset = IsViewModelMode ? PreviewCameraPreset.ItemViewModelFirstPerson : PreviewCameraPreset.ItemWorldModelEditor,
            IsWorldModel = IsWorldModelMode,
            FovDegrees = (float)FovDegrees,
            Position = _position,
            RotationDegrees = _rotationDeg,
            Scale = _scale,
            GizmoMode = GizmoMode,
            GizmoSpace = GizmoSpace,
            SnapEnabled = SnapEnabled,
            UniformScale = UniformScale,
            ShowFloorPlane = ShowFloorPlane,
            ShowBodyReference = ShowBodyReference,
            IsReadOnly = IsReadOnly,
            MeshGeometry = visual.Geometry,
            TextureMode = visual.TextureMode,
            AlphaMode = visual.AlphaMode,
            AlphaCutoff = visual.AlphaCutoff,
            DoubleSided = visual.DoubleSided,
            ItemStandaloneTexture = visual.StandaloneTexture,
            ReferenceBodyId = bodyRef?.BodyId,
            ReferenceBodySpritePath = bodyRef?.AbsolutePath,
            ReferenceBodyTexture = bodyRef?.Texture,
            ReferenceBodyAspect = bodyAspect,
            AccentHex = visual.AccentHex,
        };

        RenderStateChanged?.Invoke();
    }

    private void UpdateRenderStateOnly()
    {
        CurrentRenderState = CurrentRenderState with
        {
            ModePreset = IsViewModelMode ? PreviewCameraPreset.ItemViewModelFirstPerson : PreviewCameraPreset.ItemWorldModelEditor,
            IsWorldModel = IsWorldModelMode,
            FovDegrees = (float)FovDegrees,
            Position = _position,
            RotationDegrees = _rotationDeg,
            Scale = _scale,
            GizmoMode = GizmoMode,
            GizmoSpace = GizmoSpace,
            SnapEnabled = SnapEnabled,
            UniformScale = UniformScale,
            ShowFloorPlane = ShowFloorPlane,
            ShowBodyReference = ShowBodyReference,
            IsReadOnly = IsReadOnly,
        };

        RenderStateChanged?.Invoke();
    }

    private string ModeName => IsViewModelMode ? "ViewModel" : "WorldModel";

    private Vector3 GetDefaultPosition() =>
        IsViewModelMode ? ModelTransformMath.DefaultViewModelPosition : ModelTransformMath.DefaultWorldModelPosition;

    private Vector3 GetDefaultRotation() =>
        IsViewModelMode ? ModelTransformMath.DefaultViewModelRotation : ModelTransformMath.DefaultWorldModelRotation;

    private Vector3 GetDefaultScale() =>
        IsViewModelMode ? ModelTransformMath.DefaultViewModelScale : ModelTransformMath.DefaultWorldModelScale;

    private void CommitTransformUpdate(string undoDescription, string? statusMessage = null)
    {
        PushVectorsToUiProperties();
        ApplyTransformToDocument(
            _position,
            _rotationDeg,
            _scale,
            undoDescription,
            coalesceKey: null,
            recordUndo: true);
        UpdateRenderStateOnly();
        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            PublishStatus(statusMessage);
        }
    }

    private void PublishStatus(string message)
    {
        StatusText = message;
        _onStatus?.Invoke(message);
    }

    private void HandleTransformClipboardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(StudioTransformClipboard.Position), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(StudioTransformClipboard.HasPosition), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(CanPastePosition));
            PastePositionCommand.NotifyCanExecuteChanged();
        }

        if (string.IsNullOrEmpty(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(StudioTransformClipboard.Rotation), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(StudioTransformClipboard.HasRotation), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(CanPasteRotation));
            PasteRotationCommand.NotifyCanExecuteChanged();
        }

        if (string.IsNullOrEmpty(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(StudioTransformClipboard.Scale), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(StudioTransformClipboard.HasScale), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(CanPasteScale));
            PasteScaleCommand.NotifyCanExecuteChanged();
        }
    }

    private static bool IsScaleUniform(Vector3 scale) =>
        MathF.Abs(scale.X - scale.Y) < 1e-4f
        && MathF.Abs(scale.Y - scale.Z) < 1e-4f
        && MathF.Abs(scale.X - scale.Z) < 1e-4f;

    private static Vector3 WithComponent(Vector3 v, int axisIndex, float value) => axisIndex switch
    {
        0 => new Vector3(value, v.Y, v.Z),
        1 => new Vector3(v.X, value, v.Z),
        _ => new Vector3(v.X, v.Y, value),
    };

    private static float GetComponent(Vector3 v, int axisIndex) => axisIndex switch
    {
        0 => v.X,
        1 => v.Y,
        _ => v.Z,
    };
}
