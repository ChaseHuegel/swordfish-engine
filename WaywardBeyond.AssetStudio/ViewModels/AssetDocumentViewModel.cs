using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.AssetStudio.Services;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Editable view model representing any workspace asset document, form fields, and live TOML source.
/// </summary>
public sealed partial class AssetDocumentViewModel : ObservableObject
{
    private static readonly string[] DEFAULT_SHADER_OPTIONS =
    [
        "lighted",
        "lightedArray",
        "textured",
        "voxelGhost",
        "brick_icon",
        "laser_beam",
    ];

    private static readonly string[] BODY_DIRECTION_SWATCHES = ["#5282B8", "#436B97", "#5B8EC4", "#4D79A8"];

    private readonly List<StudioFieldViewModel> _allFields = [];
    private readonly HashSet<string> _modifiedFieldKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _removedLocalizationKeys = new(StringComparer.Ordinal);
    private readonly List<string> _tagBaseValues = [];
    private readonly List<string> _tagFileValues = [];
    private readonly List<BodySpriteSlotViewModel> _allBodySprites = [];
    private readonly Action<AssetDocumentViewModel>? _onDocumentMutated;
    private readonly Action<AssetDocumentViewModel, StudioFieldViewModel>? _onCreateLocKey;
    private readonly Action<AssetDocumentViewModel, StudioFieldViewModel>? _onBrowseTextureFile;
    private readonly Action<string>? _onActionFeedback;
    private readonly Func<DocumentSavePayload, string?>? _previewEditedToml;
    private readonly Action<AssetDocumentViewModel, string, string?>? _onBlueprintAction;
    private readonly Action<AssetDocumentViewModel, StudioUndoAction>? _onRecordUndo;
    private readonly Func<bool>? _isUndoReplaying;
    private readonly Func<string?, string?>? _resolveTexturePath;
    private readonly IReadOnlyList<ReferenceCandidate> _allReferenceCandidates;
    private IReadOnlyList<WorkspaceImageEntry> _workspaceImages;
    private string _tomlText = string.Empty;
    private string _newTagValue = string.Empty;
    private string _localizationFilterText = string.Empty;
    private string _newLocalizationKey = string.Empty;
    private string _newLocalizationValue = string.Empty;
    private string _newBodyStateName = string.Empty;
    private string _newBodySpriteDirection = "front";
    private string _newBodySpriteTexturePath = string.Empty;
    private bool _hasWarnedTagReorder;
    private bool _suppressRawTomlTracking;
    private bool _hasDirectRawTomlEdit;
    private bool _bodySpritesModified;
    private bool _skillLevelsModified;
    private bool _recipeInputsModified;
    private bool _recipeOutputsModified;
    private bool _hasInitialViewModelTable;
    private bool _hasInitialWorldModelTable;
    private int _saveEpoch;
    private int _syntaxErrorLineNumber;

    [ObservableProperty]
    private string _id;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private bool _hasSyntaxError;

    [ObservableProperty]
    private string? _syntaxErrorMessage;

    [ObservableProperty]
    private string _selectedBodyState = "standing";

    [ObservableProperty]
    private bool _showTagOrderWarning;

    [ObservableProperty]
    private bool _hasDuplicateTags;

    [ObservableProperty]
    private string _selectedLocale = "en";

    [ObservableProperty]
    private bool _isBlueprintDeleteConfirming;

    [ObservableProperty]
    private bool _isPaintTextureKind;

    [ObservableProperty]
    private bool _isEditingRawToml;

    [ObservableProperty]
    private int _multiEntryIndex = 9;

    /// <summary>
    ///     Initializes an editable asset document from its seed data and workspace candidates.
    /// </summary>
    public AssetDocumentViewModel(
        StudioAssetSeed seed,
        WorkspaceMode mode,
        IReadOnlyList<ReferenceCandidate> candidates,
        Action<AssetDocumentViewModel>? onDocumentMutated = null,
        Action<AssetDocumentViewModel, StudioFieldViewModel>? onCreateLocKey = null,
        Action<string>? onActionFeedback = null,
        Func<DocumentSavePayload, string?>? previewEditedToml = null,
        Action<AssetDocumentViewModel, string, string?>? onBlueprintAction = null,
        IReadOnlyList<string>? availableShaders = null,
        Action<AssetDocumentViewModel, StudioUndoAction>? onRecordUndo = null,
        Func<bool>? isUndoReplaying = null,
        Func<string?, string?>? resolveTexturePath = null,
        bool forceClean = false,
        IReadOnlyList<WorkspaceImageEntry>? workspaceImages = null,
        Action<AssetDocumentViewModel, StudioFieldViewModel>? onBrowseTextureFile = null)
    {
        InitialSeed = seed ?? throw new ArgumentNullException(nameof(seed));
        _onDocumentMutated = onDocumentMutated;
        _onCreateLocKey = onCreateLocKey;
        _onBrowseTextureFile = onBrowseTextureFile;
        _onActionFeedback = onActionFeedback;
        _previewEditedToml = previewEditedToml;
        _onBlueprintAction = onBlueprintAction;
        _onRecordUndo = onRecordUndo;
        _isUndoReplaying = isUndoReplaying;
        _resolveTexturePath = resolveTexturePath;
        _allReferenceCandidates = candidates;
        _workspaceImages = workspaceImages != null ? [.. workspaceImages] : [];

        _id = seed.Id;
        Kind = seed.Kind;
        GroupTitle = seed.GroupTitle;
        RelativeFile = seed.RelativeFile;
        RootName = seed.RootName;
        Provenance = seed.Provenance;
        ShadowedRoot = seed.ShadowedRoot;
        MultiEntryLabel = seed.MultiEntryLabel;
        AbsoluteFilePath = seed.AbsoluteFilePath;
        TableIndex = seed.TableIndex;
        EntryIndex = seed.EntryIndex;
        BaseContentSha256 = seed.BaseContentSha256;
        ResolvedBodyFrontTexturePath = seed.ResolvedBodyFrontTexturePath;
        ResolvedMaterialTexturePath = seed.ResolvedMaterialTexturePath;
        ResolvedPaintTexturePath = seed.ResolvedPaintTexturePath;
        _syntaxErrorLineNumber = seed.SyntaxErrorLineNumber;
        _isDirty = !forceClean && seed.IsDirty;
        _isReadOnly = mode == WorkspaceMode.Pack && seed.Provenance == AssetProvenance.Base;
        _hasSyntaxError = seed.HasSyntaxError;
        _syntaxErrorMessage = seed.SyntaxErrorMessage;
        _isEditingRawToml = seed.HasSyntaxError;

        foreach (var inbound in seed.InboundReferences)
        {
            InboundReferences.Add(inbound);
        }

        for (var i = 0; i < seed.BlueprintPalette.Count; i++)
        {
            BlueprintPalette.Add(seed.BlueprintPalette[i]);
        }

        var readOnly = _isReadOnly;
        string GetVal(string key, string fallback = "") =>
            seed.ScalarValues.TryGetValue(key, out var found) ? found : fallback;

        IReadOnlyList<ReferenceCandidate> Pool(ReferenceKind refKind) =>
            FilterCandidates(candidates, refKind);

        StudioFieldViewModel AddField(
            string key,
            string label,
            string hint,
            string initial,
            bool isBool = false,
            bool isColor = false,
            bool isNumeric = false,
            bool isInformational = false,
            bool showRawTokenToggle = true,
            IReadOnlyList<string>? choices = null,
            ReferenceKind? referenceKind = null,
            bool? showUnreadBadge = null)
        {
            var effectiveChoices = choices != null && choices.Count > 0
                ? EnsureChoiceIncludesValue(choices, initial)
                : choices;
            Action<StudioFieldViewModel>? browseTextureAction =
                referenceKind is ReferenceKind.Texture or ReferenceKind.BrickFace or ReferenceKind.Mesh && _onBrowseTextureFile != null
                    ? f => _onBrowseTextureFile.Invoke(this, f)
                    : null;
            var field = new StudioFieldViewModel(
                fieldKey: key,
                label: label,
                grammarHint: hint,
                initialValue: initial,
                isReadOnly: readOnly,
                isBool: isBool,
                isColor: isColor,
                isNumeric: isNumeric,
                isInformational: isInformational,
                showRawTokenToggle: showRawTokenToggle,
                choices: effectiveChoices,
                referenceKind: referenceKind,
                candidates: referenceKind != null ? Pool(referenceKind.Value) : null,
                onValueChanged: HandleFieldChanged,
                onCreateLocalizationKey: f => _onCreateLocKey?.Invoke(this, f),
                showUnreadBadge: showUnreadBadge,
                onValueTransition: HandleFieldValueTransition,
                onBrowseTextureFile: browseTextureAction);
            _allFields.Add(field);
            return field;
        }

        // Identity header optional fields
        if (Kind == AssetKind.Item || Kind == AssetKind.Paint || Kind == AssetKind.Skill)
        {
            var nameHint = Kind switch
            {
                AssetKind.Paint => "dot key · paint.red.name",
                AssetKind.Skill => "dot key · skill.name.building",
                _ => "dot key · item.base.core",
            };
            NameField = AddField("Name", "Display Name", nameHint, GetVal("Name"), referenceKind: ReferenceKind.Localization);
        }

        if (Kind == AssetKind.Item)
        {
            DescriptionField = AddField("Description", "Description", "dot key · item.base.desc", GetVal("Description"), referenceKind: ReferenceKind.Localization);
            IconField = AddField("Icon", "Icon Texture", "path + .png · items/drill.png", GetVal("Icon"), referenceKind: ReferenceKind.Texture);
        }
        else if (Kind == AssetKind.Skill)
        {
            IconField = AddField("Icon", "Skill Icon", "path + .png", GetVal("Icon"), isInformational: true, referenceKind: ReferenceKind.Texture);
            SkillCategoryField = AddField("Category", "Category Key", "dot key", GetVal("Category"), isInformational: true, referenceKind: ReferenceKind.Localization);
        }

        // Brick fields
        if (Kind == AssetKind.Brick)
        {
            BrickShapeField = AddField("Shape", "Shape", string.Empty, GetVal("Shape", "Block"), choices: ["Block", "Slab", "Stair", "Slope", "Column", "Plate", "Custom", "Any"]);
            BrickColorField = AddField("Color", "Albedo Color", "6-digit hex · #4A6B8A", GetVal("Color", "#FFFFFF"), isColor: true);
            BrickAlphaField = AddField("Alpha", "Alpha Mode", string.Empty, GetVal("Alpha", "Opaque"), choices: ["Opaque", "Masked", "Blend"]);
            BrickCastShadowsField = AddField("CastShadows", "Cast Shadows", string.Empty, GetVal("CastShadows", "true"), isBool: true);
            BrickPassableField = AddField("Passable", "Passable", string.Empty, GetVal("Passable", "false"), isBool: true);
            BrickMeshField = AddField("Mesh", "Custom Mesh", "path + .obj · thruster.obj", GetVal("Mesh"), referenceKind: ReferenceKind.Mesh);
            BrickConnectedField = AddField("Connected", "Connected Faces", string.Empty, GetVal("Connected", "false"), isBool: true);
            BrickTexDefaultField = AddField("TextureDefault", "Default Face", "bare layer · core", GetVal("TextureDefault"), referenceKind: ReferenceKind.BrickFace);
            BrickTexTopField = AddField("TextureTop", "Top (+Y)", "bare layer · vent", GetVal("TextureTop"), referenceKind: ReferenceKind.BrickFace);
            BrickTexBottomField = AddField("TextureBottom", "Bottom (-Y)", "bare layer · vent", GetVal("TextureBottom"), referenceKind: ReferenceKind.BrickFace);
            BrickTexFrontField = AddField("TextureFront", "Front (+Z)", "bare layer · core", GetVal("TextureFront"), referenceKind: ReferenceKind.BrickFace);
            BrickTexBackField = AddField("TextureBack", "Back (-Z)", "bare layer · greeble1", GetVal("TextureBack"), referenceKind: ReferenceKind.BrickFace);
            BrickTexLeftField = AddField("TextureLeft", "Left (-X)", "bare layer · vent", GetVal("TextureLeft"), referenceKind: ReferenceKind.BrickFace);
            BrickTexRightField = AddField("TextureRight", "Right (+X)", "bare layer · vent", GetVal("TextureRight"), referenceKind: ReferenceKind.BrickFace);
            BrickSoundPlaceField = AddField("SoundPlace", "Place Sound", "sound tag · metal_place", GetVal("SoundPlace"), referenceKind: ReferenceKind.SoundTag);
            BrickSoundBreakField = AddField("SoundBreak", "Break Sound", "sound tag · metal_break", GetVal("SoundBreak"), referenceKind: ReferenceKind.SoundTag);

            foreach (var col in seed.Colliders)
            {
                Colliders.Add(CreateColliderEntry(col.Type, col.Size, col.Offset, col.Rotation));
            }
        }

        // Item fields
        if (Kind == AssetKind.Item)
        {
            _hasInitialViewModelTable = seed.ScalarValues.ContainsKey("ViewModelPosition")
                || seed.ScalarValues.ContainsKey("ViewModelMesh")
                || seed.ScalarValues.ContainsKey("ViewModelMaterial");
            _hasInitialWorldModelTable = seed.ScalarValues.ContainsKey("WorldModelPosition")
                || seed.ScalarValues.ContainsKey("WorldModelMesh")
                || seed.ScalarValues.ContainsKey("WorldModelMaterial");

            ItemMaxStackField = AddField("MaxStack", "Max Stack", "1..999", GetVal("MaxStack", "64"), isNumeric: true);
            ItemPlaceableTypeField = AddField("PlaceableType", "Placeable Type", string.Empty, GetVal("PlaceableType", "none"), choices: ["none", "brick", "blueprint"]);
            ItemPlaceableIdField = AddField("PlaceableID", "Target Brick ID", "namespaced ID · wb:core", GetVal("PlaceableID"), referenceKind: ReferenceKind.BrickId);
            SyncItemPlaceableIdField();
            ItemToolTypeField = AddField("ToolType", "Tool Type", string.Empty, GetVal("ToolType", "none"), choices: ["none", "convert", "mining", "sprayer", "drill", "blueprint"]);
            ItemToolTargetField = AddField("ToolTarget", "Tool Target", "target tag", GetVal("ToolTarget"));
            ItemViewModelMeshField = AddField("ViewModelMesh", "Mesh", "path + .obj · optional", GetVal("ViewModelMesh"), referenceKind: ReferenceKind.Mesh);
            ItemViewModelMaterialField = AddField("ViewModelMaterial", "Material", "path no ext · bricks/light", GetVal("ViewModelMaterial"), referenceKind: ReferenceKind.Material);
            ItemViewModelPositionField = AddField("ViewModelPosition", "Position (X,Y,Z)", "X, Y, Z", GetVal("ViewModelPosition", "0.4, -0.4, -0.5"));
            ItemViewModelRotationField = AddField("ViewModelRotation", "Rotation (deg)", "X, Y, Z", GetVal("ViewModelRotation", "0, -60, 0"));
            ItemViewModelScaleField = AddField("ViewModelScale", "Scale (X,Y,Z)", "X, Y, Z", GetVal("ViewModelScale", "0.25, 0.25, 0.25"));
            ItemWorldModelMeshField = AddField("WorldModelMesh", "Mesh", "path + .obj", GetVal("WorldModelMesh"), isInformational: true, referenceKind: ReferenceKind.Mesh, showUnreadBadge: false);
            ItemWorldModelMaterialField = AddField("WorldModelMaterial", "Material", "path no ext", GetVal("WorldModelMaterial"), isInformational: true, referenceKind: ReferenceKind.Material, showUnreadBadge: false);
            ItemWorldModelPositionField = AddField("WorldModelPosition", "Position (X,Y,Z)", "X, Y, Z", GetVal("WorldModelPosition", "0, 0, 0"), isInformational: true, showUnreadBadge: false);
            ItemWorldModelRotationField = AddField("WorldModelRotation", "Rotation (deg)", "X, Y, Z", GetVal("WorldModelRotation", "0, 0, 0"), isInformational: true, showUnreadBadge: false);
            ItemWorldModelScaleField = AddField("WorldModelScale", "Scale (X,Y,Z)", "X, Y, Z", GetVal("WorldModelScale", "1, 1, 1"), isInformational: true, showUnreadBadge: false);
        }

        // Paint fields
        if (Kind == AssetKind.Paint)
        {
            PaintKindField = AddField("PaintKind", "Paint Kind", string.Empty, GetVal("PaintKind", "Color"), choices: ["Color", "Texture"]);
            PaintColorField = AddField("Color", "Color Swatch", "6-digit hex · #E43B44", GetVal("Color", "#E43B44"), isColor: true);
            PaintAlphaField = AddField("Alpha", "Alpha Mode", string.Empty, GetVal("Alpha", "Opaque"), choices: ["Opaque", "Masked", "Blend"]);
            PaintConnectedField = AddField("Connected", "Connected", string.Empty, GetVal("Connected", "false"), isBool: true);
            PaintVariantsField = AddField("Variants", "Texture Variants", "bare layer names", GetVal("Variants"), referenceKind: ReferenceKind.BrickFace);
            _isPaintTextureKind = string.Equals(PaintKindField.Value, "Texture", StringComparison.OrdinalIgnoreCase);
        }

        // Skill fields
        if (Kind == AssetKind.Skill)
        {
            foreach (var src in seed.SkillSources)
            {
                SkillSources.Add(CreateSkillSourceRow(src.Trigger, src.Target, src.Xp));
            }

            var cumulative = 0;
            foreach (var lvl in seed.SkillLevels)
            {
                cumulative += lvl.XpRequired;
                SkillLevels.Add(CreateSkillLevelRow(lvl.Level, lvl.XpRequired, cumulative));
            }

            RebuildSparklinePoints();
        }

        // Recipe fields
        if (Kind == AssetKind.Recipe)
        {
            RecipeTargetTagField = AddField("TargetTag", "Target Tag", "invariant tag · station.workbench", GetVal("TargetTag"), referenceKind: ReferenceKind.Tag);
            RecipeModeField = AddField("Mode", "Trigger Mode", "Auto or Manual", GetVal("Mode", "Manual"), choices: ["Manual", "Auto"]);
            RecipeDurationField = AddField("DurationSeconds", "Duration (s)", "seconds >= 0", GetVal("DurationSeconds", "2"), isNumeric: true);
            RecipePowerKwField = AddField("PowerKw", "Power (kW)", "negative draw · positive generate", GetVal("PowerKw", "0"), isNumeric: true);
            RecipePriorityField = AddField("Priority", "Priority", "integer · lower first", GetVal("Priority", "0"), isNumeric: true);
            RecipeCategoryField = AddField("Category", "Category Key", "optional dot key", GetVal("Category"), referenceKind: ReferenceKind.Localization);

            foreach (var input in seed.RecipeInputs)
            {
                RecipeInputs.Add(CreateRecipeInputRow(input.ItemId, input.Count, input.SlotGroup));
            }

            foreach (var output in seed.RecipeOutputs)
            {
                RecipeOutputs.Add(CreateRecipeOutputRow(output.ItemId, output.Count, output.Chance));
            }
        }

        // Body fields
        if (Kind == AssetKind.Body)
        {
            BodySpeciesField = AddField("Species", "Species Group", string.Empty, GetVal("Species", "human"));
            BodyGenderField = AddField("Gender", "Gender", string.Empty, GetVal("Gender", "Neutral"), choices: ["Neutral", "Male", "Female"]);

            var states = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sp in seed.BodySprites)
            {
                states.Add(sp.State);
                seed.BodySpriteAbsolutePaths.TryGetValue($"{sp.State}:{sp.Direction}", out var absPath);
                absPath ??= ResolveBodySpriteAbsolutePath(sp.TexturePath);
                _allBodySprites.Add(CreateBodySpriteSlot(sp.State, sp.Direction, sp.TexturePath, sp.SwatchHex, absPath));
            }

            foreach (var st in states)
            {
                BodyStates.Add(st);
            }

            if (BodyStates.Count > 0)
            {
                _selectedBodyState = BodyStates[0];
            }

            RebuildBodyStateChips();
            RefreshVisibleBodySprites();
        }

