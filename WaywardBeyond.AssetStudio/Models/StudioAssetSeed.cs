namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Holds initial field data used to construct a studio document view model.
/// </summary>
public sealed class StudioAssetSeed
{
    /// <summary>
    ///     Unique asset identifier or document path key.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     Asset authoring category.
    /// </summary>
    public required AssetKind Kind { get; init; }

    /// <summary>
    ///     Display group title in the workspace explorer tree.
    /// </summary>
    public required string GroupTitle { get; init; }

    /// <summary>
    ///     Relative path within the providing root.
    /// </summary>
    public required string RelativeFile { get; init; }

    /// <summary>
    ///     Name of the providing workspace root.
    /// </summary>
    public required string RootName { get; init; }

    /// <summary>
    ///     Provenance badge category for this document.
    /// </summary>
    public required AssetProvenance Provenance { get; init; }

    /// <summary>
    ///     Optional lower root name shadowed by this file.
    /// </summary>
    public string? ShadowedRoot { get; init; }

    /// <summary>
    ///     Optional badge text indicating a multi-entry file.
    /// </summary>
    public string? MultiEntryLabel { get; init; }

    /// <summary>
    ///     Whether the document starts with unsaved draft edits.
    /// </summary>
    public bool IsDirty { get; init; }

    /// <summary>
    ///     Whether the file has a TOML syntax error requiring raw text mode.
    /// </summary>
    public bool HasSyntaxError { get; init; }

    /// <summary>
    ///     Optional parser error banner message for malformed TOML files.
    /// </summary>
    public string? SyntaxErrorMessage { get; init; }

    /// <summary>
    ///     Optional raw TOML override text for syntax-error or custom documents.
    /// </summary>
    public string? RawTomlOverride { get; init; }

    /// <summary>
    ///     Optional full filesystem path of the backing document on disk.
    /// </summary>
    public string? AbsoluteFilePath { get; init; }

    /// <summary>
    ///     Zero-based TOML table index inside the backing file, or -1 for root-table files.
    /// </summary>
    public int TableIndex { get; init; } = -1;

    /// <summary>
    ///     Zero-based entry index inside multi-entry array-of-tables files.
    /// </summary>
    public int EntryIndex { get; init; }

    /// <summary>
    ///     SHA-256 hex digest of the backing file bytes when loaded from disk.
    /// </summary>
    public string? BaseContentSha256 { get; init; }

    /// <summary>
    ///     1-based line number of the first TOML syntax error, or 0 when valid.
    /// </summary>
    public int SyntaxErrorLineNumber { get; init; }

    /// <summary>
    ///     Optional absolute path to the front-facing body sprite texture for 2D preview rendering.
    /// </summary>
    public string? ResolvedBodyFrontTexturePath { get; init; }

    /// <summary>
    ///     Optional absolute path to the primary material texture for 2D preview rendering.
    /// </summary>
    public string? ResolvedMaterialTexturePath { get; init; }

    /// <summary>
    ///     Optional absolute path to the primary block albedo texture for texture-based paint previews.
    /// </summary>
    public string? ResolvedPaintTexturePath { get; init; }

    /// <summary>
    ///     Optional absolute file paths for body sprite slots keyed by <c>state:direction</c>.
    /// </summary>
    public Dictionary<string, string> BodySpriteAbsolutePaths { get; init; } = [];

    /// <summary>
    ///     Brick and paint palette entries extracted from a blueprint file.
    /// </summary>
    public IReadOnlyList<BlueprintPaletteItem> BlueprintPalette { get; init; } = [];

    /// <summary>
    ///     Scalar field values keyed by canonical field identifier.
    /// </summary>
    public Dictionary<string, string> ScalarValues { get; init; } = [];

    /// <summary>
    ///     Initial inbound references pointing to this asset.
    /// </summary>
    public List<InboundReferenceItem> InboundReferences { get; init; } = [];

    /// <summary>
    ///     Editable file-local values for tag documents.
    /// </summary>
    public List<string> TagFileValues { get; init; } = [];

    /// <summary>
    ///     Reference-root base values merged before pack tag values.
    /// </summary>
    public List<string> TagBaseValues { get; init; } = [];

    /// <summary>
    ///     Key-value rows for localization table documents.
    /// </summary>
    public List<(string Key, string English, string Pseudo)> LocalizationEntries { get; init; } = [];

    /// <summary>
    ///     Collider rows for brick documents.
    /// </summary>
    public List<(string Type, string Size, string Offset, string Rotation)> Colliders { get; init; } = [];

    /// <summary>
    ///     XP source rows for skill documents.
    /// </summary>
    public List<(string Trigger, string Target, int Xp)> SkillSources { get; init; } = [];

    /// <summary>
    ///     Level requirement rows for skill documents.
    /// </summary>
    public List<(int Level, int XpRequired)> SkillLevels { get; init; } = [];

    /// <summary>
    ///     State and direction sprite mappings for body documents.
    /// </summary>
    public List<(string State, string Direction, string TexturePath, string SwatchHex)> BodySprites { get; init; } = [];

    /// <summary>
    ///     Input ingredient rows for recipe documents.
    /// </summary>
    public List<(string ItemId, int Count, string SlotGroup)> RecipeInputs { get; init; } = [];

    /// <summary>
    ///     Output item rows for recipe documents.
    /// </summary>
    public List<(string ItemId, int Count, string Chance)> RecipeOutputs { get; init; } = [];
}
