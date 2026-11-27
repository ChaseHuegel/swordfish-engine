namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Captures editable document state submitted to the backend for draft sync or atomic disk save.
/// </summary>
public sealed class DocumentSavePayload
{
    /// <summary>
    ///     Asset identifier or relative file key of the modified document.
    /// </summary>
    public required string AssetId { get; init; }

    /// <summary>
    ///     Asset authoring kind of the modified document.
    /// </summary>
    public required AssetKind Kind { get; init; }

    /// <summary>
    ///     Relative path of the file inside its owning workspace root.
    /// </summary>
    public required string RelativeFile { get; init; }

    /// <summary>
    ///     Full filesystem path of the backing document on disk, when file-backed.
    /// </summary>
    public string? AbsoluteFilePath { get; init; }

    /// <summary>
    ///     Zero-based TOML table index inside the file, or -1 for root-table files.
    /// </summary>
    public int TableIndex { get; init; } = -1;

    /// <summary>
    ///     Zero-based entry index inside multi-entry array-of-tables files.
    /// </summary>
    public int EntryIndex { get; init; }

    /// <summary>
    ///     Expected SHA-256 hex digest of the file on disk before saving.
    /// </summary>
    public string? BaseContentSha256 { get; init; }

    /// <summary>
    ///     Whether the file currently has TOML syntax errors.
    /// </summary>
    public bool HasSyntaxError { get; init; }

    /// <summary>
    ///     Whether the user edited the raw TOML buffer directly rather than structured form fields.
    /// </summary>
    public bool IsDirectRawEdit { get; init; }

    /// <summary>
    ///     Current raw TOML text displayed in the code editor.
    /// </summary>
    public required string RawTomlText { get; init; }

    /// <summary>
    ///     Current friendly scalar field values keyed by canonical field identifier.
    /// </summary>
    public IReadOnlyDictionary<string, string> FieldValues { get; init; } = new Dictionary<string, string>();

    /// <summary>
    ///     Current raw TOML token expressions keyed by canonical field identifier.
    /// </summary>
    public IReadOnlyDictionary<string, string> FieldRawTokens { get; init; } = new Dictionary<string, string>();

    /// <summary>
    ///     Flags indicating which fields were explicitly edited in raw-token mode.
    /// </summary>
    public IReadOnlyDictionary<string, bool> FieldRawModeFlags { get; init; } = new Dictionary<string, bool>();

    /// <summary>
    ///     Set of canonical field keys explicitly mutated by the user since load or last save.
    /// </summary>
    public IReadOnlySet<string>? ModifiedFieldKeys { get; init; }

    /// <summary>
    ///     Current editable tag list values for tag documents.
    /// </summary>
    public IReadOnlyList<string> TagFileValues { get; init; } = [];

    /// <summary>
    ///     Current localization key-value pairs for localization table documents.
    /// </summary>
    public IReadOnlyList<(string Key, string Value)> LocalizationRows { get; init; } = [];

    /// <summary>
    ///     Optional localization row edits carrying original keys so renames update or remove old entries cleanly.
    /// </summary>
    public IReadOnlyList<(string OriginalKey, string Key, string Value)>? LocalizationKeyEdits { get; init; }

    /// <summary>
    ///     Optional localization keys explicitly removed since load or last save.
    /// </summary>
    public IReadOnlyList<string>? RemovedLocalizationKeys { get; init; }

    /// <summary>
    ///     Active locale code for localization table documents.
    /// </summary>
    public string SelectedLocale { get; init; } = "en";

    /// <summary>
    ///     Editable display name for blueprint binary records.
    /// </summary>
    public string? BlueprintDisplayName { get; init; }

    /// <summary>
    ///     Current state and direction sprite mappings for body documents.
    /// </summary>
    public IReadOnlyList<(string State, string Direction, string TexturePath)>? BodySprites { get; init; }

    /// <summary>
    ///     Whether body state and sprite mappings were modified since load or last save.
    /// </summary>
    public bool BodySpritesModified { get; init; }

    /// <summary>
    ///     Current skill level progression rows for skill documents.
    /// </summary>
    public IReadOnlyList<(int Level, int XpRequired)>? SkillLevels { get; init; }

    /// <summary>
    ///     Whether skill level progression rows were modified since load or last save.
    /// </summary>
    public bool SkillLevelsModified { get; init; }

    /// <summary>
    ///     Current input ingredient rows for recipe documents.
    /// </summary>
    public IReadOnlyList<(string ItemId, int Count, string SlotGroup)>? RecipeInputs { get; init; }

    /// <summary>
    ///     Whether recipe input rows were modified since load or last save.
    /// </summary>
    public bool RecipeInputsModified { get; init; }

    /// <summary>
    ///     Current output item rows for recipe documents.
    /// </summary>
    public IReadOnlyList<(string ItemId, int Count, string Chance)>? RecipeOutputs { get; init; }

    /// <summary>
    ///     Whether recipe output rows were modified since load or last save.
    /// </summary>
    public bool RecipeOutputsModified { get; init; }
}