        // Material fields
        if (Kind == AssetKind.Material)
        {
            var shaderPool = availableShaders != null && availableShaders.Count > 0
                ? availableShaders
                : DEFAULT_SHADER_OPTIONS;
            MaterialShaderField = AddField("Shader", "Shader", string.Empty, GetVal("Shader", "textured"), choices: shaderPool);
            MaterialTexturesField = AddField("Textures", "Textures List", "path + .png · block/glass.png", GetVal("Textures"), referenceKind: ReferenceKind.Texture);
            MaterialAlphaModeField = AddField("AlphaMode", "Alpha Mode", string.Empty, GetVal("AlphaMode", "Opaque"), choices: ["Opaque", "Masked", "Blend"]);
            MaterialAlphaCutoffField = AddField("AlphaCutoff", "Alpha Cutoff", "0.0..1.0", GetVal("AlphaCutoff", "0.5"), isNumeric: true);
        }

        // Blueprint fields
        if (Kind == AssetKind.Blueprint)
        {
            BlueprintNameField = AddField("BlueprintName", "Display Name", "max 64 chars", GetVal("BlueprintName", "Untitled Blueprint"), showRawTokenToggle: false);
            BlueprintSizeLabel = GetVal("SizeLabel", "16 × 16 × 16 voxels");
            BlueprintBrickCountLabel = GetVal("BrickCountLabel", "0 bricks");
            BlueprintFileSizeLabel = GetVal("FileSizeLabel", "4.0 KiB");
            BlueprintCreatedAtLabel = GetVal("CreatedAtLabel", "2026-11-26 00:00 UTC");
            BlueprintModifiedAtLabel = GetVal("ModifiedAtLabel", "2026-11-26 00:00 UTC");
        }

        // Tag lists
        if (Kind == AssetKind.Tag)
        {
            _tagBaseValues.AddRange(seed.TagBaseValues);
            _tagFileValues.AddRange(seed.TagFileValues);
            RebuildTagLists();
        }

        // Localization table
        if (Kind == AssetKind.Localization)
        {
            foreach (var row in seed.LocalizationEntries)
            {
                LocalizationRows.Add(CreateLocalizationRow(row.Key, row.English, row.Pseudo, false));
            }

            RefreshDuplicateLocalizationKeys();
            RefreshFilteredLocalizationRows();
            _newLocalizationKey = GenerateNextUniqueLocalizationKey();
        }

        _tomlText = seed.RawTomlOverride ?? GenerateTomlText();
    }

    /// <summary>
    ///     Raised when body states, sprites, or the active body state chip changes so the inspector preview updates.
    /// </summary>
    public event Action<AssetDocumentViewModel, bool>? BodySpritesChanged;

    /// <summary>
    ///     Canonical cardinal directions offered when adding a new directional body sprite.
    /// </summary>
    public static IReadOnlyList<string> BODY_DIRECTIONS { get; } = ["front", "back", "left", "right"];

    /// <summary>
    ///     Available locales for the localization table selector.
    /// </summary>
    public static IReadOnlyList<string> LOCALE_OPTIONS { get; } = ["en", "qps-ploc"];

    /// <summary>
    ///     Original workspace seed used to construct this document view model.
    /// </summary>
    public StudioAssetSeed InitialSeed { get; }

    /// <summary>
    ///     Unique compound key disambiguating assets that share an ID across different kinds or files.
    /// </summary>
    public string DocumentKey => $"{Kind}|{Id}|{RelativeFile}";

    /// <summary>
    ///     Asset authoring category of this document.
    /// </summary>
    public AssetKind Kind { get; }

    /// <summary>
    ///     Explorer tree group heading.
    /// </summary>
    public string GroupTitle { get; }

    /// <summary>
    ///     Relative path of this file inside its workspace root.
    /// </summary>
    public string RelativeFile { get; }

    /// <summary>
    ///     Name of the providing root.
    /// </summary>
    public string RootName { get; }

    /// <summary>
    ///     Provenance category shown on badges.
    /// </summary>
    public AssetProvenance Provenance { get; }

    /// <summary>
    ///     Optional shadowed root name when overriding a lower root.
    /// </summary>
    public string? ShadowedRoot { get; }

    /// <summary>
    ///     Optional multi-entry file badge label.
    /// </summary>
    public string? MultiEntryLabel { get; }

    /// <summary>
    ///     Full filesystem path of the backing asset file on disk, when file-backed.
    /// </summary>
    public string? AbsoluteFilePath { get; }

    /// <summary>
    ///     Zero-based TOML table index inside the backing file, or -1 for root-table files.
    /// </summary>
    public int TableIndex { get; }

    /// <summary>
    ///     Zero-based entry index inside multi-entry array-of-tables files.
    /// </summary>
    public int EntryIndex { get; }

    /// <summary>
    ///     Last observed SHA-256 hex digest of the backing file on disk.
    /// </summary>
    public string? BaseContentSha256 { get; private set; }

    /// <summary>
    ///     Optional absolute path to the front-facing body sprite texture for 2D preview rendering.
    /// </summary>
    public string? ResolvedBodyFrontTexturePath { get; }

    /// <summary>
    ///     Optional absolute path to the primary material texture for 2D and 3D preview rendering.
    /// </summary>
    public string? ResolvedMaterialTexturePath { get; }

    /// <summary>
    ///     Optional absolute path to the primary block albedo texture for texture-based paint previews.
    /// </summary>
    public string? ResolvedPaintTexturePath { get; }

    /// <summary>
    ///     Discovered image files across enabled workspace roots available to body sprite pickers.
    /// </summary>
    public IReadOnlyList<WorkspaceImageEntry> WorkspaceImages => _workspaceImages;

    /// <summary>
    ///     Full list of directional body sprite slots across all states in this body document.
    /// </summary>
    public IReadOnlyList<BodySpriteSlotViewModel> AllBodySprites => _allBodySprites;

    /// <summary>
    ///     Whether this brick document has connected face textures enabled.
    /// </summary>
    public bool IsBrickConnected => Kind == AssetKind.Brick && BrickConnectedField != null && BrickConnectedField.BoolValue;

    /// <summary>
    ///     Whether the currently selected body state can be removed.
    /// </summary>
    public bool CanRemoveSelectedBodyState => !IsReadOnly && BodyStates.Count > 0 && !string.IsNullOrWhiteSpace(SelectedBodyState);

    /// <summary>
    ///     Whether the currently selected body state has no sprite entries yet.
    /// </summary>
    public bool HasNoVisibleBodySprites => VisibleBodySprites.Count == 0;

    /// <summary>
    ///     Raw TOML source text for the code view and syntax-error recovery editor.
    /// </summary>
    public string TomlText
    {
        get => _tomlText;
        set
        {
            var safe = value ?? string.Empty;
            var oldText = _tomlText;
            if (!SetProperty(ref _tomlText, safe))
            {
                return;
            }

            if (_suppressRawTomlTracking || !IsEditingRawToml)
            {
                return;
            }

            var wasDirty = IsDirty;
            var wasDirectEdit = _hasDirectRawTomlEdit;
            var recordedEpoch = _saveEpoch;
            RecordUndo(
                "Edit TOML",
                coalesceKey: $"toml:{DocumentKey}",
                undo: () =>
                {
                    _hasDirectRawTomlEdit = wasDirectEdit;
                    SetTomlTextInternal(oldText);
                    RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                    _onDocumentMutated?.Invoke(this);
                },
                redo: () =>
                {
                    _hasDirectRawTomlEdit = true;
                    SetTomlTextInternal(safe);
                    IsDirty = true;
                    _onDocumentMutated?.Invoke(this);
                });

            _hasDirectRawTomlEdit = true;
            IsDirty = true;
            _onDocumentMutated?.Invoke(this);
        }
    }

    /// <summary>
    ///     Pending tag entry text typed in the tag editor append box.
    /// </summary>
    public string NewTagValue
    {
        get => _newTagValue;
        set => SetProperty(ref _newTagValue, value ?? string.Empty);
    }

    /// <summary>
    ///     Filter query narrowing rows shown in the localization table editor.
    /// </summary>
    public string LocalizationFilterText
    {
        get => _localizationFilterText;
        set
        {
            var safe = value ?? string.Empty;
            if (SetProperty(ref _localizationFilterText, safe))
            {
                RefreshFilteredLocalizationRows();
            }
        }
    }

    /// <summary>
    ///     Pending localization key typed in the bottom add-key input, coerced to non-null text.
    /// </summary>
    public string NewLocalizationKey
    {
        get => _newLocalizationKey;
        set => SetProperty(ref _newLocalizationKey, value ?? string.Empty);
    }

    /// <summary>
    ///     Pending translation text typed in the bottom add-key input, coerced to non-null text.
    /// </summary>
    public string NewLocalizationValue
    {
        get => _newLocalizationValue;
        set => SetProperty(ref _newLocalizationValue, value ?? string.Empty);
    }

    /// <summary>
    ///     Pending state name typed in the body form's add-state input.
    /// </summary>
    public string NewBodyStateName
    {
        get => _newBodyStateName;
        set => SetProperty(ref _newBodyStateName, value ?? string.Empty);
    }

    /// <summary>
    ///     Selected cardinal direction for the next sprite added to the active body state.
    /// </summary>
    public string NewBodySpriteDirection
    {
        get => _newBodySpriteDirection;
        set => SetProperty(ref _newBodySpriteDirection, value ?? string.Empty);
    }

    /// <summary>
    ///     Pending texture path for the next sprite added to the active body state.
    /// </summary>
    public string NewBodySpriteTexturePath
    {
        get => _newBodySpriteTexturePath;
        set => SetProperty(ref _newBodySpriteTexturePath, value ?? string.Empty);
    }

    /// <summary>
    ///     Namespace prefix extracted from the ID, or empty for non-namespaced kinds.
    /// </summary>
    public string NamespaceBadge
    {
        get
        {
            var colonIndex = Id.IndexOf(':');
            if (colonIndex > 0)
            {
                return $"ns:{Id[..colonIndex]}";
            }

            return string.Empty;
        }
    }

    /// <summary>
    ///     Whether the asset ID carries an explicit namespace prefix.
    /// </summary>
    public bool HasNamespaceBadge => !string.IsNullOrEmpty(NamespaceBadge);

    /// <summary>
    ///     Whether an override or read-only provenance badge should be shown in the identity header.
    /// </summary>
    public bool ShowProvenanceBadge => Provenance == AssetProvenance.Override || Provenance == AssetProvenance.Base;

    /// <summary>
    ///     Human-readable provenance pill text for the identity header.
    /// </summary>
    public string ProvenanceDetailLabel => Provenance switch
    {
        AssetProvenance.Override => $"Overrides {ShadowedRoot ?? "base"}",
        AssetProvenance.Pack => "Pack root",
        AssetProvenance.Base => "Read-only",
        AssetProvenance.Repo => "Repository",
        _ => string.Empty,
    };

    /// <summary>
    ///     Whether the document belongs to a multi-entry TOML file.
    /// </summary>
    public bool HasMultiEntry => !string.IsNullOrWhiteSpace(MultiEntryLabel);

    /// <summary>
    ///     Whether the structured form view is enabled for this document.
    /// </summary>
    public bool CanShowForm => !HasSyntaxError;

    /// <summary>
    ///     1-based line number highlighted as a TOML syntax error, or 0 when syntax is valid.
    /// </summary>
    public int SyntaxErrorLineNumber => HasSyntaxError ? (_syntaxErrorLineNumber > 0 ? _syntaxErrorLineNumber : 4) : 0;

    /// <summary>
    ///     Whether the document supports cross-file ID renaming.
    /// </summary>
    public bool CanRenameId => !IsReadOnly && !HasSyntaxError && Kind != AssetKind.Localization && Kind != AssetKind.Blueprint;

    /// <summary>
    ///     Whether the identity header includes a display name localization field.
    /// </summary>
    public bool HasNameField => NameField != null;

    /// <summary>
    ///     Whether the identity header includes a description localization field.
    /// </summary>
    public bool HasDescriptionField => DescriptionField != null;

    /// <summary>
    ///     Whether the identity header includes an icon texture field.
    /// </summary>
    public bool HasIconField => IconField != null;

    /// <summary>
    ///     Whether the document is a brick form.
    /// </summary>
    public bool IsBrick => Kind == AssetKind.Brick;

    /// <summary>
    ///     Whether the document is an item form.
    /// </summary>
    public bool IsItem => Kind == AssetKind.Item;

    /// <summary>
    ///     Whether the document is a paint form.
    /// </summary>
    public bool IsPaint => Kind == AssetKind.Paint;

    /// <summary>
    ///     Whether the document is a skill form.
    /// </summary>
    public bool IsSkill => Kind == AssetKind.Skill;

    /// <summary>
    ///     Whether the document is a recipe form.
    /// </summary>
    public bool IsRecipe => Kind == AssetKind.Recipe;

    /// <summary>
    ///     Whether the document is a body form.
    /// </summary>
    public bool IsBody => Kind == AssetKind.Body;

    /// <summary>
    ///     Whether the document is a material form.
    /// </summary>
    public bool IsMaterial => Kind == AssetKind.Material;

    /// <summary>
    ///     Whether the document is a tag list editor.
    /// </summary>
    public bool IsTag => Kind == AssetKind.Tag;

    /// <summary>
    ///     Whether the document is a localization table editor.
    /// </summary>
    public bool IsLocalization => Kind == AssetKind.Localization;

    /// <summary>
    ///     Whether the document is a blueprint metadata view.
    /// </summary>
    public bool IsBlueprint => Kind == AssetKind.Blueprint;

    /// <summary>
    ///     Whether the paint document is currently configured as a color swatch.
    /// </summary>
    public bool IsPaintColorKind => !IsPaintTextureKind;

    /// <summary>
    ///     Display name localization key field, when defined by the kind.
    /// </summary>
    public StudioFieldViewModel? NameField { get; }

    /// <summary>
    ///     Description localization key field, when defined by the kind.
    /// </summary>
    public StudioFieldViewModel? DescriptionField { get; }

    /// <summary>
    ///     Icon texture path field, when defined by the kind.
    /// </summary>
    public StudioFieldViewModel? IconField { get; }

    /// <summary>
    ///     Brick shape enum field.
    /// </summary>
    public StudioFieldViewModel? BrickShapeField { get; }

    /// <summary>
    ///     Brick default albedo hex color field.
    /// </summary>
    public StudioFieldViewModel? BrickColorField { get; }

    /// <summary>
    ///     Brick alpha rendering mode field.
    /// </summary>
    public StudioFieldViewModel? BrickAlphaField { get; }

    /// <summary>
    ///     Brick cast shadows boolean field.
    /// </summary>
    public StudioFieldViewModel? BrickCastShadowsField { get; }

    /// <summary>
    ///     Brick passable collision boolean field.
    /// </summary>
    public StudioFieldViewModel? BrickPassableField { get; }

    /// <summary>
    ///     Brick custom mesh path field.
    /// </summary>
    public StudioFieldViewModel? BrickMeshField { get; }

    /// <summary>
    ///     Brick connected-texture adjacency boolean field.
    /// </summary>
    public StudioFieldViewModel? BrickConnectedField { get; }

    /// <summary>
    ///     Brick default fallback face layer field.
    /// </summary>
    public StudioFieldViewModel? BrickTexDefaultField { get; }

    /// <summary>
    ///     Brick top face layer override field.
    /// </summary>
    public StudioFieldViewModel? BrickTexTopField { get; }

    /// <summary>
    ///     Brick bottom face layer override field.
    /// </summary>
    public StudioFieldViewModel? BrickTexBottomField { get; }

    /// <summary>
    ///     Brick front face layer override field.
    /// </summary>
    public StudioFieldViewModel? BrickTexFrontField { get; }

    /// <summary>
    ///     Brick back face layer override field.
    /// </summary>
    public StudioFieldViewModel? BrickTexBackField { get; }

    /// <summary>
    ///     Brick left face layer override field.
    /// </summary>
    public StudioFieldViewModel? BrickTexLeftField { get; }

    /// <summary>
    ///     Brick right face layer override field.
    /// </summary>
    public StudioFieldViewModel? BrickTexRightField { get; }

    /// <summary>
    ///     Brick placement sound tag field.
    /// </summary>
    public StudioFieldViewModel? BrickSoundPlaceField { get; }

    /// <summary>
    ///     Brick break sound tag field.
    /// </summary>
    public StudioFieldViewModel? BrickSoundBreakField { get; }

    /// <summary>
    ///     Item max stack size field.
    /// </summary>
    public StudioFieldViewModel? ItemMaxStackField { get; }

    /// <summary>
    ///     Item placeable type enum field.
    /// </summary>
    public StudioFieldViewModel? ItemPlaceableTypeField { get; }

    /// <summary>
    ///     Item target brick ID reference field.
    /// </summary>
    public StudioFieldViewModel? ItemPlaceableIdField { get; }

    /// <summary>
    ///     Item tool type enum field.
    /// </summary>
    public StudioFieldViewModel? ItemToolTypeField { get; }

    /// <summary>
    ///     Item tool target field.
    /// </summary>
    public StudioFieldViewModel? ItemToolTargetField { get; }

    /// <summary>
    ///     Item first-person view model mesh field.
    /// </summary>
    public StudioFieldViewModel? ItemViewModelMeshField { get; }

    /// <summary>
    ///     Item first-person view model material field.
    /// </summary>
    public StudioFieldViewModel? ItemViewModelMaterialField { get; }

    /// <summary>
    ///     Item first-person view model offset position field.
    /// </summary>
    public StudioFieldViewModel? ItemViewModelPositionField { get; }

    /// <summary>
    ///     Item first-person view model Euler rotation field.
    /// </summary>
    public StudioFieldViewModel? ItemViewModelRotationField { get; }

    /// <summary>
    ///     Item first-person view model scale field.
    /// </summary>
    public StudioFieldViewModel? ItemViewModelScaleField { get; }

    /// <summary>
    ///     Item world model mesh field (informational).
    /// </summary>
    public StudioFieldViewModel? ItemWorldModelMeshField { get; }

    /// <summary>
    ///     Item world model material field (informational).
    /// </summary>
    public StudioFieldViewModel? ItemWorldModelMaterialField { get; }

    /// <summary>
    ///     Item world model offset position field.
    /// </summary>
    public StudioFieldViewModel? ItemWorldModelPositionField { get; }

    /// <summary>
    ///     Item world model Euler rotation field.
    /// </summary>
    public StudioFieldViewModel? ItemWorldModelRotationField { get; }

    /// <summary>
    ///     Item world model scale field.
    /// </summary>
    public StudioFieldViewModel? ItemWorldModelScaleField { get; }

    /// <summary>
    ///     Paint kind switch field (Color or Texture).
    /// </summary>
    public StudioFieldViewModel? PaintKindField { get; }

    /// <summary>
    ///     Paint color hex swatch field.
    /// </summary>
    public StudioFieldViewModel? PaintColorField { get; }

    /// <summary>
    ///     Paint alpha mode field.
    /// </summary>
    public StudioFieldViewModel? PaintAlphaField { get; }

    /// <summary>
    ///     Paint connected texture toggle field.
    /// </summary>
    public StudioFieldViewModel? PaintConnectedField { get; }

    /// <summary>
    ///     Paint texture variant list field.
    /// </summary>
    public StudioFieldViewModel? PaintVariantsField { get; }

    /// <summary>
    ///     Skill category localization key field (informational).
    /// </summary>
    public StudioFieldViewModel? SkillCategoryField { get; }

    /// <summary>
    ///     Body species grouping field.
    /// </summary>
    public StudioFieldViewModel? BodySpeciesField { get; }

    /// <summary>
    ///     Body gender enum field.
    /// </summary>
    public StudioFieldViewModel? BodyGenderField { get; }

    /// <summary>
    ///     Material shader identifier field.
    /// </summary>
    public StudioFieldViewModel? MaterialShaderField { get; }

    /// <summary>
    ///     Material textures list field.
    /// </summary>
    public StudioFieldViewModel? MaterialTexturesField { get; }

    /// <summary>
    ///     Material alpha mode enum field.
    /// </summary>
    public StudioFieldViewModel? MaterialAlphaModeField { get; }

    /// <summary>
    ///     Material alpha cutoff float field.
    /// </summary>
    public StudioFieldViewModel? MaterialAlphaCutoffField { get; }

    /// <summary>
    ///     Editable blueprint display name field.
    /// </summary>
    public StudioFieldViewModel? BlueprintNameField { get; }

    /// <summary>
    ///     Read-only blueprint voxel bounds summary.
    /// </summary>
    public string BlueprintSizeLabel { get; } = string.Empty;

    /// <summary>
    ///     Read-only blueprint brick count summary.
    /// </summary>
    public string BlueprintBrickCountLabel { get; } = string.Empty;

    /// <summary>
    ///     Read-only blueprint binary file size summary.
    /// </summary>
    public string BlueprintFileSizeLabel { get; } = string.Empty;

    /// <summary>
    ///     Read-only blueprint creation timestamp label.
    /// </summary>
    public string BlueprintCreatedAtLabel { get; } = string.Empty;

    /// <summary>
    ///     Read-only blueprint last modified timestamp label.
    /// </summary>
    public string BlueprintModifiedAtLabel { get; } = string.Empty;

    /// <summary>
    ///     Editable collider rows for brick documents.
    /// </summary>
    public ObservableCollection<ColliderEntryViewModel> Colliders { get; } = [];

    /// <summary>
    ///     Editable XP source rows for skill documents.
    /// </summary>
    public ObservableCollection<SkillSourceRowViewModel> SkillSources { get; } = [];

    /// <summary>
    ///     Editable level curve rows for skill documents.
    /// </summary>
    public ObservableCollection<SkillLevelRowViewModel> SkillLevels { get; } = [];

    /// <summary>
    ///     Numeric XP series rendered by the custom sparkline control.
    /// </summary>
    public ObservableCollection<double> SkillSparklineValues { get; } = [];

    /// <summary>
    ///     Whether this document overrides a lower workspace root.
    /// </summary>
    public bool IsOverrideProvenance => Provenance == AssetProvenance.Override;

    /// <summary>
    ///     Whether this document is newly authored in the active pack root.
    /// </summary>
    public bool IsPackProvenance => Provenance == AssetProvenance.Pack;

    /// <summary>
    ///     Whether the document supports switching between Form and TOML views.
    /// </summary>
    public bool SupportsTomlView => Kind != AssetKind.Blueprint;

    /// <summary>
    ///     Summary metric displayed in the inspector card for non-3D data documents.
    /// </summary>
    public string DataSummaryMetric => Kind switch
    {
        AssetKind.Recipe => $"{RecipeInputs.Count} inputs → {RecipeOutputs.Count} outputs · {RecipeDurationLabel}",
        AssetKind.Skill => $"{SkillLevels.Count} levels · {SkillSources.Count} XP sources",
        AssetKind.Tag => $"{TagFileEntries.Count} local · {TagEffectiveEntries.Count} effective",
        AssetKind.Localization => $"{LocalizationRows.Count} keys · locale '{SelectedLocale}'",
        _ => RelativeFile,
    };

    /// <summary>
    ///     Secondary hint displayed in the inspector card for non-3D data documents.
    /// </summary>
    public string DataSummaryDetail => Kind switch
    {
        AssetKind.Recipe => $"{RecipeTargetTagLabel} · {RecipeModeBadge} · {RecipePowerLabel}",
        AssetKind.Skill => SkillCurveSummary,
        AssetKind.Tag => HasDuplicateTags ? "Duplicate entries flagged (not auto-merged)" : "Ordered set · first-occurrence merge",
        AssetKind.Localization => "Localization table",
        _ => ProvenanceDetailLabel,
    };

    /// <summary>Recipe target tag field.</summary>
    public StudioFieldViewModel? RecipeTargetTagField { get; }

    /// <summary>Recipe trigger mode field.</summary>
    public StudioFieldViewModel? RecipeModeField { get; }

    /// <summary>Recipe processing duration field in seconds.</summary>
    public StudioFieldViewModel? RecipeDurationField { get; }

    /// <summary>Recipe power draw/generation field in kilowatts.</summary>
    public StudioFieldViewModel? RecipePowerKwField { get; }

    /// <summary>Recipe deterministic priority field (lower values sort first).</summary>
    public StudioFieldViewModel? RecipePriorityField { get; }

    /// <summary>Recipe optional category key field.</summary>
    public StudioFieldViewModel? RecipeCategoryField { get; }

    /// <summary>Editable recipe input ingredient rows.</summary>
    public ObservableCollection<RecipeIngredientRowViewModel> RecipeInputs { get; } = [];

    /// <summary>Editable recipe output item rows.</summary>
    public ObservableCollection<RecipeOutputRowViewModel> RecipeOutputs { get; } = [];

    /// <summary>Display label for the recipe target tag.</summary>
    public string RecipeTargetTagLabel => string.IsNullOrWhiteSpace(RecipeTargetTagField?.Value)
        ? "(no TargetTag)"
        : RecipeTargetTagField.Value.Trim();

    /// <summary>Whether the recipe target tag is non-empty for deep-link clicks.</summary>
    public bool HasRecipeTargetTag => !string.IsNullOrWhiteSpace(RecipeTargetTagField?.Value);

    /// <summary>Uppercase mode badge for recipe preview.</summary>
    public string RecipeModeBadge => IsRecipeAutoMode ? "AUTO" : "MANUAL";

    /// <summary>Whether the recipe runs automatically when matching inputs are present.</summary>
    public bool IsRecipeAutoMode => string.Equals(RecipeModeField?.Value?.Trim(), "auto", StringComparison.OrdinalIgnoreCase);

    /// <summary>Formatted processing duration for recipe preview.</summary>
    public string RecipeDurationLabel
    {
        get
        {
            var seconds = ParseRecipeDurationSeconds();
            if (seconds <= 0.0001)
            {
                return "Instant";
            }

            return string.Create(CultureInfo.InvariantCulture, $"{seconds:0.##} s");
        }
    }

    /// <summary>Formatted power delta label in kW for recipe preview.</summary>
    public string RecipePowerLabel
    {
        get
        {
            var kw = ParseRecipePowerKw();
            if (Math.Abs(kw) < 0.0001)
            {
                return "0 kW";
            }

            return kw > 0
                ? string.Create(CultureInfo.InvariantCulture, $"+{kw:0.##} kW")
                : string.Create(CultureInfo.InvariantCulture, $"{kw:0.##} kW");
        }
    }

    /// <summary>Whether the recipe generates power (<c>power_kw &gt; 0</c>).</summary>
    public bool IsRecipePowerGeneration => ParseRecipePowerKw() > 0.0001;

    /// <summary>Whether the recipe consumes power (<c>power_kw &lt; 0</c>).</summary>
    public bool IsRecipePowerConsumption => ParseRecipePowerKw() < -0.0001;

    /// <summary>Calculated cycles/min and energy per cycle summary for recipe preview.</summary>
    public string RecipeThroughputLabel
    {
        get
        {
            var seconds = ParseRecipeDurationSeconds();
            var kw = ParseRecipePowerKw();
            if (seconds <= 0.0001)
            {
                return Math.Abs(kw) < 0.0001 ? "Instant craft · passive" : $"Instant craft · {RecipePowerLabel}";
            }

            var perMin = 60.0 / seconds;
            var energyKj = kw * seconds;
            if (Math.Abs(energyKj) < 0.0001)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{perMin:0.#} cycles/min · passive");
            }

            var energyText = energyKj > 0
                ? string.Create(CultureInfo.InvariantCulture, $"+{energyKj:0.#} kJ/cycle")
                : string.Create(CultureInfo.InvariantCulture, $"{energyKj:0.#} kJ/cycle");
            return string.Create(CultureInfo.InvariantCulture, $"{perMin:0.#} cycles/min · {energyText}");
        }
    }

    /// <summary>Section subtitle for recipe inputs.</summary>
    public string RecipeInputsSummary => RecipeInputs.Count == 1 ? "1 input" : $"{RecipeInputs.Count} inputs";

    /// <summary>Section subtitle for recipe outputs.</summary>
    public string RecipeOutputsSummary => RecipeOutputs.Count == 1 ? "1 output" : $"{RecipeOutputs.Count} outputs";

    /// <summary>Whether the recipe has at least one input row.</summary>
    public bool RecipeHasInputs => RecipeInputs.Count > 0;

    /// <summary>Whether the recipe has no input rows.</summary>
    public bool RecipeHasNoInputs => RecipeInputs.Count == 0;

    /// <summary>Whether the recipe has at least one output row.</summary>
    public bool RecipeHasOutputs => RecipeOutputs.Count > 0;

    /// <summary>Whether the recipe has no output rows.</summary>
    public bool RecipeHasNoOutputs => RecipeOutputs.Count == 0;

    /// <summary>Whether the recipe is a power generator with no item outputs.</summary>
    public bool IsRecipeGeneratorWithoutOutputs => RecipeOutputs.Count == 0 && IsRecipePowerGeneration;

    /// <summary>Count of input/output item or tag references that do not currently resolve in the workspace.</summary>
    public int RecipeUnresolvedCount
    {
        get
        {
            var unresolved = 0;
            for (var i = 0; i < RecipeInputs.Count; i++)
            {
                if (!RecipeInputs[i].IsResolved)
                {
                    unresolved++;
                }
            }

            for (var i = 0; i < RecipeOutputs.Count; i++)
            {
                if (!RecipeOutputs[i].IsResolved)
                {
                    unresolved++;
                }
            }

            return unresolved;
        }
    }

    /// <summary>Whether any recipe input or output reference is currently unresolved.</summary>
    public bool RecipeHasUnresolvedRefs => RecipeUnresolvedCount > 0;

    /// <summary>Warning text shown in the recipe preview when references are unresolved.</summary>
    public string RecipeUnresolvedSummary => RecipeUnresolvedCount == 1
        ? "1 unresolved item or tag reference"
        : $"{RecipeUnresolvedCount} unresolved item or tag references";

    /// <summary>
    ///     Available animation state chips with active highlight state for body sprite editing.
    /// </summary>
    public ObservableCollection<FilterChipViewModel> BodyStateChips { get; } = [];

    /// <summary>
    ///     Available animation state tabs for body sprite editing.
    /// </summary>
    public ObservableCollection<string> BodyStates { get; } = [];

    /// <summary>
    ///     Directional sprite slots visible for the selected body state.
    /// </summary>
    public ObservableCollection<BodySpriteSlotViewModel> VisibleBodySprites { get; } = [];

    /// <summary>
    ///     Editable file-local entries for tag documents.
    /// </summary>
    public ObservableCollection<TagListEntryViewModel> TagFileEntries { get; } = [];

    /// <summary>
    ///     Read-only effective merged entries with provider badges for tag documents.
    /// </summary>
    public ObservableCollection<TagListEntryViewModel> TagEffectiveEntries { get; } = [];

    /// <summary>
    ///     All localization entries stored in a localization file.
    /// </summary>
    public ObservableCollection<LocalizationRowViewModel> LocalizationRows { get; } = [];

    /// <summary>
    ///     Filtered localization entries displayed in the table view.
    /// </summary>
    public ObservableCollection<LocalizationRowViewModel> FilteredLocalizationRows { get; } = [];

    /// <summary>
    ///     Brick and paint palette entries extracted from a blueprint file.
    /// </summary>
    public ObservableCollection<BlueprintPaletteItem> BlueprintPalette { get; } = [];

    /// <summary>
    ///     Whether this blueprint document contains at least one brick or paint palette entry.
    /// </summary>
    public bool HasBlueprintPalette => BlueprintPalette.Count > 0;

    /// <summary>
    ///     Whether this blueprint document has no brick or paint palette entries.
    /// </summary>
    public bool HasNoBlueprintPalette => BlueprintPalette.Count == 0;

    /// <summary>
    ///     Compact summary of brick, paint, and missing entry counts in the blueprint palette.
    /// </summary>
    public string BlueprintPaletteSummary
    {
        get
        {
            if (BlueprintPalette.Count == 0)
            {
                return "No palette entries";
            }

            var bricks = 0;
            var paints = 0;
            var missing = 0;
            for (var i = 0; i < BlueprintPalette.Count; i++)
            {
                var item = BlueprintPalette[i];
                if (item.Kind == AssetKind.Paint)
                {
                    paints++;
                }
                else
                {
                    bricks++;
                }

                if (!item.IsResolved)
                {
                    missing++;
                }
            }

            var parts = new List<string>(3);
            if (bricks > 0)
            {
                parts.Add(bricks == 1 ? "1 brick" : $"{bricks} bricks");
            }

            if (paints > 0)
            {
                parts.Add(paints == 1 ? "1 paint" : $"{paints} paints");
            }

            if (missing > 0)
            {
                parts.Add($"{missing} missing");
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>
    ///     Inbound references pointing to this asset across the workspace.
    /// </summary>
    public ObservableCollection<InboundReferenceItem> InboundReferences { get; } = [];

    /// <summary>
    ///     Heading for the right-pane references section.
    /// </summary>
    public string ReferencesSectionTitle => Kind == AssetKind.Blueprint ? "USED BRICKS" : "INBOUND REFERENCES";

    /// <summary>
    ///     Empty-state text displayed when no references are present.
    /// </summary>
    public string EmptyReferencesMessage => Kind == AssetKind.Blueprint
        ? "No bricks referenced in blueprint palette."
        : "No inbound references in workspace.";

    /// <summary>
    ///     Whether no other workspace assets reference this document.
    /// </summary>
    public bool HasNoInboundReferences => InboundReferences.Count == 0;

    /// <summary>
    ///     Summary label for the skill sparkline header.
    /// </summary>
    public string SkillCurveSummary => SkillLevels.Count == 0
        ? "No levels configured"
        : $"Levels 1–{SkillLevels[^1].Level} · Peak step {SkillLevels[^1].XpRequired:N0} XP · Total {SkillLevels[^1].CumulativeXp:N0} XP";

    partial void OnSelectedBodyStateChanged(string value)
    {
        for (var i = 0; i < BodyStateChips.Count; i++)
        {
            BodyStateChips[i].IsSelected = string.Equals(BodyStateChips[i].Label, value, StringComparison.OrdinalIgnoreCase);
        }

        RefreshVisibleBodySprites();
        OnPropertyChanged(nameof(CanRemoveSelectedBodyState));
        BodySpritesChanged?.Invoke(this, true);
    }

    partial void OnSelectedLocaleChanged(string value)
    {
        var pseudo = string.Equals(value, "qps-ploc", StringComparison.OrdinalIgnoreCase);
        for (var i = 0; i < LocalizationRows.Count; i++)
        {
            LocalizationRows[i].UsePseudoLocale = pseudo;
        }

        SetTomlTextInternal(GenerateTomlText());
        OnPropertyChanged(nameof(DataSummaryMetric));
    }

    partial void OnIsPaintTextureKindChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPaintColorKind));
    }

    partial void OnIdChanged(string value)
    {
        OnPropertyChanged(nameof(DocumentKey));
        OnPropertyChanged(nameof(NamespaceBadge));
        OnPropertyChanged(nameof(HasNamespaceBadge));
        SetTomlTextInternal(GenerateTomlText());
    }

    /// <summary>
    ///     Builds a structured save payload for atomic disk persistence or crash-recovery draft sync.
    /// </summary>
    public DocumentSavePayload BuildSavePayload()
    {
        var values = new Dictionary<string, string>(_allFields.Count, StringComparer.Ordinal);
        var rawTokens = new Dictionary<string, string>(_allFields.Count, StringComparer.Ordinal);
        var rawFlags = new Dictionary<string, bool>(_allFields.Count, StringComparer.Ordinal);
        for (var i = 0; i < _allFields.Count; i++)
        {
            var field = _allFields[i];
            values[field.FieldKey] = field.Value;
            rawTokens[field.FieldKey] = field.RawTokenText;
            rawFlags[field.FieldKey] = field.IsRawMode;
        }

        var locRows = new List<(string Key, string Value)>(LocalizationRows.Count);
        var locKeyEdits = new List<(string OriginalKey, string Key, string Value)>(LocalizationRows.Count);
        for (var i = 0; i < LocalizationRows.Count; i++)
        {
            var row = LocalizationRows[i];
            locRows.Add((row.Key, row.ActiveValue));
            locKeyEdits.Add((row.OriginalKey, row.Key, row.EnglishValue));
        }

        List<(string State, string Direction, string TexturePath)>? bodySprites = null;
        if (Kind == AssetKind.Body)
        {
            bodySprites = new List<(string State, string Direction, string TexturePath)>(_allBodySprites.Count);
            for (var i = 0; i < _allBodySprites.Count; i++)
            {
                var slot = _allBodySprites[i];
                bodySprites.Add((slot.StateName, slot.Direction, slot.TexturePath));
            }
        }

        List<(int Level, int XpRequired)>? skillLevels = null;
        if (Kind == AssetKind.Skill)
        {
            skillLevels = new List<(int Level, int XpRequired)>(SkillLevels.Count);
            for (var i = 0; i < SkillLevels.Count; i++)
            {
                skillLevels.Add((SkillLevels[i].Level, SkillLevels[i].XpRequired));
            }
        }

        List<(string ItemId, int Count, string SlotGroup)>? recipeInputs = null;
        List<(string ItemId, int Count, string Chance)>? recipeOutputs = null;
        if (Kind == AssetKind.Recipe)
        {
            recipeInputs = new List<(string ItemId, int Count, string SlotGroup)>(RecipeInputs.Count);
            for (var i = 0; i < RecipeInputs.Count; i++)
            {
                recipeInputs.Add((RecipeInputs[i].ItemId, RecipeInputs[i].Count, RecipeInputs[i].SlotGroup));
            }

            recipeOutputs = new List<(string ItemId, int Count, string Chance)>(RecipeOutputs.Count);
            for (var i = 0; i < RecipeOutputs.Count; i++)
            {
                recipeOutputs.Add((RecipeOutputs[i].ItemId, RecipeOutputs[i].Count, RecipeOutputs[i].Chance));
            }
        }

        return new DocumentSavePayload
        {
            AssetId = Id,
            Kind = Kind,
            RelativeFile = RelativeFile,
            AbsoluteFilePath = AbsoluteFilePath,
            TableIndex = TableIndex,
            EntryIndex = EntryIndex,
            BaseContentSha256 = BaseContentSha256,
            HasSyntaxError = HasSyntaxError,
            IsDirectRawEdit = _hasDirectRawTomlEdit,
            RawTomlText = TomlText,
            FieldValues = values,
            FieldRawTokens = rawTokens,
            FieldRawModeFlags = rawFlags,
            ModifiedFieldKeys = new HashSet<string>(_modifiedFieldKeys, StringComparer.Ordinal),
            TagFileValues = [.. _tagFileValues],
            LocalizationRows = locRows,
            LocalizationKeyEdits = locKeyEdits,
            RemovedLocalizationKeys = _removedLocalizationKeys.Count > 0 ? [.. _removedLocalizationKeys] : null,
            SelectedLocale = SelectedLocale,
            BlueprintDisplayName = BlueprintNameField?.Value,
            BodySprites = bodySprites,
            BodySpritesModified = _bodySpritesModified,
            SkillLevels = skillLevels,
            SkillLevelsModified = _skillLevelsModified,
            RecipeInputs = recipeInputs,
            RecipeInputsModified = _recipeInputsModified,
            RecipeOutputs = recipeOutputs,
            RecipeOutputsModified = _recipeOutputsModified,
        };
    }

    /// <summary>
    ///     Marks the document clean after a successful disk save and updates its base hash and text.
    /// </summary>
    public void MarkSaved(string? updatedSha256, string? updatedTomlText)
    {
        _saveEpoch++;
        IsDirty = false;
        _hasDirectRawTomlEdit = false;
        _bodySpritesModified = false;
        _skillLevelsModified = false;
        _recipeInputsModified = false;
        _recipeOutputsModified = false;
        _modifiedFieldKeys.Clear();
        _removedLocalizationKeys.Clear();
        for (var i = 0; i < LocalizationRows.Count; i++)
        {
            LocalizationRows[i].CommitSavedKey();
        }

        if (!string.IsNullOrWhiteSpace(updatedSha256))
        {
            BaseContentSha256 = updatedSha256;
        }

        if (updatedTomlText != null)
        {
            SetTomlTextInternal(updatedTomlText);
        }
    }

    /// <summary>
    ///     Refreshes the discovered workspace image list and re-resolves body sprite thumbnails.
    /// </summary>
    public void UpdateWorkspaceImages(IReadOnlyList<WorkspaceImageEntry> workspaceImages)
    {
        _workspaceImages = workspaceImages != null ? [.. workspaceImages] : [];
        OnPropertyChanged(nameof(WorkspaceImages));

        if (Kind == AssetKind.Body)
        {
            for (var i = 0; i < _allBodySprites.Count; i++)
            {
                var slot = _allBodySprites[i];
                slot.UpdateResolvedPath(ResolveBodySpriteAbsolutePath(slot.TexturePath));
            }
        }
    }

    /// <summary>
    ///     Generates the next unique default localization key derived from this file's name without colliding with existing rows.
    /// </summary>
    public string GenerateNextUniqueLocalizationKey()
    {
        var prefix = DeriveLocalizationKeyPrefix();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < LocalizationRows.Count; i++)
        {
            var key = LocalizationRows[i].Key.Trim();
            if (key.Length > 0)
            {
                existing.Add(key);
            }
        }

        var baseCandidate = $"{prefix}.new_key";
        if (!existing.Contains(baseCandidate))
        {
            return baseCandidate;
        }

        var suffix = 2;
        while (true)
        {
            var numbered = $"{prefix}.new_key_{suffix}";
            if (!existing.Contains(numbered))
            {
                return numbered;
            }

            suffix++;
        }
    }

    /// <summary>
    ///     Updates <see cref="LocalizationRowViewModel.IsDuplicateKey"/> flags across all rows in this localization document.
    /// </summary>
    public void RefreshDuplicateLocalizationKeys()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < LocalizationRows.Count; i++)
        {
            var trimmed = LocalizationRows[i].Key.Trim();
            if (trimmed.Length > 0)
            {
                counts[trimmed] = counts.TryGetValue(trimmed, out var count) ? count + 1 : 1;
            }
        }

        for (var i = 0; i < LocalizationRows.Count; i++)
        {
            var trimmed = LocalizationRows[i].Key.Trim();
            LocalizationRows[i].IsDuplicateKey = trimmed.Length > 0
                && counts.TryGetValue(trimmed, out var count)
                && count > 1;
        }
    }

    /// <summary>
    ///     Returns whether <paramref name="fieldKey"/> is currently marked modified in the draft.
    /// </summary>
    public bool IsFieldModified(string fieldKey) => _modifiedFieldKeys.Contains(fieldKey);

    /// <summary>
    ///     Reads the active ViewModel or WorldModel Position, Rotation (degrees), and Scale vectors from this item document.
    /// </summary>
    public (Vector3 Position, Vector3 Rotation, Vector3 Scale) ReadItemModelTransform(bool isWorldModel)
    {
        StudioFieldViewModel? posField = isWorldModel ? ItemWorldModelPositionField : ItemViewModelPositionField;
        StudioFieldViewModel? rotField = isWorldModel ? ItemWorldModelRotationField : ItemViewModelRotationField;
        StudioFieldViewModel? scaleField = isWorldModel ? ItemWorldModelScaleField : ItemViewModelScaleField;

        Vector3 defaultPos = isWorldModel ? ModelTransformMath.DefaultWorldModelPosition : ModelTransformMath.DefaultViewModelPosition;
        Vector3 defaultRot = isWorldModel ? ModelTransformMath.DefaultWorldModelRotation : ModelTransformMath.DefaultViewModelRotation;
        Vector3 defaultScale = isWorldModel ? ModelTransformMath.DefaultWorldModelScale : ModelTransformMath.DefaultViewModelScale;

        return (
            ModelTransformMath.ParseFloat3(posField?.Value, defaultPos),
            ModelTransformMath.ParseFloat3(rotField?.Value, defaultRot),
            ModelTransformMath.ParseFloat3(scaleField?.Value, defaultScale));
    }

    /// <summary>
    ///     Updates the ViewModel or WorldModel Position, Rotation, and Scale fields atomically with optional undo recording.
    /// </summary>
    public void ApplyItemModelTransform(
        bool isWorldModel,
        Vector3 position,
        Vector3 rotationDegrees,
        Vector3 scale,
        string undoDescription,
        string? coalesceKey = null,
        bool recordUndo = true)
    {
        if (Kind != AssetKind.Item || IsReadOnly)
        {
            return;
        }

        StudioFieldViewModel? posField = isWorldModel ? ItemWorldModelPositionField : ItemViewModelPositionField;
        StudioFieldViewModel? rotField = isWorldModel ? ItemWorldModelRotationField : ItemViewModelRotationField;
        StudioFieldViewModel? scaleField = isWorldModel ? ItemWorldModelScaleField : ItemViewModelScaleField;
        if (posField == null || rotField == null || scaleField == null)
        {
            return;
        }

        string newPos = ModelTransformMath.FormatFloat3(position);
        string newRot = ModelTransformMath.FormatFloat3(rotationDegrees);
        string newScale = ModelTransformMath.FormatFloat3(scale);

        string oldPos = posField.Value;
        string oldRot = rotField.Value;
        string oldScale = scaleField.Value;

        if (string.Equals(oldPos, newPos, StringComparison.Ordinal)
            && string.Equals(oldRot, newRot, StringComparison.Ordinal)
            && string.Equals(oldScale, newScale, StringComparison.Ordinal))
        {
            return;
        }

        var prevModified = CaptureModelTransformModifiedSet(isWorldModel);
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        ApplyItemModelFieldsReplay(isWorldModel, newPos, newRot, newScale);
        MarkItemModelFieldsModified(
            isWorldModel,
            oldPos != newPos,
            oldRot != newRot,
            oldScale != newScale);

        if (recordUndo)
        {
            RecordUndo(
                undoDescription,
                coalesceKey,
                undo: () =>
                {
                    ApplyItemModelFieldsReplay(isWorldModel, oldPos, oldRot, oldScale);
                    RestoreModelTransformModifiedSet(isWorldModel, prevModified, recordedEpoch);
                    RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                    RefreshTomlAndNotify();
                },
                redo: () =>
                {
                    ApplyItemModelFieldsReplay(isWorldModel, newPos, newRot, newScale);
                    MarkItemModelFieldsModified(isWorldModel, true, true, true);
                    MarkMutated();
                });
        }

        MarkMutated();
    }

    /// <summary>
    ///     Commits one atomic undo/redo entry summarizing an interactive gizmo or slider drag.
    /// </summary>
    public void CommitItemModelTransformUndo(
        bool isWorldModel,
        Vector3 beforePosition,
        Vector3 beforeRotationDegrees,
        Vector3 beforeScale,
        Vector3 afterPosition,
        Vector3 afterRotationDegrees,
        Vector3 afterScale,
        bool wasDirtyBeforeDrag,
        IReadOnlySet<string> previouslyModifiedKeys,
        string undoDescription)
    {
        if (Kind != AssetKind.Item || IsReadOnly)
        {
            return;
        }

        string oldPos = ModelTransformMath.FormatFloat3(beforePosition);
        string oldRot = ModelTransformMath.FormatFloat3(beforeRotationDegrees);
        string oldScale = ModelTransformMath.FormatFloat3(beforeScale);

        string newPos = ModelTransformMath.FormatFloat3(afterPosition);
        string newRot = ModelTransformMath.FormatFloat3(afterRotationDegrees);
        string newScale = ModelTransformMath.FormatFloat3(afterScale);

        if (string.Equals(oldPos, newPos, StringComparison.Ordinal)
            && string.Equals(oldRot, newRot, StringComparison.Ordinal)
            && string.Equals(oldScale, newScale, StringComparison.Ordinal))
        {
            return;
        }

        var recordedEpoch = _saveEpoch;
        var prevSetCopy = new HashSet<string>(previouslyModifiedKeys, StringComparer.Ordinal);
        RecordUndo(
            undoDescription,
            coalesceKey: null,
            undo: () =>
            {
                ApplyItemModelFieldsReplay(isWorldModel, oldPos, oldRot, oldScale);
                RestoreModelTransformModifiedSet(isWorldModel, prevSetCopy, recordedEpoch);
                RestoreDirtyAfterUndo(wasDirtyBeforeDrag, recordedEpoch);
                RefreshTomlAndNotify();
            },
            redo: () =>
            {
                ApplyItemModelFieldsReplay(isWorldModel, newPos, newRot, newScale);
                MarkItemModelFieldsModified(isWorldModel, true, true, true);
                MarkMutated();
            });
    }

    private void ApplyItemModelFieldsReplay(bool isWorldModel, string pos, string rot, string scale)
    {
        StudioFieldViewModel? posField = isWorldModel ? ItemWorldModelPositionField : ItemViewModelPositionField;
        StudioFieldViewModel? rotField = isWorldModel ? ItemWorldModelRotationField : ItemViewModelRotationField;
        StudioFieldViewModel? scaleField = isWorldModel ? ItemWorldModelScaleField : ItemViewModelScaleField;

        posField?.ApplyValueForReplay(pos);
        rotField?.ApplyValueForReplay(rot);
        scaleField?.ApplyValueForReplay(scale);
    }

    private void MarkItemModelFieldsModified(bool isWorldModel, bool posChanged, bool rotChanged, bool scaleChanged)
    {
        string prefix = isWorldModel ? "WorldModel" : "ViewModel";
        bool hadInitial = isWorldModel ? _hasInitialWorldModelTable : _hasInitialViewModelTable;
        // When a brand-new ViewModel/WorldModel table is materialized, write all 3 vectors so runtime defaults stay faithful.
        bool forceAll = !hadInitial;
        if (forceAll || posChanged)
        {
            _modifiedFieldKeys.Add($"{prefix}Position");
        }

        if (forceAll || rotChanged)
        {
            _modifiedFieldKeys.Add($"{prefix}Rotation");
        }

        if (forceAll || scaleChanged)
        {
            _modifiedFieldKeys.Add($"{prefix}Scale");
        }
    }

    private HashSet<string> CaptureModelTransformModifiedSet(bool isWorldModel)
    {
        string prefix = isWorldModel ? "WorldModel" : "ViewModel";
        var set = new HashSet<string>(StringComparer.Ordinal);
        string[] keys = [$"{prefix}Position", $"{prefix}Rotation", $"{prefix}Scale"];
        for (var i = 0; i < keys.Length; i++)
        {
            if (_modifiedFieldKeys.Contains(keys[i]))
            {
                set.Add(keys[i]);
            }
        }

        return set;
    }

    private void RestoreModelTransformModifiedSet(bool isWorldModel, IReadOnlySet<string> previous, int recordedEpoch)
    {
        if (_saveEpoch != recordedEpoch)
        {
            return;
        }

        string prefix = isWorldModel ? "WorldModel" : "ViewModel";
        string[] keys = [$"{prefix}Position", $"{prefix}Rotation", $"{prefix}Scale"];
        for (var i = 0; i < keys.Length; i++)
        {
            if (previous.Contains(keys[i]))
            {
                _modifiedFieldKeys.Add(keys[i]);
            }
            else
            {
                _modifiedFieldKeys.Remove(keys[i]);
            }
        }
    }

    /// <summary>
    ///     Finds a scalar field view model by canonical field key.
    /// </summary>
    public StudioFieldViewModel? FindField(string? fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
        {
            return null;
        }

        for (var i = 0; i < _allFields.Count; i++)
        {
            if (string.Equals(_allFields[i].FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
            {
                return _allFields[i];
            }
        }

        return null;
    }

    /// <summary>
    ///     Highlights the target field and clears highlights on all other fields.
    /// </summary>
    public bool HighlightField(string? fieldKey)
    {
        var matched = false;
        for (var i = 0; i < _allFields.Count; i++)
        {
            var isMatch = !string.IsNullOrWhiteSpace(fieldKey)
                && string.Equals(_allFields[i].FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase);
            _allFields[i].IsHighlighted = isMatch;
            if (isMatch)
            {
                matched = true;
            }
        }

        return matched;
    }

    /// <summary>
    ///     Appends a newly created localization key into this localization table document.
    /// </summary>
    public void AddLocalizationEntry(string key, string englishText)
    {
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;
        var safeKey = key?.Trim() ?? string.Empty;
        var safeEnglish = englishText ?? string.Empty;
        var wasPreviouslyRemoved = safeKey.Length > 0 && _removedLocalizationKeys.Remove(safeKey);
        var pseudo = $"[!! {safeEnglish} ··· !!]";
        var row = CreateLocalizationRow(
            safeKey,
            safeEnglish,
            pseudo,
            string.Equals(SelectedLocale, "qps-ploc", StringComparison.OrdinalIgnoreCase));
        LocalizationRows.Add(row);
        RefreshDuplicateLocalizationKeys();
        RefreshFilteredLocalizationRows();
        OnPropertyChanged(nameof(DataSummaryMetric));

        RecordUndo(
            $"Add key {safeKey}",
            coalesceKey: null,
            undo: () =>
            {
                LocalizationRows.Remove(row);
                if (wasPreviouslyRemoved)
                {
                    _removedLocalizationKeys.Add(safeKey);
                }

                RefreshDuplicateLocalizationKeys();
                RefreshFilteredLocalizationRows();
                NewLocalizationKey = GenerateNextUniqueLocalizationKey();
                OnPropertyChanged(nameof(DataSummaryMetric));
                RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                RefreshTomlAndNotify();
            },
            redo: () =>
            {
                if (!LocalizationRows.Contains(row))
                {
                    LocalizationRows.Add(row);
                }

                if (safeKey.Length > 0)
                {
                    _removedLocalizationKeys.Remove(safeKey);
                }

                RefreshDuplicateLocalizationKeys();
                RefreshFilteredLocalizationRows();
                NewLocalizationKey = GenerateNextUniqueLocalizationKey();
                OnPropertyChanged(nameof(DataSummaryMetric));
                MarkMutated();
            });

        MarkMutated();
    }

    /// <summary>
    ///     Applies a localization key already persisted to disk into this document without marking it dirty or recording undo.
    /// </summary>
    public void AddPersistedLocalizationEntry(
        string key,
        string englishText,
        string? updatedSha256,
        string? updatedTomlText)
    {
        var pseudo = $"[!! {englishText} ··· !!]";
        LocalizationRowViewModel? existing = null;
        for (var i = 0; i < LocalizationRows.Count; i++)
        {
            if (string.Equals(LocalizationRows[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                existing = LocalizationRows[i];
                break;
            }
        }

        if (existing != null)
        {
            existing.ApplyValueForReplay(pseudoLocale: false, englishText);
            existing.ApplyValueForReplay(pseudoLocale: true, pseudo);
        }
        else
        {
            var row = CreateLocalizationRow(
                key,
                englishText,
                pseudo,
                string.Equals(SelectedLocale, "qps-ploc", StringComparison.OrdinalIgnoreCase));
            LocalizationRows.Add(row);
        }

        RefreshDuplicateLocalizationKeys();
        RefreshFilteredLocalizationRows();
        NewLocalizationKey = GenerateNextUniqueLocalizationKey();
        OnPropertyChanged(nameof(DataSummaryMetric));

        if (!IsDirty)
        {
            MarkSaved(updatedSha256, updatedTomlText);
        }
        else if (!string.IsNullOrWhiteSpace(updatedSha256))
        {
            BaseContentSha256 = updatedSha256;
        }
    }

    /// <summary>
    ///     Moves a tag entry from <paramref name="fromIndex"/> to <paramref name="toIndex"/> with undo support.
    /// </summary>
    public bool MoveTag(int fromIndex, int toIndex)
    {
        if (IsReadOnly
            || fromIndex < 0
            || fromIndex >= _tagFileValues.Count
            || toIndex < 0
            || toIndex >= _tagFileValues.Count
            || fromIndex == toIndex)
        {
            return false;
        }

        var before = new List<string>(_tagFileValues);
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        var moved = _tagFileValues[fromIndex];
        _tagFileValues.RemoveAt(fromIndex);
        _tagFileValues.Insert(toIndex, moved);
        var after = new List<string>(_tagFileValues);

        RecordUndo(
            "Reorder tag",
            coalesceKey: null,
            undo: () => ApplyTagValuesSnapshot(before, wasDirty, recordedEpoch, isUndo: true),
            redo: () => ApplyTagValuesSnapshot(after, true, recordedEpoch, isUndo: false));

        TriggerOneTimeOrderWarning();
        RebuildTagLists();
        MarkMutated();
        return true;
    }

    /// <summary>
    ///     Applies a clean formatted TOML regeneration without discarding field state.
    /// </summary>
    public void FormatToml()
    {
        if (HasSyntaxError)
        {
            return;
        }

        SetTomlTextInternal(GenerateTomlText());
        IsDirty = true;
    }

    /// <summary>
    ///     Updates document writability when toggling between pack and repository workspace modes.
    /// </summary>
    public void UpdateReadOnlyForMode(WorkspaceMode mode)
    {
        IsReadOnly = mode == WorkspaceMode.Pack && Provenance == AssetProvenance.Base;
        for (var i = 0; i < _allFields.Count; i++)
        {
            _allFields[i].IsReadOnly = IsReadOnly;
        }

        OnPropertyChanged(nameof(CanRenameId));
        OnPropertyChanged(nameof(CanRemoveSelectedBodyState));
    }

    [RelayCommand]
    private void AddCollider()
    {
        if (IsReadOnly)
        {
            return;
        }

        var before = SnapshotColliders();
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        Colliders.Add(CreateColliderEntry("box", "1.0, 1.0, 1.0", "0.0, 0.0, 0.0", "0, 0, 0"));
        var after = SnapshotColliders();

        RecordUndo(
            "Add collider",
            coalesceKey: null,
            undo: () => RestoreCollidersSnapshot(before, wasDirty, recordedEpoch, isUndo: true),
            redo: () => RestoreCollidersSnapshot(after, true, recordedEpoch, isUndo: false));

        MarkMutated();
    }

    [RelayCommand]
    private void RemoveCollider(ColliderEntryViewModel? collider)
    {
        if (IsReadOnly || collider == null || !Colliders.Contains(collider))
        {
            return;
        }

        var before = SnapshotColliders();
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        Colliders.Remove(collider);
        var after = SnapshotColliders();

        RecordUndo(
            "Remove collider",
            coalesceKey: null,
            undo: () => RestoreCollidersSnapshot(before, wasDirty, recordedEpoch, isUndo: true),
            redo: () => RestoreCollidersSnapshot(after, true, recordedEpoch, isUndo: false));

        MarkMutated();
    }

    [RelayCommand]
    private void AddSkillSource()
    {
        if (IsReadOnly)
        {
            return;
        }

        var before = SnapshotSkillSources();
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        SkillSources.Add(CreateSkillSourceRow("Place", "wb:panel", 2));
        var after = SnapshotSkillSources();

        RecordUndo(
            "Add XP source",
            coalesceKey: null,
            undo: () => RestoreSkillSourcesSnapshot(before, wasDirty, recordedEpoch, isUndo: true),
            redo: () => RestoreSkillSourcesSnapshot(after, true, recordedEpoch, isUndo: false));

        OnPropertyChanged(nameof(DataSummaryMetric));
        MarkMutated();
    }

    [RelayCommand]
    private void RemoveSkillSource(SkillSourceRowViewModel? row)
    {
        if (IsReadOnly || row == null || !SkillSources.Contains(row))
        {
            return;
        }

        var before = SnapshotSkillSources();
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        SkillSources.Remove(row);
        var after = SnapshotSkillSources();

        RecordUndo(
            "Remove XP source",
            coalesceKey: null,
            undo: () => RestoreSkillSourcesSnapshot(before, wasDirty, recordedEpoch, isUndo: true),
            redo: () => RestoreSkillSourcesSnapshot(after, true, recordedEpoch, isUndo: false));

        OnPropertyChanged(nameof(DataSummaryMetric));
        MarkMutated();
    }

    [RelayCommand]
    private void AddRecipeInput()
    {
        if (IsReadOnly)
        {
            return;
        }

        var before = SnapshotRecipeInputs();
        var wasDirty = IsDirty;
        var wasModified = _recipeInputsModified;
        var recordedEpoch = _saveEpoch;

        var defaultItem = FirstItemCandidateId();
        RecipeInputs.Add(CreateRecipeInputRow(defaultItem, 1, string.Empty));
        _recipeInputsModified = true;
        var after = SnapshotRecipeInputs();

        RecordUndo(
            "Add recipe input",
            coalesceKey: null,
            undo: () => RestoreRecipeInputsSnapshot(before, wasDirty, wasModified, recordedEpoch, isUndo: true),
            redo: () => RestoreRecipeInputsSnapshot(after, true, true, recordedEpoch, isUndo: false));

        NotifyRecipePreviewChanged();
        MarkMutated();
    }

    [RelayCommand]
    private void RemoveRecipeInput(RecipeIngredientRowViewModel? row)
    {
        if (IsReadOnly || row == null || !RecipeInputs.Contains(row))
        {
            return;
        }

        var before = SnapshotRecipeInputs();
        var wasDirty = IsDirty;
        var wasModified = _recipeInputsModified;
        var recordedEpoch = _saveEpoch;

        RecipeInputs.Remove(row);
        _recipeInputsModified = true;
        var after = SnapshotRecipeInputs();

        RecordUndo(
            "Remove recipe input",
            coalesceKey: null,
            undo: () => RestoreRecipeInputsSnapshot(before, wasDirty, wasModified, recordedEpoch, isUndo: true),
            redo: () => RestoreRecipeInputsSnapshot(after, true, true, recordedEpoch, isUndo: false));

        NotifyRecipePreviewChanged();
        MarkMutated();
    }

    [RelayCommand]
    private void AddRecipeOutput()
    {
        if (IsReadOnly)
        {
            return;
        }

        var before = SnapshotRecipeOutputs();
        var wasDirty = IsDirty;
        var wasModified = _recipeOutputsModified;
        var recordedEpoch = _saveEpoch;

        var defaultItem = FirstItemCandidateId();
        RecipeOutputs.Add(CreateRecipeOutputRow(defaultItem, 1, string.Empty));
        _recipeOutputsModified = true;
        var after = SnapshotRecipeOutputs();

        RecordUndo(
            "Add recipe output",
            coalesceKey: null,
            undo: () => RestoreRecipeOutputsSnapshot(before, wasDirty, wasModified, recordedEpoch, isUndo: true),
            redo: () => RestoreRecipeOutputsSnapshot(after, true, true, recordedEpoch, isUndo: false));

        NotifyRecipePreviewChanged();
        MarkMutated();
    }

    [RelayCommand]
    private void RemoveRecipeOutput(RecipeOutputRowViewModel? row)
    {
        if (IsReadOnly || row == null || !RecipeOutputs.Contains(row))
        {
            return;
        }

        var before = SnapshotRecipeOutputs();
        var wasDirty = IsDirty;
        var wasModified = _recipeOutputsModified;
        var recordedEpoch = _saveEpoch;

        RecipeOutputs.Remove(row);
        _recipeOutputsModified = true;
        var after = SnapshotRecipeOutputs();

        RecordUndo(
            "Remove recipe output",
            coalesceKey: null,
            undo: () => RestoreRecipeOutputsSnapshot(before, wasDirty, wasModified, recordedEpoch, isUndo: true),
            redo: () => RestoreRecipeOutputsSnapshot(after, true, true, recordedEpoch, isUndo: false));

        NotifyRecipePreviewChanged();
        MarkMutated();
    }

    private RecipeIngredientRowViewModel CreateRecipeInputRow(string itemId, int count, string slotGroup)
    {
        RecipeIngredientRowViewModel? row = null;
        row = new RecipeIngredientRowViewModel(
            itemId,
            count,
            slotGroup,
            onChanged: () =>
            {
                row?.RefreshResolution(_allReferenceCandidates);
                _recipeInputsModified = true;
                NotifyRecipePreviewChanged();
                MarkMutated();
            },
            onPropertyTransition: (targetRow, propertyName, oldValue, newValue) =>
            {
                if (_isUndoReplaying != null && _isUndoReplaying())
                {
                    return;
                }

                var wasDirty = IsDirty;
                var wasModified = _recipeInputsModified;
                var recordedEpoch = _saveEpoch;
                RecordUndo(
                    $"Edit recipe input {propertyName}",
                    coalesceKey: $"recipe-input:{Id}:{targetRow.GetHashCode()}:{propertyName}",
                    undo: () =>
                    {
                        targetRow.ApplyPropertyForReplay(propertyName, oldValue);
                        targetRow.RefreshResolution(_allReferenceCandidates);
                        _recipeInputsModified = _saveEpoch == recordedEpoch ? wasModified : true;
                        NotifyRecipePreviewChanged();
                        RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                        RefreshTomlAndNotify();
                    },
                    redo: () =>
                    {
                        targetRow.ApplyPropertyForReplay(propertyName, newValue);
                        targetRow.RefreshResolution(_allReferenceCandidates);
                        _recipeInputsModified = true;
                        NotifyRecipePreviewChanged();
                        MarkMutated();
                    });
            },
            pickerCandidates: BuildRecipeInputCandidates(),
            isReadOnly: IsReadOnly);
        row.RefreshResolution(_allReferenceCandidates);
        return row;
    }

    private RecipeOutputRowViewModel CreateRecipeOutputRow(string itemId, int count, string chance)
    {
        RecipeOutputRowViewModel? row = null;
        row = new RecipeOutputRowViewModel(
            itemId,
            count,
            chance,
            onChanged: () =>
            {
                row?.RefreshResolution(_allReferenceCandidates);
                _recipeOutputsModified = true;
                NotifyRecipePreviewChanged();
                MarkMutated();
            },
            onPropertyTransition: (targetRow, propertyName, oldValue, newValue) =>
            {
                if (_isUndoReplaying != null && _isUndoReplaying())
                {
                    return;
                }

                var wasDirty = IsDirty;
                var wasModified = _recipeOutputsModified;
                var recordedEpoch = _saveEpoch;
                RecordUndo(
                    $"Edit recipe output {propertyName}",
                    coalesceKey: $"recipe-output:{Id}:{targetRow.GetHashCode()}:{propertyName}",
                    undo: () =>
                    {
                        targetRow.ApplyPropertyForReplay(propertyName, oldValue);
                        targetRow.RefreshResolution(_allReferenceCandidates);
                        _recipeOutputsModified = _saveEpoch == recordedEpoch ? wasModified : true;
                        NotifyRecipePreviewChanged();
                        RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                        RefreshTomlAndNotify();
                    },
                    redo: () =>
                    {
                        targetRow.ApplyPropertyForReplay(propertyName, newValue);
                        targetRow.RefreshResolution(_allReferenceCandidates);
                        _recipeOutputsModified = true;
                        NotifyRecipePreviewChanged();
                        MarkMutated();
                    });
            },
            pickerCandidates: FilterCandidates(_allReferenceCandidates, ReferenceKind.ItemId),
            isReadOnly: IsReadOnly);
        row.RefreshResolution(_allReferenceCandidates);
        return row;
    }

    private IReadOnlyList<ReferenceCandidate> BuildRecipeInputCandidates()
    {
        var list = new List<ReferenceCandidate>();
        for (var i = 0; i < _allReferenceCandidates.Count; i++)
        {
            var candidate = _allReferenceCandidates[i];
            if (candidate.Kind == ReferenceKind.ItemId)
            {
                list.Add(candidate);
            }
        }

        for (var i = 0; i < _allReferenceCandidates.Count; i++)
        {
            var candidate = _allReferenceCandidates[i];
            if (candidate.Kind == ReferenceKind.Tag)
            {
                list.Add(candidate with
                {
                    Value = $"tag:{candidate.Value}",
                    Kind = ReferenceKind.ItemId,
                });
            }
        }

        return list;
    }

    private List<(string ItemId, int Count, string SlotGroup)> SnapshotRecipeInputs()
    {
        var list = new List<(string, int, string)>(RecipeInputs.Count);
        for (var i = 0; i < RecipeInputs.Count; i++)
        {
            list.Add((RecipeInputs[i].ItemId, RecipeInputs[i].Count, RecipeInputs[i].SlotGroup));
        }

        return list;
    }

    private void RestoreRecipeInputsSnapshot(
        List<(string ItemId, int Count, string SlotGroup)> snapshot,
        bool targetDirty,
        bool targetModified,
        int recordedEpoch,
        bool isUndo)
    {
        RecipeInputs.Clear();
        for (var i = 0; i < snapshot.Count; i++)
        {
            RecipeInputs.Add(CreateRecipeInputRow(snapshot[i].ItemId, snapshot[i].Count, snapshot[i].SlotGroup));
        }

        _recipeInputsModified = isUndo && _saveEpoch == recordedEpoch ? targetModified : true;
        NotifyRecipePreviewChanged();
        if (isUndo)
        {
            RestoreDirtyAfterUndo(targetDirty, recordedEpoch);
            RefreshTomlAndNotify();
        }
        else
        {
            MarkMutated();
        }
    }

    private List<(string ItemId, int Count, string Chance)> SnapshotRecipeOutputs()
    {
        var list = new List<(string, int, string)>(RecipeOutputs.Count);
        for (var i = 0; i < RecipeOutputs.Count; i++)
        {
            list.Add((RecipeOutputs[i].ItemId, RecipeOutputs[i].Count, RecipeOutputs[i].Chance));
        }

        return list;
    }

    private void RestoreRecipeOutputsSnapshot(
        List<(string ItemId, int Count, string Chance)> snapshot,
        bool targetDirty,
        bool targetModified,
        int recordedEpoch,
        bool isUndo)
    {
        RecipeOutputs.Clear();
        for (var i = 0; i < snapshot.Count; i++)
        {
            RecipeOutputs.Add(CreateRecipeOutputRow(snapshot[i].ItemId, snapshot[i].Count, snapshot[i].Chance));
        }

        _recipeOutputsModified = isUndo && _saveEpoch == recordedEpoch ? targetModified : true;
        NotifyRecipePreviewChanged();
        if (isUndo)
        {
            RestoreDirtyAfterUndo(targetDirty, recordedEpoch);
            RefreshTomlAndNotify();
        }
        else
        {
            MarkMutated();
        }
    }

    private string FirstItemCandidateId()
    {
        for (var i = 0; i < _allReferenceCandidates.Count; i++)
        {
            if (_allReferenceCandidates[i].Kind == ReferenceKind.ItemId)
            {
                return _allReferenceCandidates[i].Value;
            }
        }

        return string.Empty;
    }

    private double ParseRecipeDurationSeconds()
    {
        var raw = RecipeDurationField?.Value?.Trim();
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && double.IsFinite(seconds)
            ? Math.Max(0.0, seconds)
            : 0.0;
    }

    private double ParseRecipePowerKw()
    {
        var raw = RecipePowerKwField?.Value?.Trim();
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var kw) && double.IsFinite(kw)
            ? kw
            : 0.0;
    }

    private void NotifyRecipePreviewChanged()
    {
        OnPropertyChanged(nameof(RecipeTargetTagLabel));
        OnPropertyChanged(nameof(HasRecipeTargetTag));
        OnPropertyChanged(nameof(RecipeModeBadge));
        OnPropertyChanged(nameof(IsRecipeAutoMode));
        OnPropertyChanged(nameof(RecipeDurationLabel));
        OnPropertyChanged(nameof(RecipePowerLabel));
        OnPropertyChanged(nameof(IsRecipePowerGeneration));
        OnPropertyChanged(nameof(IsRecipePowerConsumption));
        OnPropertyChanged(nameof(RecipeThroughputLabel));
        OnPropertyChanged(nameof(RecipeInputsSummary));
        OnPropertyChanged(nameof(RecipeOutputsSummary));
        OnPropertyChanged(nameof(RecipeHasInputs));
        OnPropertyChanged(nameof(RecipeHasNoInputs));
        OnPropertyChanged(nameof(RecipeHasOutputs));
        OnPropertyChanged(nameof(RecipeHasNoOutputs));
        OnPropertyChanged(nameof(IsRecipeGeneratorWithoutOutputs));
        OnPropertyChanged(nameof(RecipeUnresolvedCount));
        OnPropertyChanged(nameof(RecipeHasUnresolvedRefs));
        OnPropertyChanged(nameof(RecipeUnresolvedSummary));
        OnPropertyChanged(nameof(DataSummaryMetric));
        OnPropertyChanged(nameof(DataSummaryDetail));
    }

    [RelayCommand]
    private void AddSkillLevel()
    {
        if (IsReadOnly)
        {
            return;
        }

        var before = SnapshotSkillLevels();
        var wasDirty = IsDirty;
        var wasModified = _skillLevelsModified;
        var recordedEpoch = _saveEpoch;

        var nextLevel = SkillLevels.Count + 1;
        var nextXp = SkillLevels.Count == 0 ? 100 : SkillLevels[^1].XpRequired + 620;
        var cumulative = SkillLevels.Count == 0 ? nextXp : SkillLevels[^1].CumulativeXp + nextXp;
        SkillLevels.Add(CreateSkillLevelRow(nextLevel, nextXp, cumulative));
        var after = SnapshotSkillLevels();

        RecordUndo(
            "Add skill level",
            coalesceKey: null,
            undo: () => RestoreSkillLevelsSnapshot(before, wasDirty, wasModified, recordedEpoch, isUndo: true),
            redo: () => RestoreSkillLevelsSnapshot(after, true, true, recordedEpoch, isUndo: false));

        HandleSkillCurveChanged();
    }

    [RelayCommand]
    private void RemoveSkillLevel(SkillLevelRowViewModel? row)
    {
        if (IsReadOnly || row == null || SkillLevels.Count <= 1 || !SkillLevels.Contains(row))
        {
            return;
        }

        var before = SnapshotSkillLevels();
        var wasDirty = IsDirty;
        var wasModified = _skillLevelsModified;
        var recordedEpoch = _saveEpoch;

        SkillLevels.Remove(row);
        for (var i = 0; i < SkillLevels.Count; i++)
        {
            SkillLevels[i].Level = i + 1;
        }

        var after = SnapshotSkillLevels();

        RecordUndo(
            "Remove skill level",
            coalesceKey: null,
            undo: () => RestoreSkillLevelsSnapshot(before, wasDirty, wasModified, recordedEpoch, isUndo: true),
            redo: () => RestoreSkillLevelsSnapshot(after, true, true, recordedEpoch, isUndo: false));

        HandleSkillCurveChanged();
    }

    /// <summary>
    ///     Returns an ordered snapshot of the skill level progression curve for clipboard copy operations.
    /// </summary>
    public IReadOnlyList<(int Level, int Xp)> GetSkillLevelCurve()
    {
        if (Kind != AssetKind.Skill || SkillLevels.Count == 0)
        {
            return [];
        }

        return SnapshotSkillLevels();
    }

    /// <summary>
    ///     Replaces the skill level curve rows atomically as a single undoable operation.
    /// </summary>
    public bool ReplaceSkillLevels(IReadOnlyList<(int Level, int Xp)> levels)
    {
        if (Kind != AssetKind.Skill || IsReadOnly || levels == null || levels.Count == 0)
        {
            return false;
        }

        var before = SnapshotSkillLevels();
        var wasDirty = IsDirty;
        var wasModified = _skillLevelsModified;
        var recordedEpoch = _saveEpoch;

        var normalized = new List<(int Level, int Xp)>(levels.Count);
        for (var i = 0; i < levels.Count; i++)
        {
            normalized.Add((i + 1, Math.Max(1, levels[i].Xp)));
        }

        SkillLevels.Clear();
        var cumulative = 0;
        for (var i = 0; i < normalized.Count; i++)
        {
            cumulative += normalized[i].Xp;
            SkillLevels.Add(CreateSkillLevelRow(normalized[i].Level, normalized[i].Xp, cumulative));
        }

        var after = SnapshotSkillLevels();
        RecordUndo(
            "Paste skill level curve",
            coalesceKey: null,
            undo: () => RestoreSkillLevelsSnapshot(before, wasDirty, wasModified, recordedEpoch, isUndo: true),
            redo: () => RestoreSkillLevelsSnapshot(after, true, true, recordedEpoch, isUndo: false));

        HandleSkillCurveChanged();
        return true;
    }

    [RelayCommand]
    private void SelectBodyState(string? stateName)
    {
        if (!string.IsNullOrWhiteSpace(stateName))
        {
            SelectedBodyState = stateName;
        }
    }

    [RelayCommand]
    private void AddBodyState()
    {
        if (IsReadOnly)
        {
            return;
        }

        var candidate = NewBodyStateName.Trim();
        if (candidate.Length == 0)
        {
            _onActionFeedback?.Invoke("Enter a state name (for example walking or floating).");
            return;
        }

        for (var i = 0; i < BodyStates.Count; i++)
        {
            if (string.Equals(BodyStates[i], candidate, StringComparison.OrdinalIgnoreCase))
            {
                SelectedBodyState = BodyStates[i];
                NewBodyStateName = string.Empty;
                return;
            }
        }

        var before = SnapshotBodyState();
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        BodyStates.Add(candidate);
        RebuildBodyStateChips();
        SelectedBodyState = candidate;
        NewBodyStateName = string.Empty;
        _bodySpritesModified = true;

        var after = SnapshotBodyState();
        RecordUndo(
            $"Add body state '{candidate}'",
            coalesceKey: null,
            undo: () => RestoreBodyStateSnapshot(before, wasDirty, recordedEpoch, isUndo: true),
            redo: () => RestoreBodyStateSnapshot(after, true, recordedEpoch, isUndo: false));

        OnPropertyChanged(nameof(CanRemoveSelectedBodyState));
        BodySpritesChanged?.Invoke(this, true);
        MarkMutated();
    }

    [RelayCommand]
    private void RemoveSelectedBodyState()
    {
        if (!CanRemoveSelectedBodyState)
        {
            return;
        }

        var targetState = SelectedBodyState;
        var before = SnapshotBodyState();
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        for (var i = BodyStates.Count - 1; i >= 0; i--)
        {
            if (string.Equals(BodyStates[i], targetState, StringComparison.OrdinalIgnoreCase))
            {
                BodyStates.RemoveAt(i);
            }
        }

        for (var i = _allBodySprites.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_allBodySprites[i].StateName, targetState, StringComparison.OrdinalIgnoreCase))
            {
                _allBodySprites.RemoveAt(i);
            }
        }

        RebuildBodyStateChips();
        SelectedBodyState = BodyStates.Count > 0 ? BodyStates[0] : string.Empty;
        RefreshVisibleBodySprites();
        _bodySpritesModified = true;

        var after = SnapshotBodyState();
        RecordUndo(
            $"Remove body state '{targetState}'",
            coalesceKey: null,
            undo: () => RestoreBodyStateSnapshot(before, wasDirty, recordedEpoch, isUndo: true),
            redo: () => RestoreBodyStateSnapshot(after, true, recordedEpoch, isUndo: false));

        OnPropertyChanged(nameof(CanRemoveSelectedBodyState));
        BodySpritesChanged?.Invoke(this, true);
        MarkMutated();
    }

    [RelayCommand]
    private void AddBodySprite()
    {
        if (IsReadOnly)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedBodyState))
        {
            _onActionFeedback?.Invoke("Add or select a body state before adding a sprite.");
            return;
        }

        var direction = string.IsNullOrWhiteSpace(NewBodySpriteDirection)
            ? "front"
            : NewBodySpriteDirection.Trim().ToLowerInvariant();
        var texturePath = NewBodySpriteTexturePath.Trim();
        if (texturePath.Length == 0)
        {
            _onActionFeedback?.Invoke("Enter a sprite texture path (for example characters/human_standing.png).");
            return;
        }

        var before = SnapshotBodyState();
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        var swatchHex = ResolveDirectionSwatch(direction);
        var absPath = ResolveBodySpriteAbsolutePath(texturePath);

        BodySpriteSlotViewModel? existing = null;
        for (var i = 0; i < _allBodySprites.Count; i++)
        {
            if (string.Equals(_allBodySprites[i].StateName, SelectedBodyState, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_allBodySprites[i].Direction, direction, StringComparison.OrdinalIgnoreCase))
            {
                existing = _allBodySprites[i];
                break;
            }
        }

        if (existing != null)
        {
            existing.ApplyTexturePathForReplay(texturePath, absPath);
        }
        else
        {
            _allBodySprites.Add(CreateBodySpriteSlot(SelectedBodyState, direction, texturePath, swatchHex, absPath));
        }

        NewBodySpriteTexturePath = string.Empty;
        _bodySpritesModified = true;
        RefreshVisibleBodySprites();

        var after = SnapshotBodyState();
        RecordUndo(
            $"Add {SelectedBodyState}/{direction} sprite",
            coalesceKey: null,
            undo: () => RestoreBodyStateSnapshot(before, wasDirty, recordedEpoch, isUndo: true),
            redo: () => RestoreBodyStateSnapshot(after, true, recordedEpoch, isUndo: false));

        BodySpritesChanged?.Invoke(this, false);
        MarkMutated();
    }

    [RelayCommand]
    private void RemoveBodySprite(BodySpriteSlotViewModel? slot)
    {
        if (IsReadOnly || slot == null || !_allBodySprites.Contains(slot))
        {
            return;
        }

        var before = SnapshotBodyState();
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        _allBodySprites.Remove(slot);
        _bodySpritesModified = true;
        RefreshVisibleBodySprites();

        var after = SnapshotBodyState();
        RecordUndo(
            $"Remove {slot.StateName}/{slot.Direction} sprite",
            coalesceKey: null,
            undo: () => RestoreBodyStateSnapshot(before, wasDirty, recordedEpoch, isUndo: true),
            redo: () => RestoreBodyStateSnapshot(after, true, recordedEpoch, isUndo: false));

        BodySpritesChanged?.Invoke(this, false);
        MarkMutated();
    }

    [RelayCommand]
    private void AppendTag()
    {
        if (IsReadOnly)
        {
            return;
        }

        var candidate = string.IsNullOrWhiteSpace(NewTagValue)
            ? $"colony:custom_{_tagFileValues.Count + 1}"
            : NewTagValue.Trim();

        var before = new List<string>(_tagFileValues);
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        _tagFileValues.Add(candidate);
        NewTagValue = string.Empty;
        var after = new List<string>(_tagFileValues);

        RecordUndo(
            $"Append tag '{candidate}'",
            coalesceKey: null,
            undo: () => ApplyTagValuesSnapshot(before, wasDirty, recordedEpoch, isUndo: true),
            redo: () => ApplyTagValuesSnapshot(after, true, recordedEpoch, isUndo: false));

        RebuildTagLists();
        MarkMutated();
    }

    [RelayCommand]
    private void MoveTagUp(TagListEntryViewModel? entry)
    {
        if (entry == null)
        {
            return;
        }

        MoveTag(entry.Index, entry.Index - 1);
    }

    [RelayCommand]
    private void MoveTagDown(TagListEntryViewModel? entry)
    {
        if (entry == null)
        {
            return;
        }

        MoveTag(entry.Index, entry.Index + 1);
    }

    [RelayCommand]
    private void DeleteTag(TagListEntryViewModel? entry)
    {
        if (IsReadOnly || entry == null || entry.Index < 0 || entry.Index >= _tagFileValues.Count)
        {
            return;
        }

        var before = new List<string>(_tagFileValues);
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;
        var removedVal = _tagFileValues[entry.Index];

        _tagFileValues.RemoveAt(entry.Index);
        var after = new List<string>(_tagFileValues);

        RecordUndo(
            $"Delete tag '{removedVal}'",
            coalesceKey: null,
            undo: () => ApplyTagValuesSnapshot(before, wasDirty, recordedEpoch, isUndo: true),
            redo: () => ApplyTagValuesSnapshot(after, true, recordedEpoch, isUndo: false));

        RebuildTagLists();
        MarkMutated();
    }

    [RelayCommand]
    private void DismissTagOrderWarning()
    {
        ShowTagOrderWarning = false;
    }

    [RelayCommand]
    private void AddLocalizationRow()
    {
        if (IsReadOnly)
        {
            return;
        }

        var candidate = (NewLocalizationKey ?? string.Empty).Trim();
        var keyToUse = candidate.Length > 0 && !ContainsLocalizationKey(candidate)
            ? candidate
            : GenerateNextUniqueLocalizationKey();
        var initialValue = NewLocalizationValue ?? string.Empty;

        var activeFilter = LocalizationFilterText.Trim();
        if (activeFilter.Length > 0
            && !keyToUse.Contains(activeFilter, StringComparison.OrdinalIgnoreCase)
            && !initialValue.Contains(activeFilter, StringComparison.OrdinalIgnoreCase))
        {
            LocalizationFilterText = string.Empty;
        }

        AddLocalizationEntry(keyToUse, initialValue);
        NewLocalizationValue = string.Empty;
        NewLocalizationKey = GenerateNextUniqueLocalizationKey();
    }

    [RelayCommand]
    private void DeleteLocalizationRow(LocalizationRowViewModel? row)
    {
        if (IsReadOnly || row == null)
        {
            return;
        }

        var index = LocalizationRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;
        var originalKey = row.OriginalKey.Trim();
        var wasAlreadyRemoved = originalKey.Length > 0 && _removedLocalizationKeys.Contains(originalKey);

        LocalizationRows.RemoveAt(index);
        if (originalKey.Length > 0)
        {
            _removedLocalizationKeys.Add(originalKey);
        }

        RefreshDuplicateLocalizationKeys();
        RefreshFilteredLocalizationRows();
        NewLocalizationKey = GenerateNextUniqueLocalizationKey();
        OnPropertyChanged(nameof(DataSummaryMetric));

        RecordUndo(
            $"Delete key '{row.Key}'",
            coalesceKey: null,
            undo: () =>
            {
                if (!LocalizationRows.Contains(row))
                {
                    var insertAt = Math.Clamp(index, 0, LocalizationRows.Count);
                    LocalizationRows.Insert(insertAt, row);
                }

                if (originalKey.Length > 0 && !wasAlreadyRemoved)
                {
                    _removedLocalizationKeys.Remove(originalKey);
                }

                RefreshDuplicateLocalizationKeys();
                RefreshFilteredLocalizationRows();
                NewLocalizationKey = GenerateNextUniqueLocalizationKey();
                OnPropertyChanged(nameof(DataSummaryMetric));
                RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                RefreshTomlAndNotify();
            },
            redo: () =>
            {
                LocalizationRows.Remove(row);
                if (originalKey.Length > 0)
                {
                    _removedLocalizationKeys.Add(originalKey);
                }

                RefreshDuplicateLocalizationKeys();
                RefreshFilteredLocalizationRows();
                NewLocalizationKey = GenerateNextUniqueLocalizationKey();
                OnPropertyChanged(nameof(DataSummaryMetric));
                MarkMutated();
            });

        MarkMutated();
    }

    [RelayCommand]
    private void DuplicateBlueprint()
    {
        if (_onBlueprintAction != null)
        {
            _onBlueprintAction(this, "duplicate", null);
            return;
        }

        _onActionFeedback?.Invoke($"Duplicated blueprint '{BlueprintNameField?.Value}' as copy in pack.");
    }

    [RelayCommand]
    private void ImportBlueprint()
    {
        if (_onBlueprintAction != null)
        {
            _onBlueprintAction(this, "import", null);
            return;
        }

        _onActionFeedback?.Invoke("Opened player blueprint library for import.");
    }

    [RelayCommand]
    private void ExportBlueprint()
    {
        if (_onBlueprintAction != null)
        {
            _onBlueprintAction(this, "export", null);
            return;
        }

        _onActionFeedback?.Invoke($"Exported '{BlueprintNameField?.Value}' to player blueprint library.");
    }

    [RelayCommand]
    private void MoveBlueprint()
    {
        if (_onBlueprintAction != null)
        {
            _onBlueprintAction(this, "move", null);
            return;
        }

        _onActionFeedback?.Invoke("Move blueprint path: changing relative path changes pack blueprint ID.");
    }

    [RelayCommand]
    private void BeginBlueprintDelete()
    {
        IsBlueprintDeleteConfirming = true;
    }

    [RelayCommand]
    private void CancelBlueprintDelete()
    {
        IsBlueprintDeleteConfirming = false;
    }

    [RelayCommand]
    private void ConfirmBlueprintDelete()
    {
        IsBlueprintDeleteConfirming = false;
        if (_onBlueprintAction != null)
        {
            _onBlueprintAction(this, "delete", null);
            return;
        }

        _onActionFeedback?.Invoke($"Confirmed delete for blueprint '{RelativeFile}'.");
    }

    private void TriggerOneTimeOrderWarning()
    {
        if (!_hasWarnedTagReorder)
        {
            _hasWarnedTagReorder = true;
            ShowTagOrderWarning = true;
        }
    }

    private void HandleFieldValueTransition(StudioFieldViewModel field, string oldValue, string newValue)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return;
        }

        var wasDirty = IsDirty;
        var wasModified = _modifiedFieldKeys.Contains(field.FieldKey);
        var recordedEpoch = _saveEpoch;

        RecordUndo(
            $"Edit {field.Label}",
            coalesceKey: $"field:{DocumentKey}:{field.FieldKey}",
            undo: () =>
            {
                field.ApplyValueForReplay(oldValue);
                if (!wasModified && _saveEpoch == recordedEpoch)
                {
                    _modifiedFieldKeys.Remove(field.FieldKey);
                }
                else
                {
                    _modifiedFieldKeys.Add(field.FieldKey);
                }

                SyncSpecialFieldEffects(field);
                RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                RefreshTomlAndNotify();
            },
            redo: () =>
            {
                field.ApplyValueForReplay(newValue);
                _modifiedFieldKeys.Add(field.FieldKey);
                SyncSpecialFieldEffects(field);
                MarkMutated();
            });
    }

    private void HandleFieldChanged(StudioFieldViewModel field)
    {
        _modifiedFieldKeys.Add(field.FieldKey);
        SyncSpecialFieldEffects(field);
        MarkMutated();
    }

    private void SyncSpecialFieldEffects(StudioFieldViewModel field)
    {
        if (field == PaintKindField)
        {
            IsPaintTextureKind = string.Equals(field.Value, "Texture", StringComparison.OrdinalIgnoreCase);
        }
        else if (field == BrickConnectedField)
        {
            OnPropertyChanged(nameof(IsBrickConnected));
        }
        else if (field == ItemPlaceableTypeField)
        {
            SyncItemPlaceableIdField();
        }
        else if (Kind == AssetKind.Recipe)
        {
            NotifyRecipePreviewChanged();
        }
    }

    private void SyncItemPlaceableIdField()
    {
        if (ItemPlaceableTypeField == null || ItemPlaceableIdField == null)
        {
            return;
        }

        string placeableType = ItemPlaceableTypeField.Value.Trim();
        if (string.Equals(placeableType, "blueprint", StringComparison.OrdinalIgnoreCase))
        {
            ItemPlaceableIdField.ReconfigureReference(
                "Target Blueprint ID",
                "blueprint ID · builtin/core",
                ReferenceKind.BlueprintId,
                FilterCandidates(_allReferenceCandidates, ReferenceKind.BlueprintId));
        }
        else if (string.Equals(placeableType, "brick", StringComparison.OrdinalIgnoreCase))
        {
            ItemPlaceableIdField.ReconfigureReference(
                "Target Brick ID",
                "namespaced ID · wb:core",
                ReferenceKind.BrickId,
                FilterCandidates(_allReferenceCandidates, ReferenceKind.BrickId));
        }
        else
        {
            ItemPlaceableIdField.ReconfigureReference(
                "Target ID",
                "brick or blueprint ID",
                referenceKind: null,
                candidates: null);
        }
    }

    private void HandleSkillCurveChanged()
    {
        _skillLevelsModified = true;
        var cumulative = 0;
        for (var i = 0; i < SkillLevels.Count; i++)
        {
            cumulative += Math.Max(1, SkillLevels[i].XpRequired);
            SkillLevels[i].CumulativeXp = cumulative;
        }

        RebuildSparklinePoints();
        OnPropertyChanged(nameof(SkillCurveSummary));
        OnPropertyChanged(nameof(DataSummaryMetric));
        OnPropertyChanged(nameof(DataSummaryDetail));
        MarkMutated();
    }

    private void RebuildSparklinePoints()
    {
        SkillSparklineValues.Clear();
        for (var i = 0; i < SkillLevels.Count; i++)
        {
            SkillSparklineValues.Add(SkillLevels[i].XpRequired);
        }
    }

    private void RebuildBodyStateChips()
    {
        BodyStateChips.Clear();
        for (var i = 0; i < BodyStates.Count; i++)
        {
            var st = BodyStates[i];
            BodyStateChips.Add(new FilterChipViewModel(
                st,
                string.Equals(st, SelectedBodyState, StringComparison.OrdinalIgnoreCase),
                chip => SelectedBodyState = chip.Label));
        }
    }

    private void RefreshVisibleBodySprites()
    {
        VisibleBodySprites.Clear();
        var visited = new HashSet<BodySpriteSlotViewModel>();
        string[] canonicalOrder = ["front", "back", "left", "right"];
        for (var d = 0; d < canonicalOrder.Length; d++)
        {
            var dir = canonicalOrder[d];
            for (var i = 0; i < _allBodySprites.Count; i++)
            {
                var slot = _allBodySprites[i];
                if (string.Equals(slot.StateName, SelectedBodyState, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(slot.Direction, dir, StringComparison.OrdinalIgnoreCase)
                    && visited.Add(slot))
                {
                    VisibleBodySprites.Add(slot);
                }
            }
        }

        for (var i = 0; i < _allBodySprites.Count; i++)
        {
            var slot = _allBodySprites[i];
            if (string.Equals(slot.StateName, SelectedBodyState, StringComparison.OrdinalIgnoreCase)
                && visited.Add(slot))
            {
                VisibleBodySprites.Add(slot);
            }
        }

        OnPropertyChanged(nameof(HasNoVisibleBodySprites));
        OnPropertyChanged(nameof(CanRemoveSelectedBodyState));
    }

    private void RebuildTagLists()
    {
        TagFileEntries.Clear();
        TagEffectiveEntries.Clear();

        var fileCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < _tagFileValues.Count; i++)
        {
            var val = _tagFileValues[i];
            fileCounts[val] = fileCounts.TryGetValue(val, out var existing) ? existing + 1 : 1;
        }

        var hasDupes = false;
        for (var i = 0; i < _tagFileValues.Count; i++)
        {
            var val = _tagFileValues[i];
            var isDup = fileCounts[val] > 1;
            if (isDup)
            {
                hasDupes = true;
            }

            TagFileEntries.Add(new TagListEntryViewModel(i, val, RootName, isDup, false));
        }

        HasDuplicateTags = hasDupes;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var effectiveIdx = 0;
        for (var i = 0; i < _tagBaseValues.Count; i++)
        {
            var val = _tagBaseValues[i];
            if (seen.Add(val))
            {
                TagEffectiveEntries.Add(new TagListEntryViewModel(effectiveIdx++, val, "base", false, true));
            }
        }

        for (var i = 0; i < _tagFileValues.Count; i++)
        {
            var val = _tagFileValues[i];
            if (seen.Add(val))
            {
                TagEffectiveEntries.Add(new TagListEntryViewModel(effectiveIdx++, val, RootName, false, false));
            }
        }

        OnPropertyChanged(nameof(DataSummaryMetric));
        OnPropertyChanged(nameof(DataSummaryDetail));
    }

    private void RefreshFilteredLocalizationRows()
    {
        FilteredLocalizationRows.Clear();
        var query = LocalizationFilterText.Trim();
        for (var i = 0; i < LocalizationRows.Count; i++)
        {
            var row = LocalizationRows[i];
            if (query.Length == 0
                || row.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.ActiveValue.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredLocalizationRows.Add(row);
            }
        }
    }

    private ColliderEntryViewModel CreateColliderEntry(string type, string size, string offset, string rotation)
    {
        return new ColliderEntryViewModel(
            type,
            size,
            offset,
            rotation,
            MarkMutated,
            HandleColliderPropertyTransition);
    }

    private void HandleColliderPropertyTransition(ColliderEntryViewModel entry, string propertyName, string oldValue, string newValue)
    {
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;
        var idx = Colliders.IndexOf(entry);

        RecordUndo(
            $"Edit collider {propertyName}",
            coalesceKey: $"collider:{DocumentKey}:{idx}:{propertyName}",
            undo: () =>
            {
                entry.ApplyPropertyForReplay(propertyName, oldValue);
                RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                RefreshTomlAndNotify();
            },
            redo: () =>
            {
                entry.ApplyPropertyForReplay(propertyName, newValue);
                MarkMutated();
            });
    }

    private SkillSourceRowViewModel CreateSkillSourceRow(string trigger, string target, int xpAmount)
    {
        return new SkillSourceRowViewModel(
            trigger,
            target,
            xpAmount,
            MarkMutated,
            HandleSkillSourcePropertyTransition);
    }

    private void HandleSkillSourcePropertyTransition(SkillSourceRowViewModel row, string propertyName, string oldValue, string newValue)
    {
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;
        var idx = SkillSources.IndexOf(row);

        RecordUndo(
            $"Edit XP source {propertyName}",
            coalesceKey: $"skill-src:{DocumentKey}:{idx}:{propertyName}",
            undo: () =>
            {
                row.ApplyPropertyForReplay(propertyName, oldValue);
                RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                RefreshTomlAndNotify();
            },
            redo: () =>
            {
                row.ApplyPropertyForReplay(propertyName, newValue);
                MarkMutated();
            });
    }

    private SkillLevelRowViewModel CreateSkillLevelRow(int level, int xpRequired, int cumulativeXp)
    {
        return new SkillLevelRowViewModel(
            level,
            xpRequired,
            cumulativeXp,
            HandleSkillCurveChanged,
            HandleSkillLevelXpTransition);
    }

    private void HandleSkillLevelXpTransition(SkillLevelRowViewModel row, int oldXp, int newXp)
    {
        var wasDirty = IsDirty;
        var wasModified = _skillLevelsModified;
        var recordedEpoch = _saveEpoch;

        RecordUndo(
            $"Edit Lv {row.Level} XP",
            coalesceKey: $"skill-lvl:{DocumentKey}:{row.Level}",
            undo: () =>
            {
                row.ApplyXpForReplay(oldXp);
                if (!wasModified && _saveEpoch == recordedEpoch)
                {
                    _skillLevelsModified = false;
                }

                RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                RecomputeSkillCurveWithoutMarkingDirty();
                RefreshTomlAndNotify();
            },
            redo: () =>
            {
                row.ApplyXpForReplay(newXp);
                HandleSkillCurveChanged();
            });
    }

    private BodySpriteSlotViewModel CreateBodySpriteSlot(
        string stateName,
        string direction,
        string texturePath,
        string swatchHex,
        string? resolvedAbsolutePath)
    {
        return new BodySpriteSlotViewModel(
            stateName,
            direction,
            texturePath,
            swatchHex,
            onChanged: HandleBodySpriteSlotMutated,
            resolvedAbsolutePath: resolvedAbsolutePath,
            onTexturePathTransition: HandleBodySpritePathTransition);
    }

    private void HandleBodySpriteSlotMutated()
    {
        _bodySpritesModified = true;
        BodySpritesChanged?.Invoke(this, false);
        MarkMutated();
    }

    private void HandleBodySpritePathTransition(BodySpriteSlotViewModel slot, string oldPath, string newPath)
    {
        var resolvedNew = ResolveBodySpriteAbsolutePath(newPath);
        slot.UpdateResolvedPath(resolvedNew);

        var wasDirty = IsDirty;
        var wasModified = _bodySpritesModified;
        var recordedEpoch = _saveEpoch;

        RecordUndo(
            $"Edit {slot.StateName}/{slot.Direction} sprite",
            coalesceKey: $"body-sprite:{DocumentKey}:{slot.StateName}:{slot.Direction}",
            undo: () =>
            {
                slot.ApplyTexturePathForReplay(oldPath, ResolveBodySpriteAbsolutePath(oldPath));
                if (!wasModified && _saveEpoch == recordedEpoch)
                {
                    _bodySpritesModified = false;
                }

                RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                BodySpritesChanged?.Invoke(this, false);
                RefreshTomlAndNotify();
            },
            redo: () =>
            {
                slot.ApplyTexturePathForReplay(newPath, ResolveBodySpriteAbsolutePath(newPath));
                _bodySpritesModified = true;
                BodySpritesChanged?.Invoke(this, false);
                MarkMutated();
            });
    }

    private string? ResolveBodySpriteAbsolutePath(string? texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            return null;
        }

        var trimmed = texturePath.Trim();
        var normalized = trimmed.Replace('\\', '/').TrimStart('/');
        var stripped = normalized.StartsWith("textures/", StringComparison.OrdinalIgnoreCase)
            ? normalized["textures/".Length..]
            : normalized;

        for (var i = 0; i < _workspaceImages.Count; i++)
        {
            var entry = _workspaceImages[i];
            if ((string.Equals(entry.RelativePath, normalized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.RelativePath, stripped, StringComparison.OrdinalIgnoreCase))
                && File.Exists(entry.AbsolutePath))
            {
                return entry.AbsolutePath;
            }
        }

        var resolved = _resolveTexturePath?.Invoke(trimmed);
        if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
        {
            return resolved;
        }

        if (File.Exists(trimmed))
        {
            return Path.GetFullPath(trimmed);
        }

        return null;
    }

    private LocalizationRowViewModel CreateLocalizationRow(string key, string english, string pseudo, bool usePseudo)
    {
        return new LocalizationRowViewModel(
            key,
            english,
            pseudo,
            usePseudo,
            MarkMutated,
            HandleLocalizationValueTransition,
            HandleLocalizationKeyTransition);
    }

    private void HandleLocalizationKeyTransition(LocalizationRowViewModel row, string oldKey, string newKey)
    {
        RefreshDuplicateLocalizationKeys();
        if (string.Equals(oldKey, newKey, StringComparison.Ordinal))
        {
            return;
        }

        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        RecordUndo(
            $"Rename key '{oldKey}'",
            coalesceKey: $"loc-key:{DocumentKey}:{row.GetHashCode()}",
            undo: () =>
            {
                row.ApplyKeyForReplay(oldKey);
                RefreshDuplicateLocalizationKeys();
                RefreshFilteredLocalizationRows();
                RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                RefreshTomlAndNotify();
            },
            redo: () =>
            {
                row.ApplyKeyForReplay(newKey);
                RefreshDuplicateLocalizationKeys();
                RefreshFilteredLocalizationRows();
                MarkMutated();
            });
    }

    private bool ContainsLocalizationKey(string key)
    {
        for (var i = 0; i < LocalizationRows.Count; i++)
        {
            if (string.Equals(LocalizationRows[i].Key.Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string DeriveLocalizationKeyPrefix()
    {
        var stem = Path.GetFileNameWithoutExtension(RelativeFile);
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = Path.GetFileNameWithoutExtension(Id);
        }

        if (string.IsNullOrWhiteSpace(stem))
        {
            return "entry";
        }

        var sb = new StringBuilder(stem.Length);
        var trimmed = stem.Trim().ToLowerInvariant();
        for (var i = 0; i < trimmed.Length; i++)
        {
            var ch = trimmed[i];
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_')
            {
                sb.Append(ch);
            }
            else if (ch == '-' || ch == ' ')
            {
                sb.Append('_');
            }
        }

        var cleaned = sb.ToString().Trim('_');
        return cleaned.Length > 0 ? cleaned : "entry";
    }

    private void HandleLocalizationValueTransition(LocalizationRowViewModel row, bool pseudoLocale, string oldValue, string newValue)
    {
        var wasDirty = IsDirty;
        var recordedEpoch = _saveEpoch;

        RecordUndo(
            $"Edit {row.Key}",
            coalesceKey: $"loc:{DocumentKey}:{row.Key}:{pseudoLocale}",
            undo: () =>
            {
                row.ApplyValueForReplay(pseudoLocale, oldValue);
                RestoreDirtyAfterUndo(wasDirty, recordedEpoch);
                RefreshTomlAndNotify();
            },
            redo: () =>
            {
                row.ApplyValueForReplay(pseudoLocale, newValue);
                MarkMutated();
            });
    }

    private void RecordUndo(string description, string? coalesceKey, Action undo, Action redo)
    {
        if (_onRecordUndo == null || (_isUndoReplaying != null && _isUndoReplaying()))
        {
            return;
        }

        _onRecordUndo(this, new StudioUndoAction(description, DocumentKey, undo, redo, coalesceKey));
    }

    private void RestoreDirtyAfterUndo(bool wasDirty, int recordedEpoch)
    {
        IsDirty = _saveEpoch == recordedEpoch ? wasDirty : true;
    }

    private void RefreshTomlAndNotify()
    {
        if (!HasSyntaxError && !_hasDirectRawTomlEdit)
        {
            var lossless = _previewEditedToml?.Invoke(BuildSavePayload());
            SetTomlTextInternal(lossless ?? GenerateTomlText());
        }

        _onDocumentMutated?.Invoke(this);
    }

    private void ApplyTagValuesSnapshot(List<string> snapshot, bool targetDirty, int recordedEpoch, bool isUndo)
    {
        _tagFileValues.Clear();
        _tagFileValues.AddRange(snapshot);
        RebuildTagLists();
        if (isUndo)
        {
            RestoreDirtyAfterUndo(targetDirty, recordedEpoch);
            RefreshTomlAndNotify();
        }
        else
        {
            MarkMutated();
        }
    }

    private List<(string Type, string Size, string Offset, string Rotation)> SnapshotColliders()
    {
        var list = new List<(string, string, string, string)>(Colliders.Count);
        for (var i = 0; i < Colliders.Count; i++)
        {
            var c = Colliders[i];
            list.Add((c.ColliderType, c.Size, c.Offset, c.Rotation));
        }

        return list;
    }

    private void RestoreCollidersSnapshot(
        List<(string Type, string Size, string Offset, string Rotation)> snapshot,
        bool targetDirty,
        int recordedEpoch,
        bool isUndo)
    {
        Colliders.Clear();
        for (var i = 0; i < snapshot.Count; i++)
        {
            var c = snapshot[i];
            Colliders.Add(CreateColliderEntry(c.Type, c.Size, c.Offset, c.Rotation));
        }

        if (isUndo)
        {
            RestoreDirtyAfterUndo(targetDirty, recordedEpoch);
            RefreshTomlAndNotify();
        }
        else
        {
            MarkMutated();
        }
    }

    private List<(string Trigger, string Target, int Xp)> SnapshotSkillSources()
    {
        var list = new List<(string, string, int)>(SkillSources.Count);
        for (var i = 0; i < SkillSources.Count; i++)
        {
            var s = SkillSources[i];
            list.Add((s.Trigger, s.Target, s.XpAmount));
        }

        return list;
    }

    private void RestoreSkillSourcesSnapshot(
        List<(string Trigger, string Target, int Xp)> snapshot,
        bool targetDirty,
        int recordedEpoch,
        bool isUndo)
    {
        SkillSources.Clear();
        for (var i = 0; i < snapshot.Count; i++)
        {
            var s = snapshot[i];
            SkillSources.Add(CreateSkillSourceRow(s.Trigger, s.Target, s.Xp));
        }

        OnPropertyChanged(nameof(DataSummaryMetric));
        if (isUndo)
        {
            RestoreDirtyAfterUndo(targetDirty, recordedEpoch);
            RefreshTomlAndNotify();
        }
        else
        {
            MarkMutated();
        }
    }

    private List<(int Level, int Xp)> SnapshotSkillLevels()
    {
        var list = new List<(int, int)>(SkillLevels.Count);
        for (var i = 0; i < SkillLevels.Count; i++)
        {
            list.Add((SkillLevels[i].Level, SkillLevels[i].XpRequired));
        }

        return list;
    }

    private void RestoreSkillLevelsSnapshot(
        List<(int Level, int Xp)> snapshot,
        bool targetDirty,
        bool targetLevelsModified,
        int recordedEpoch,
        bool isUndo)
    {
        SkillLevels.Clear();
        var cumulative = 0;
        for (var i = 0; i < snapshot.Count; i++)
        {
            cumulative += Math.Max(1, snapshot[i].Xp);
            SkillLevels.Add(CreateSkillLevelRow(i + 1, snapshot[i].Xp, cumulative));
        }

        _skillLevelsModified = isUndo && _saveEpoch == recordedEpoch
            ? targetLevelsModified
            : true;

        RecomputeSkillCurveWithoutMarkingDirty();
        if (isUndo)
        {
            RestoreDirtyAfterUndo(targetDirty, recordedEpoch);
            RefreshTomlAndNotify();
        }
        else
        {
            MarkMutated();
        }
    }

    private void RecomputeSkillCurveWithoutMarkingDirty()
    {
        var cumulative = 0;
        for (var i = 0; i < SkillLevels.Count; i++)
        {
            cumulative += Math.Max(1, SkillLevels[i].XpRequired);
            SkillLevels[i].CumulativeXp = cumulative;
        }

        RebuildSparklinePoints();
        OnPropertyChanged(nameof(SkillCurveSummary));
        OnPropertyChanged(nameof(DataSummaryMetric));
        OnPropertyChanged(nameof(DataSummaryDetail));
    }

    private BodyStateSnapshot SnapshotBodyState()
    {
        var sprites = new List<(string State, string Direction, string TexturePath, string SwatchHex, string? AbsPath)>(_allBodySprites.Count);
        for (var i = 0; i < _allBodySprites.Count; i++)
        {
            var s = _allBodySprites[i];
            sprites.Add((s.StateName, s.Direction, s.TexturePath, s.SwatchHex, s.ResolvedAbsolutePath));
        }

        return new BodyStateSnapshot([.. BodyStates], sprites, SelectedBodyState, _bodySpritesModified);
    }

    private void RestoreBodyStateSnapshot(BodyStateSnapshot snapshot, bool targetDirty, int recordedEpoch, bool isUndo)
    {
        BodyStates.Clear();
        for (var i = 0; i < snapshot.States.Count; i++)
        {
            BodyStates.Add(snapshot.States[i]);
        }

        _allBodySprites.Clear();
        for (var i = 0; i < snapshot.Sprites.Count; i++)
        {
            var sp = snapshot.Sprites[i];
            _allBodySprites.Add(CreateBodySpriteSlot(sp.State, sp.Direction, sp.TexturePath, sp.SwatchHex, sp.AbsPath));
        }

        RebuildBodyStateChips();
        SelectedBodyState = snapshot.SelectedState;
        RefreshVisibleBodySprites();
        _bodySpritesModified = isUndo && _saveEpoch == recordedEpoch ? snapshot.WasModified : true;
        BodySpritesChanged?.Invoke(this, true);

        if (isUndo)
        {
            RestoreDirtyAfterUndo(targetDirty, recordedEpoch);
            RefreshTomlAndNotify();
        }
        else
        {
            MarkMutated();
        }
    }

    private void MarkMutated()
    {
        IsDirty = true;
        RefreshTomlAndNotify();
    }

    private void SetTomlTextInternal(string text)
    {
        _suppressRawTomlTracking = true;
        try
        {
            SetProperty(ref _tomlText, text ?? string.Empty, nameof(TomlText));
        }
        finally
        {
            _suppressRawTomlTracking = false;
        }
    }

    private string GenerateTomlText()
    {
        var sb = new StringBuilder();
        switch (Kind)
        {
            case AssetKind.Brick:
                AppendBrickToml(sb);
                break;
            case AssetKind.Item:
                AppendItemToml(sb);
                break;
            case AssetKind.Paint:
                AppendPaintToml(sb);
                break;
            case AssetKind.Skill:
                AppendSkillToml(sb);
                break;
            case AssetKind.Recipe:
                AppendRecipeToml(sb);
                break;
            case AssetKind.Body:
                AppendBodyToml(sb);
                break;
            case AssetKind.Material:
                AppendMaterialToml(sb);
                break;
            case AssetKind.Tag:
                AppendTagToml(sb);
                break;
            case AssetKind.Localization:
                AppendLocalizationToml(sb);
                break;
            case AssetKind.Blueprint:
                sb.AppendLine($"# Blueprint metadata ({RelativeFile})");
                sb.AppendLine($"Name       = \"{BlueprintNameField?.Value}\"");
                sb.AppendLine($"Dimensions = \"{BlueprintSizeLabel}\"");
                sb.AppendLine($"Bricks     = \"{BlueprintBrickCountLabel}\"");
                break;
        }

        return sb.ToString().TrimEnd();
    }

    private void AppendRecipeToml(StringBuilder sb)
    {
        sb.AppendLine("[[Recipes]]");
        sb.AppendLine($"ID              = \"{Id}\"");
        sb.AppendLine($"TargetTag       = {RecipeTargetTagField?.RawTokenText ?? "\"\""}");
        sb.AppendLine($"Mode            = {RecipeModeField?.RawTokenText ?? "\"Manual\""}");
        sb.AppendLine($"DurationSeconds = {RecipeDurationField?.RawTokenText ?? "0"}");
        sb.AppendLine($"PowerKw         = {RecipePowerKwField?.RawTokenText ?? "0"}");
        if (!string.IsNullOrWhiteSpace(RecipeCategoryField?.Value))
        {
            sb.AppendLine($"Category        = {RecipeCategoryField.RawTokenText}");
        }

        sb.AppendLine($"Priority        = {RecipePriorityField?.RawTokenText ?? "0"}");

        for (var i = 0; i < RecipeInputs.Count; i++)
        {
            var input = RecipeInputs[i];
            sb.AppendLine();
            sb.AppendLine("[[Recipes.Inputs]]");
            sb.AppendLine($"ItemId = \"{input.ItemId}\"");
            sb.AppendLine($"Count  = {Math.Max(1, input.Count)}");
            if (!string.IsNullOrWhiteSpace(input.SlotGroup))
            {
                sb.AppendLine($"SlotGroup = \"{input.SlotGroup}\"");
            }
        }

        for (var i = 0; i < RecipeOutputs.Count; i++)
        {
            var output = RecipeOutputs[i];
            sb.AppendLine();
            sb.AppendLine("[[Recipes.Outputs]]");
            sb.AppendLine($"ItemId = \"{output.ItemId}\"");
            sb.AppendLine($"Count  = {Math.Max(1, output.Count)}");
            if (!string.IsNullOrWhiteSpace(output.Chance))
            {
                sb.AppendLine($"Chance = {output.Chance}");
            }
        }
    }

    private void AppendBrickToml(StringBuilder sb)
    {
        sb.AppendLine("# Saved without reformatting untouched sections");
        sb.AppendLine("[[Bricks]]");
        sb.AppendLine($"ID          = \"{Id}\"");
        sb.AppendLine($"Shape       = {BrickShapeField?.RawTokenText}");
        sb.AppendLine($"Color       = {BrickColorField?.RawTokenText}");
        sb.AppendLine($"Alpha       = {BrickAlphaField?.RawTokenText}");
        sb.AppendLine($"CastShadows = {BrickCastShadowsField?.RawTokenText}");
        sb.AppendLine($"Passable    = {BrickPassableField?.RawTokenText}");
        if (!string.IsNullOrWhiteSpace(BrickMeshField?.Value))
        {
            sb.AppendLine($"Mesh        = {BrickMeshField.RawTokenText}");
        }

        sb.AppendLine();
        sb.AppendLine("[Bricks.Textures]");
        sb.AppendLine($"Connected = {BrickConnectedField?.RawTokenText}");
        sb.AppendLine($"Default   = [{BrickTexDefaultField?.RawTokenText}]");
        if (!string.IsNullOrWhiteSpace(BrickTexTopField?.Value))
        {
            sb.AppendLine($"Top       = [{BrickTexTopField.RawTokenText}]");
        }

        if (!string.IsNullOrWhiteSpace(BrickTexFrontField?.Value))
        {
            sb.AppendLine($"Front     = [{BrickTexFrontField.RawTokenText}]");
        }

        if (!string.IsNullOrWhiteSpace(BrickTexBackField?.Value))
        {
            sb.AppendLine($"Back      = [{BrickTexBackField.RawTokenText}]");
        }

        sb.AppendLine();
        sb.AppendLine("[Bricks.Sounds]");
        sb.AppendLine($"Place = [{BrickSoundPlaceField?.RawTokenText}]");
        sb.AppendLine($"Break = [{BrickSoundBreakField?.RawTokenText}]");

        for (var i = 0; i < Colliders.Count; i++)
        {
            var col = Colliders[i];
            sb.AppendLine();
            sb.AppendLine("[[Bricks.Colliders]]");
            sb.AppendLine($"Type   = \"{col.ColliderType}\"");
            sb.AppendLine($"Size   = {{ {FormatVec3(col.Size)} }}");
            sb.AppendLine($"Offset = {{ {FormatVec3(col.Offset)} }}");
        }
    }

    private void AppendItemToml(StringBuilder sb)
    {
        sb.AppendLine("[[Items]]");
        sb.AppendLine($"ID          = \"{Id}\"");
        sb.AppendLine($"Name        = {NameField?.RawTokenText}");
        sb.AppendLine($"Description = {DescriptionField?.RawTokenText}");
        sb.AppendLine($"Icon        = {IconField?.RawTokenText}");
        sb.AppendLine($"MaxStack    = {ItemMaxStackField?.RawTokenText}");
        if (!string.IsNullOrWhiteSpace(ItemPlaceableTypeField?.Value)
            && !string.Equals(ItemPlaceableTypeField.Value, "none", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine();
            sb.AppendLine("[Items.Placeable]");
            sb.AppendLine($"Type = {ItemPlaceableTypeField.RawTokenText}");
            sb.AppendLine($"ID   = {ItemPlaceableIdField?.RawTokenText}");
        }
        sb.AppendLine();
        sb.AppendLine("[Items.ViewModel]");
        if (!string.IsNullOrWhiteSpace(ItemViewModelMeshField?.Value))
        {
            sb.AppendLine($"Mesh       = {ItemViewModelMeshField.RawTokenText}");
        }

        if (!string.IsNullOrWhiteSpace(ItemViewModelMaterialField?.Value))
        {
            sb.AppendLine($"Material   = {ItemViewModelMaterialField.RawTokenText}");
        }

        AppendDottedFloat3(sb, "Position", ItemViewModelPositionField?.Value, ModelTransformMath.DefaultViewModelPosition);
        AppendDottedFloat3(sb, "Rotation", ItemViewModelRotationField?.Value, ModelTransformMath.DefaultViewModelRotation);
        AppendDottedFloat3(sb, "Scale", ItemViewModelScaleField?.Value, ModelTransformMath.DefaultViewModelScale);
        sb.AppendLine();
        sb.AppendLine("# Informational section (unread by client runtime)");
        sb.AppendLine("[Items.WorldModel]");
        if (!string.IsNullOrWhiteSpace(ItemWorldModelMeshField?.Value))
        {
            sb.AppendLine($"Mesh       = {ItemWorldModelMeshField.RawTokenText}");
        }

        if (!string.IsNullOrWhiteSpace(ItemWorldModelMaterialField?.Value))
        {
            sb.AppendLine($"Material   = {ItemWorldModelMaterialField.RawTokenText}");
        }

        AppendDottedFloat3(sb, "Position", ItemWorldModelPositionField?.Value, ModelTransformMath.DefaultWorldModelPosition);
        AppendDottedFloat3(sb, "Rotation", ItemWorldModelRotationField?.Value, ModelTransformMath.DefaultWorldModelRotation);
        AppendDottedFloat3(sb, "Scale", ItemWorldModelScaleField?.Value, ModelTransformMath.DefaultWorldModelScale);
    }

    private static void AppendDottedFloat3(StringBuilder sb, string prefix, string? value, Vector3 fallback)
    {
        Vector3 v = ModelTransformMath.ParseFloat3(value, fallback);
        sb.AppendLine($"{prefix}.X = {ModelTransformMath.FormatComponent(v.X, 4)}");
        sb.AppendLine($"{prefix}.Y = {ModelTransformMath.FormatComponent(v.Y, 4)}");
        sb.AppendLine($"{prefix}.Z = {ModelTransformMath.FormatComponent(v.Z, 4)}");
    }

    private void AppendPaintToml(StringBuilder sb)
    {
        if (HasMultiEntry)
        {
            sb.AppendLine($"# {MultiEntryLabel}");
        }

        sb.AppendLine("[[Paints]]");
        sb.AppendLine($"ID        = \"{Id}\"");
        sb.AppendLine($"Name      = {NameField?.RawTokenText}");
        if (IsPaintTextureKind)
        {
            sb.AppendLine($"Alpha     = {PaintAlphaField?.RawTokenText}");
            sb.AppendLine($"Connected = {PaintConnectedField?.RawTokenText}");
            sb.AppendLine($"Textures  = [\"{PaintVariantsField?.Value.Replace(", ", "\", \"")}\"]");
        }
        else
        {
            sb.AppendLine($"Color     = {PaintColorField?.RawTokenText}");
        }
    }

    private void AppendSkillToml(StringBuilder sb)
    {
        sb.AppendLine("[[Skills]]");
        sb.AppendLine($"ID       = \"{Id}\"");
        sb.AppendLine($"Name     = {NameField?.RawTokenText}");
        sb.AppendLine($"Category = {SkillCategoryField?.RawTokenText}");
        sb.AppendLine($"Icon     = {IconField?.RawTokenText}");
        sb.AppendLine();
        sb.AppendLine("[Skills.Sources.Place]");
        for (var i = 0; i < SkillSources.Count; i++)
        {
            var src = SkillSources[i];
            if (string.Equals(src.Trigger, "Place", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"\"{src.Target}\" = {src.XpAmount}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("[Skills.Levels]");
        for (var i = 0; i < SkillLevels.Count; i++)
        {
            sb.AppendLine($"{SkillLevels[i].Level} = {SkillLevels[i].XpRequired}");
        }
    }

    private void AppendBodyToml(StringBuilder sb)
    {
        sb.AppendLine("[[Bodies]]");
        sb.AppendLine($"ID      = \"{Id}\"");
        sb.AppendLine($"Species = {BodySpeciesField?.RawTokenText}");
        sb.AppendLine($"Gender  = {BodyGenderField?.RawTokenText}");
        for (var s = 0; s < BodyStates.Count; s++)
        {
            var state = BodyStates[s];
            sb.AppendLine();
            sb.AppendLine($"[Bodies.States.{state}]");
            for (var i = 0; i < _allBodySprites.Count; i++)
            {
                var slot = _allBodySprites[i];
                if (string.Equals(slot.StateName, state, StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"{slot.Direction,-5} = [\"{slot.TexturePath}\"]");
                }
            }
        }
    }

    private void AppendMaterialToml(StringBuilder sb)
    {
        sb.AppendLine($"Shader      = {MaterialShaderField?.RawTokenText}");
        sb.AppendLine($"AlphaMode   = {MaterialAlphaModeField?.RawTokenText}");
        sb.AppendLine($"AlphaCutoff = {MaterialAlphaCutoffField?.RawTokenText}");
        sb.AppendLine("Textures    = [");
        sb.AppendLine($"    {MaterialTexturesField?.RawTokenText},");
        sb.AppendLine("]");
    }

    private void AppendTagToml(StringBuilder sb)
    {
        sb.AppendLine("\"Language\" = \"\"");
        sb.AppendLine();
        sb.AppendLine("[Tags]");
        sb.AppendLine($"\"{Id}\" = [");
        for (var i = 0; i < _tagFileValues.Count; i++)
        {
            sb.AppendLine($"    \"{_tagFileValues[i]}\",");
        }

        sb.AppendLine("]");
    }

    private void AppendLocalizationToml(StringBuilder sb)
    {
        sb.AppendLine($"\"Language\" = \"{SelectedLocale}\"");
        sb.AppendLine();
        sb.AppendLine("[Translations]");
        for (var i = 0; i < LocalizationRows.Count; i++)
        {
            sb.AppendLine($"\"{LocalizationRows[i].Key}\" = \"{LocalizationRows[i].ActiveValue}\"");
        }
    }

    private static IReadOnlyList<string> EnsureChoiceIncludesValue(IReadOnlyList<string> choices, string? value)
    {
        var list = new List<string>(choices.Count + 1);
        for (var i = 0; i < choices.Count; i++)
        {
            var option = choices[i];
            if (!string.IsNullOrWhiteSpace(option) && !list.Contains(option, StringComparer.Ordinal))
            {
                list.Add(option);
            }
        }

        if (!string.IsNullOrWhiteSpace(value) && !list.Contains(value, StringComparer.Ordinal))
        {
            list.Add(value);
        }

        return list;
    }

    private static string ResolveDirectionSwatch(string direction)
    {
        return direction.ToLowerInvariant() switch
        {
            "front" => BODY_DIRECTION_SWATCHES[0],
            "back" => BODY_DIRECTION_SWATCHES[1],
            "left" => BODY_DIRECTION_SWATCHES[2],
            "right" => BODY_DIRECTION_SWATCHES[3],
            _ => BODY_DIRECTION_SWATCHES[0],
        };
    }

    private static string FormatVec3(string csv)
    {
        var parts = (csv ?? string.Empty).Split(',');
        var x = parts.Length > 0 ? parts[0].Trim() : "0.0";
        var y = parts.Length > 1 ? parts[1].Trim() : "0.0";
        var z = parts.Length > 2 ? parts[2].Trim() : "0.0";
        return $"X = {x}, Y = {y}, Z = {z}";
    }

    private static IReadOnlyList<ReferenceCandidate> FilterCandidates(IReadOnlyList<ReferenceCandidate> all, ReferenceKind kind)
    {
        var list = new List<ReferenceCandidate>();
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Kind == kind)
            {
                list.Add(all[i]);
            }
        }

        return list;
    }

    private readonly record struct BodyStateSnapshot(
        List<string> States,
        List<(string State, string Direction, string TexturePath, string SwatchHex, string? AbsPath)> Sprites,
        string SelectedState,
        bool WasModified);
}
