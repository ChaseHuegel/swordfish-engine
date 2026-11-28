using System.Globalization;
using System.Text;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Editing;
using WaywardBeyond.AssetTools.Workspace;
using WaywardBeyond.Bodies;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Bars;
using WaywardBeyond.Data.Buffs;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Items;
using WaywardBeyond.Recipes;
using WaywardBeyond.Skills;

namespace WaywardBeyond.AssetTools.Validation;

/// <summary>
///     Validates a multi-root <see cref="WorkspaceIndex"/> against consumer-aware content and cross-platform rules.
/// </summary>
public sealed class WorkspaceValidator
{
    private static readonly string[] NON_ALBEDO_LAYER_SUFFIXES = [".m", ".s", ".n", ".e", ".t"];
    private static readonly string[] AUDIO_EXTENSIONS = [".wav", ".mp3", ".flac", ".ogg"];
    private static readonly string[] TEXTURE_EXTENSIONS = [".png", ".bmp"];
    private static readonly string[] SHADER_EXTENSIONS = [".glsl", ".vert", ".frag"];

    /// <summary>
    ///     Runs all content, strict-enum, reference-grammar, localization, tag, and platform checks on <paramref name="workspace"/>.
    /// </summary>
    public ValidationReport Validate(WorkspaceIndex workspace)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        List<ValidationFinding> findings = [];
        ValidateDocuments(workspace, findings);
        ValidateAssetIdentityAndDuplicates(workspace, findings);
        ValidateAssets(workspace, findings);
        ValidateLocalization(workspace, findings);
        ValidateTags(workspace, findings);

        return new ValidationReport(findings);
    }

    /// <summary>
    ///     Creates a style warning finding when a tag list order is modified.
    /// </summary>
    public static ValidationFinding CreateTagReorderWarning(string rootId, string filePath, int line, string tagName)
    {
        return new ValidationFinding(
            "STYLE001",
            ValidationSeverity.Style,
            rootId,
            filePath,
            line,
            Column: 1,
            tagName,
            $"Reordering tag '{tagName}' changes semantic value order.");
    }

    /// <summary>
    ///     Creates a style warning finding when a brick ID is renamed due to legacy save hash mapping.
    /// </summary>
    public static ValidationFinding CreateBrickRenameWarning(string rootId, string filePath, int line, string oldId, string newId)
    {
        return new ValidationFinding(
            "STYLE002",
            ValidationSeverity.Style,
            rootId,
            filePath,
            line,
            Column: 1,
            "ID",
            $"Renaming brick '{oldId}' to '{newId}' breaks legacy save hash reverse-mapping.");
    }

    /// <summary>
    ///     Suggests an unambiguous bare-name or close Levenshtein match for an unresolved asset identifier.
    /// </summary>
    internal static string? SuggestAssetReference(WorkspaceIndex workspace, AssetKind kind, string rawValue)
    {
        return FindBestReferenceMatch(rawValue, EnumerateAssetIds(workspace, kind));
    }

    /// <summary>
    ///     Suggests an unambiguous bare-name or close Levenshtein match for an unresolved resource identifier.
    /// </summary>
    internal static string? SuggestResourceReference(WorkspaceIndex workspace, ResourceKind kind, string rawValue)
    {
        return FindBestReferenceMatch(rawValue, EnumerateResourceIds(workspace, kind));
    }

    /// <summary>
    ///     Suggests an unambiguous bare-name or close Levenshtein match for an unresolved block albedo layer.
    /// </summary>
    internal static string? SuggestBlockLayerReference(WorkspaceIndex workspace, string rawValue)
    {
        return FindBestReferenceMatch(rawValue, workspace.WinningBlockAlbedoLayers.Keys);
    }

    /// <summary>
    ///     Suggests an unambiguous bare-name or close Levenshtein match for an unresolved invariant tag.
    /// </summary>
    internal static string? SuggestInvariantTagReference(WorkspaceIndex workspace, string rawValue, bool soundTagsOnly = false)
    {
        return FindBestReferenceMatch(rawValue, EnumerateInvariantTagNames(workspace, soundTagsOnly));
    }

    private static void ValidateDocuments(WorkspaceIndex workspace, List<ValidationFinding> findings)
    {
        foreach (KeyValuePair<string, TomlDocument> pair in workspace.DocumentsByPath)
        {
            string filePath = pair.Key;
            TomlDocument doc = pair.Value;
            string rootId = ResolveRootIdForPath(workspace, filePath);

            if (doc.HasSyntaxErrors)
            {
                AddSyntaxDiagnostics(doc, rootId, filePath, findings);
            }

            if (doc.HasUtf8Bom)
            {
                findings.Add(new ValidationFinding(
                    "FMT001",
                    ValidationSeverity.CrossPlatform,
                    rootId,
                    filePath,
                    Line: 1,
                    Column: 1,
                    string.Empty,
                    "File contains a UTF-8 BOM; remove BOM for cross-platform consistency.",
                    FixKind: ValidationFixKind.StripUtf8Bom,
                    IsSafeBulkFix: true,
                    FixDescription: "Remove UTF-8 BOM"));
            }

            if (doc.Text.Contains("\r\n", StringComparison.Ordinal))
            {
                findings.Add(new ValidationFinding(
                    "FMT002",
                    ValidationSeverity.CrossPlatform,
                    rootId,
                    filePath,
                    Line: 1,
                    Column: 1,
                    string.Empty,
                    "File uses CRLF line endings; normalize to LF for cross-platform consistency.",
                    FixKind: ValidationFixKind.NormalizeLineEndings,
                    IsSafeBulkFix: true,
                    FixDescription: "Convert CRLF to LF"));
            }
        }
    }

    private static void ValidateAssetIdentityAndDuplicates(WorkspaceIndex workspace, List<ValidationFinding> findings)
    {
        HashSet<(string RootId, AssetKind Kind, string Id)> seenInRoot = new();
        HashSet<string> seenSkillIds = new(StringComparer.Ordinal);
        Dictionary<AssetKind, HashSet<string>> allocatedDuplicateIds = new();

        for (int i = 0; i < workspace.AllAssets.Count; i++)
        {
            IndexedAsset asset = workspace.AllAssets[i];
            TomlDocument? doc = TryGetDocument(workspace, asset.Provenance.FilePath);
            TomlFieldSpan? idSpan = FindEntryField(doc, asset.Provenance.TableIndex, "ID");
            int line = idSpan.HasValue ? idSpan.Value.KeySpan.Line : ResolveEntryLine(doc, asset.Provenance.TableIndex);
            int column = idSpan.HasValue ? idSpan.Value.KeySpan.Column : 1;

            if (asset.AuthoritativeParseFailed)
            {
                findings.Add(new ValidationFinding(
                    "TOML002",
                    ValidationSeverity.Fatal,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    column,
                    string.Empty,
                    asset.AuthoritativeParseError ?? "Failed to deserialize definition."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(asset.Id))
            {
                findings.Add(new ValidationFinding(
                    "ID001",
                    ValidationSeverity.Fatal,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    column,
                    "ID",
                    $"{asset.Kind} definition is missing a non-empty ID.",
                    FieldSpan: idSpan?.ValueSpan));
                continue;
            }

            bool isDuplicateInRoot = !seenInRoot.Add((asset.Provenance.ProviderRootId, asset.Kind, asset.Id));
            bool isDuplicateSkill = asset.Kind == AssetKind.Skill && !seenSkillIds.Add(asset.Id);
            if (!isDuplicateInRoot && !isDuplicateSkill)
            {
                continue;
            }

            string? suggestedNextId = AllocateNextDuplicateId(workspace, asset.Kind, asset.Id, allocatedDuplicateIds);
            ValidationFixKind fixKind = suggestedNextId != null && idSpan.HasValue
                ? ValidationFixKind.ReplaceValue
                : ValidationFixKind.None;
            string? fixDesc = fixKind == ValidationFixKind.ReplaceValue ? "Assign next unique ID" : null;

            if (isDuplicateInRoot)
            {
                findings.Add(new ValidationFinding(
                    "ID002",
                    ValidationSeverity.Fatal,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    column,
                    "ID",
                    $"Duplicate {asset.Kind} ID '{asset.Id}' within root '{asset.Provenance.ProviderRootId}'.",
                    SuggestedFixValue: suggestedNextId,
                    FieldSpan: idSpan?.ValueSpan,
                    FixKind: fixKind,
                    IsSafeBulkFix: false,
                    FixDescription: fixDesc));
            }

            if (isDuplicateSkill)
            {
                findings.Add(new ValidationFinding(
                    "ID003",
                    ValidationSeverity.Broken,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    column,
                    "ID",
                    $"Duplicate skill ID '{asset.Id}'; SkillDatabase rejects duplicate skill IDs.",
                    SuggestedFixValue: suggestedNextId,
                    FieldSpan: idSpan?.ValueSpan,
                    FixKind: fixKind,
                    IsSafeBulkFix: false,
                    FixDescription: fixDesc));
            }
        }
    }

    private static string? AllocateNextDuplicateId(
        WorkspaceIndex workspace,
        AssetKind kind,
        string duplicateId,
        Dictionary<AssetKind, HashSet<string>> allocatedByKind)
    {
        if (!allocatedByKind.TryGetValue(kind, out HashSet<string>? taken))
        {
            taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            allocatedByKind[kind] = taken;
        }

        Result<string> allocated = AssetIdAllocator.AllocateNextId(workspace, kind, duplicateId, taken);
        if (!allocated.Success)
        {
            return null;
        }

        taken.Add(allocated.Value);
        return allocated.Value;
    }

    private static void ValidateAssets(WorkspaceIndex workspace, List<ValidationFinding> findings)
    {
        for (int i = 0; i < workspace.AllAssets.Count; i++)
        {
            IndexedAsset asset = workspace.AllAssets[i];
            if (asset.AuthoritativeParseFailed || asset.AuthoritativeDefinition == null)
            {
                continue;
            }

            TomlDocument? doc = TryGetDocument(workspace, asset.Provenance.FilePath);
            ValidateStrictEnums(in asset, doc, findings);

            switch (asset.AuthoritativeDefinition)
            {
                case ItemDefinition item:
                    ValidateItem(workspace, in asset, in item, doc, findings);
                    break;

                case BrickDefinition brick:
                    ValidateBrick(workspace, in asset, in brick, doc, findings);
                    break;

                case PaintDefinition paint:
                    ValidatePaint(workspace, in asset, in paint, doc, findings);
                    break;

                case SkillDefinition skill:
                    ValidateSkill(workspace, in asset, in skill, doc, findings);
                    break;

                case BodyDefinition body:
                    ValidateBody(workspace, in asset, in body, doc, findings);
                    break;

                case RecipeDefinition recipe:
                    ValidateRecipe(workspace, in asset, in recipe, doc, findings);
                    break;

                case SlotGroupDefinition slotGroup:
                    ValidateSlotGroup(workspace, in asset, in slotGroup, doc, findings);
                    break;

                case BarDefinition bar:
                    ValidateBar(workspace, in asset, in bar, doc, findings);
                    break;

                case BuffDefinition buff:
                    ValidateBuff(workspace, in asset, in buff, doc, findings);
                    break;

                case MaterialDefinition material:
                    ValidateMaterial(workspace, in asset, in material, doc, findings);
                    break;
            }
        }
    }

    private static void ValidateStrictEnums(in IndexedAsset asset, TomlDocument? doc, List<ValidationFinding> findings)
    {
        int beforeCount = findings.Count;
        if (doc != null)
        {
            switch (asset.Kind)
            {
                case AssetKind.Brick:
                    CheckEnumField<BrickShape>(in asset, doc, null, "Shape", findings);
                    CheckEnumField<BrickAlpha>(in asset, doc, null, "Alpha", findings);
                    CheckSubTableKeysEnum<BrickEventKind>(in asset, doc, "Bricks.Sounds", "Sounds", findings);
                    break;

                case AssetKind.Paint:
                    CheckEnumField<BrickAlpha>(in asset, doc, null, "Alpha", findings);
                    break;

                case AssetKind.Item:
                    CheckEnumField<PlaceableType>(in asset, doc, "Items.Placeable", "Type", findings);
                    CheckEnumField<ToolType>(in asset, doc, "Items.Tool", "Type", findings);
                    break;

                case AssetKind.Body:
                    CheckEnumField<Gender>(in asset, doc, null, "Gender", findings);
                    break;

                case AssetKind.Material:
                    CheckEnumField<AlphaMode>(in asset, doc, null, "AlphaMode", findings);
                    break;

                case AssetKind.Skill:
                    CheckSkillSourceEnums(in asset, doc, findings);
                    break;

                case AssetKind.Recipe:
                    CheckEnumField<RecipeMode>(in asset, doc, null, "Mode", findings);
                    break;
            }
        }

        if (findings.Count == beforeCount && !string.IsNullOrEmpty(asset.StrictParseError))
        {
            int line = ResolveEntryLine(doc, asset.Provenance.TableIndex);
            findings.Add(new ValidationFinding(
                "ENUM001",
                ValidationSeverity.Broken,
                asset.Provenance.ProviderRootId,
                asset.Provenance.FilePath,
                line,
                Column: 1,
                string.Empty,
                $"Strict parse rejected value ignored by authoritative mode: {asset.StrictParseError}"));
        }
    }

    private static void CheckEnumField<TEnum>(
        in IndexedAsset asset,
        TomlDocument doc,
        string? subTablePath,
        string fieldName,
        List<ValidationFinding> findings)
        where TEnum : struct, Enum
    {
        TomlFieldSpan? field = FindEntryField(doc, asset.Provenance.TableIndex, fieldName, subTablePath);
        if (!field.HasValue)
        {
            return;
        }

        string displayField = string.IsNullOrEmpty(subTablePath) ? fieldName : $"{subTablePath}.{fieldName}";
        string? rawEnum = field.Value.TypedValue as string;
        if (rawEnum == null || !IsValidEnumName<TEnum>(rawEnum))
        {
            string validValues = string.Join(", ", Enum.GetNames<TEnum>());
            (string? suggested, bool isSafe, string? fixDesc) = TrySuggestEnumFix<TEnum>(rawEnum);
            ValidationFixKind fixKind = suggested != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None;
            findings.Add(new ValidationFinding(
                "ENUM001",
                ValidationSeverity.Broken,
                asset.Provenance.ProviderRootId,
                asset.Provenance.FilePath,
                field.Value.ValueSpan.Line,
                field.Value.ValueSpan.Column,
                displayField,
                $"Invalid {typeof(TEnum).Name} value {field.Value.RawValue}. Expected one of: {validValues}.",
                SuggestedFixValue: suggested,
                FieldSpan: field.Value.ValueSpan,
                FixKind: fixKind,
                IsSafeBulkFix: isSafe,
                FixDescription: fixDesc));
        }
    }

    private static void CheckSubTableKeysEnum<TEnum>(
        in IndexedAsset asset,
        TomlDocument doc,
        string subTablePath,
        string fieldPrefix,
        List<ValidationFinding> findings)
        where TEnum : struct, Enum
    {
        TomlTableSpan? subTable = FindSubTable(doc, asset.Provenance.TableIndex, subTablePath);
        if (!subTable.HasValue)
        {
            return;
        }

        for (int i = 0; i < subTable.Value.Fields.Count; i++)
        {
            TomlFieldSpan field = subTable.Value.Fields[i];
            if (!IsValidEnumName<TEnum>(field.Key))
            {
                string validValues = string.Join(", ", Enum.GetNames<TEnum>());
                (string? suggested, bool isSafe, string? fixDesc) = TrySuggestEnumFix<TEnum>(field.Key);
                ValidationFixKind fixKind = suggested != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None;
                findings.Add(new ValidationFinding(
                    "ENUM001",
                    ValidationSeverity.Broken,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    field.KeySpan.Line,
                    field.KeySpan.Column,
                    $"{fieldPrefix}.{field.Key}",
                    $"Invalid {typeof(TEnum).Name} key '{field.Key}'. Expected one of: {validValues}.",
                    SuggestedFixValue: suggested,
                    FieldSpan: field.KeySpan,
                    FixKind: fixKind,
                    IsSafeBulkFix: isSafe,
                    FixDescription: fixDesc));
            }
        }
    }

    private static void CheckSkillSourceEnums(in IndexedAsset asset, TomlDocument doc, List<ValidationFinding> findings)
    {
        IReadOnlyList<TomlTableSpan> subTables = FindSubTablesWithPrefix(doc, asset.Provenance.TableIndex, "Skills.Sources.");
        for (int i = 0; i < subTables.Count; i++)
        {
            TomlTableSpan sub = subTables[i];
            string sourceSegment = sub.HeaderSegments.Count >= 3 ? sub.HeaderSegments[2] : string.Empty;
            if (!IsValidEnumName<XPSource>(sourceSegment))
            {
                string validValues = string.Join(", ", Enum.GetNames<XPSource>());
                (string? suggested, bool isSafe, string? fixDesc) = TrySuggestEnumFix<XPSource>(sourceSegment);
                ValidationFixKind fixKind = suggested != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None;
                findings.Add(new ValidationFinding(
                    "ENUM001",
                    ValidationSeverity.Broken,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    sub.HeaderSpan.Line,
                    sub.HeaderSpan.Column,
                    sub.HeaderPath,
                    $"Invalid XPSource '{sourceSegment}' in table header '[{sub.HeaderPath}]'. Expected one of: {validValues}.",
                    SuggestedFixValue: suggested,
                    FieldSpan: sub.HeaderSpan,
                    FixKind: fixKind,
                    IsSafeBulkFix: isSafe,
                    FixDescription: fixDesc));
            }
        }
    }

    private static bool IsValidEnumName<TEnum>(string rawEnum)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(rawEnum))
        {
            return false;
        }

        char first = rawEnum[0];
        if (char.IsDigit(first) || first == '-' || first == '+' || rawEnum.Contains(','))
        {
            return false;
        }

        return Enum.TryParse<TEnum>(rawEnum, ignoreCase: true, out TEnum parsed) &&
               Enum.IsDefined(parsed);
    }

    private static (string? Suggested, bool IsSafeBulkFix, string? FixDescription) TrySuggestEnumFix<TEnum>(string? rawEnum)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(rawEnum))
        {
            return (null, false, null);
        }

        string trimmed = rawEnum.Trim();
        string[] names = Enum.GetNames<TEnum>();

        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(trimmed, names[i], StringComparison.OrdinalIgnoreCase))
            {
                return (names[i], true, "Fix enum casing");
            }
        }

        string normalizedInput = StripEnumSeparators(trimmed);
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(normalizedInput, names[i], StringComparison.OrdinalIgnoreCase))
            {
                return (names[i], true, "Fix enum formatting");
            }
        }

        string? closest = FindUniqueClosestLevenshtein(trimmed, names, maxDistance: 2);
        return closest != null
            ? (closest, false, "Replace with closest valid enum")
            : (null, false, null);
    }

    private static string StripEnumSeparators(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c != '_' && c != '-' && c != ' ')
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static void ValidateItem(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        in ItemDefinition item,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        if (!string.IsNullOrWhiteSpace(item.Name))
        {
            ValidateLocalizationReference(workspace, in asset, doc, null, "Name", item.Name, ValidationSeverity.Broken, findings);
        }

        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            ValidateLocalizationReference(workspace, in asset, doc, null, "Description", item.Description, ValidationSeverity.Broken, findings);
        }

        if (!string.IsNullOrWhiteSpace(item.Icon))
        {
            ValidateTextureReference(workspace, in asset, doc, null, "Icon", item.Icon, ValidationSeverity.Broken, findings);
        }

        if (item.Placeable.HasValue &&
            item.Placeable.Value.Type == PlaceableType.Brick &&
            !string.IsNullOrWhiteSpace(item.Placeable.Value.ID))
        {
            string brickId = item.Placeable.Value.ID;
            if (!workspace.GetAsset(AssetKind.Brick, brickId).Success)
            {
                TomlFieldSpan? span = FindEntryField(doc, asset.Provenance.TableIndex, "ID", "Items.Placeable");
                string? caseMatch = FindCaseInsensitiveAssetId(workspace, AssetKind.Brick, brickId);
                if (caseMatch != null)
                {
                    AddFieldFinding(
                        findings,
                        "PATH001",
                        ValidationSeverity.CrossPlatform,
                        in asset,
                        span,
                        "Placeable.ID",
                        $"Placeable brick ID '{brickId}' differs by case from '{caseMatch}'.",
                        suggestedFixValue: caseMatch,
                        fixKind: ValidationFixKind.ReplaceValue,
                        isSafeBulkFix: true,
                        fixDescription: "Fix path/ID casing");
                }
                else
                {
                    string? suggested = SuggestAssetReference(workspace, AssetKind.Brick, brickId);
                    AddFieldFinding(
                        findings,
                        "REF001",
                        ValidationSeverity.Broken,
                        in asset,
                        span,
                        "Placeable.ID",
                        $"Placeable brick '{brickId}' was not found.",
                        suggestedFixValue: suggested,
                        fixKind: suggested != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                        isSafeBulkFix: false,
                        fixDescription: suggested != null ? "Replace with closest matching brick" : null);
                }
            }
        }
        else if (item.Placeable.HasValue &&
            item.Placeable.Value.Type == PlaceableType.Blueprint &&
            !string.IsNullOrWhiteSpace(item.Placeable.Value.ID))
        {
            string blueprintId = item.Placeable.Value.ID;
            if (!workspace.GetBlueprintFile(blueprintId).Success)
            {
                TomlFieldSpan? span = FindEntryField(doc, asset.Provenance.TableIndex, "ID", "Items.Placeable");
                Result<string> caseMatch = workspace.FindBlueprintIdCaseInsensitive(blueprintId);
                if (caseMatch.Success)
                {
                    AddFieldFinding(
                        findings,
                        "PATH001",
                        ValidationSeverity.CrossPlatform,
                        in asset,
                        span,
                        "Placeable.ID",
                        $"Placeable blueprint ID '{blueprintId}' differs by case from '{caseMatch.Value}'.",
                        suggestedFixValue: caseMatch.Value,
                        fixKind: ValidationFixKind.ReplaceValue,
                        isSafeBulkFix: true,
                        fixDescription: "Fix path/ID casing");
                }
                else
                {
                    string? suggested = FindBestReferenceMatch(blueprintId, workspace.WinningBlueprintFiles.Keys);
                    AddFieldFinding(
                        findings,
                        "REF001",
                        ValidationSeverity.Broken,
                        in asset,
                        span,
                        "Placeable.ID",
                        $"Placeable blueprint '{blueprintId}' was not found.",
                        suggestedFixValue: suggested,
                        fixKind: suggested != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                        isSafeBulkFix: false,
                        fixDescription: suggested != null ? "Replace with closest matching blueprint" : null);
                }
            }
        }

        if (item.Consumable.HasValue)
        {
            ValidateItemConsumable(workspace, in asset, item.Consumable.Value, doc, findings);
        }

        if (item.Equip.HasValue)
        {
            ValidateItemEquip(in asset, item.Equip.Value, doc, findings);
        }

        if (item.ViewModel.HasValue)
        {
            ValidateModelDefinition(workspace, in asset, doc, "Items.ViewModel", "ViewModel", item.ViewModel.Value, ValidationSeverity.Broken, findings);
        }

        if (item.WorldModel.HasValue)
        {
            ValidateModelDefinition(workspace, in asset, doc, "Items.WorldModel", "WorldModel", item.WorldModel.Value, ValidationSeverity.Informational, findings);
        }

        ValidateStatistics(in asset, doc, "Items.Statistics", "item", item.Statistics, findings);
    }

    private static void ValidateItemConsumable(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        in ConsumableDefinition consumable,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        if (!string.IsNullOrWhiteSpace(consumable.BuffID))
        {
            string buffId = consumable.BuffID.Trim();
            if (!workspace.GetAsset(AssetKind.Buff, buffId).Success)
            {
                TomlFieldSpan? span = FindEntryField(doc, asset.Provenance.TableIndex, "BuffID", "Items.Consumable");
                string? caseMatch = FindCaseInsensitiveAssetId(workspace, AssetKind.Buff, buffId);
                if (caseMatch != null)
                {
                    AddFieldFinding(
                        findings,
                        "PATH001",
                        ValidationSeverity.CrossPlatform,
                        in asset,
                        span,
                        "Consumable.BuffID",
                        $"Consumable buff ID '{buffId}' differs by case from '{caseMatch}'.",
                        suggestedFixValue: caseMatch,
                        fixKind: ValidationFixKind.ReplaceValue,
                        isSafeBulkFix: true,
                        fixDescription: "Fix path/ID casing");
                }
                else
                {
                    string? suggested = SuggestAssetReference(workspace, AssetKind.Buff, buffId);
                    AddFieldFinding(
                        findings,
                        "REF001",
                        ValidationSeverity.Broken,
                        in asset,
                        span,
                        "Consumable.BuffID",
                        $"Consumable buff '{buffId}' was not found.",
                        suggestedFixValue: suggested,
                        fixKind: suggested != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                        isSafeBulkFix: false,
                        fixDescription: suggested != null ? "Replace with closest matching buff" : null);
                }
            }
        }

        if (!float.IsFinite(consumable.CooldownSeconds) || consumable.CooldownSeconds < 0f)
        {
            TomlFieldSpan? cooldownField = FindEntryField(doc, asset.Provenance.TableIndex, "CooldownSeconds", "Items.Consumable");
            AddFieldFinding(
                findings,
                "CONSUMABLE001",
                ValidationSeverity.Broken,
                in asset,
                cooldownField,
                "Consumable.CooldownSeconds",
                $"Item '{asset.Id}' Consumable.CooldownSeconds ({consumable.CooldownSeconds}) must be finite and >= 0.");
        }

        if (consumable.Modifiers != null)
        {
            List<TomlTableSpan> modTables = FindOwnedSubTables(doc, asset.Provenance.TableIndex, "Items", "Items.Consumable.Modifiers");
            for (int i = 0; i < consumable.Modifiers.Count; i++)
            {
                ConsumableStatModifierDefinition mod = consumable.Modifiers[i];
                ValidateItemStatModifier(
                    in asset,
                    doc,
                    modTables,
                    i,
                    code: "CONSUMABLE001",
                    fieldPrefix: $"Consumable.Modifiers[{i}]",
                    label: $"Item '{asset.Id}' Consumable.Modifiers[{i}]",
                    mod.StatId,
                    mod.FlatDelta,
                    mod.PercentDelta,
                    findings);
            }
        }
    }

    private static void ValidateItemEquip(
        in IndexedAsset asset,
        in EquipDefinition equip,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        if (!ItemTags.TryResolveEquipTag(equip.SlotTag, out _))
        {
            TomlFieldSpan? slotTagField = FindEntryField(doc, asset.Provenance.TableIndex, "SlotTag", "Items.Equip");
            AddFieldFinding(
                findings,
                "EQUIP001",
                ValidationSeverity.Broken,
                in asset,
                slotTagField,
                "Equip.SlotTag",
                $"Item '{asset.Id}' Equip.SlotTag '{equip.SlotTag}' is invalid.");
        }

        if (equip.StatModifiers == null)
        {
            return;
        }

        List<TomlTableSpan> modTables = FindOwnedSubTables(doc, asset.Provenance.TableIndex, "Items", "Items.Equip.StatModifiers");
        for (int i = 0; i < equip.StatModifiers.Count; i++)
        {
            EquipStatModifierDefinition mod = equip.StatModifiers[i];
            ValidateItemStatModifier(
                in asset,
                doc,
                modTables,
                i,
                code: "EQUIP001",
                fieldPrefix: $"Equip.StatModifiers[{i}]",
                label: $"Item '{asset.Id}' Equip.StatModifiers[{i}]",
                mod.StatId,
                mod.FlatDelta,
                mod.PercentDelta,
                findings);
        }
    }

    private static void ValidateItemStatModifier(
        in IndexedAsset asset,
        TomlDocument? doc,
        List<TomlTableSpan> modTables,
        int index,
        string code,
        string fieldPrefix,
        string label,
        string? statId,
        float? flatDelta,
        float? percentDelta,
        List<ValidationFinding> findings)
    {
        TomlTableSpan? modTable = index < modTables.Count ? modTables[index] : null;
        TomlFieldSpan? statField = ResolveSubTableField(doc, modTable, "StatId");
        TomlFieldSpan? flatField = ResolveSubTableField(doc, modTable, "FlatDelta");
        TomlFieldSpan? pctField = ResolveSubTableField(doc, modTable, "PercentDelta");

        if (string.IsNullOrWhiteSpace(statId))
        {
            AddFieldFinding(
                findings,
                code,
                ValidationSeverity.Broken,
                in asset,
                statField,
                $"{fieldPrefix}.StatId",
                $"{label} is missing StatId.");
        }

        if (flatDelta.HasValue && !float.IsFinite(flatDelta.Value))
        {
            AddFieldFinding(
                findings,
                code,
                ValidationSeverity.Broken,
                in asset,
                flatField,
                $"{fieldPrefix}.FlatDelta",
                $"{label} FlatDelta must be finite.");
        }

        if (percentDelta.HasValue && !float.IsFinite(percentDelta.Value))
        {
            AddFieldFinding(
                findings,
                code,
                ValidationSeverity.Broken,
                in asset,
                pctField,
                $"{fieldPrefix}.PercentDelta",
                $"{label} PercentDelta must be finite.");
        }

        if (!flatDelta.HasValue && !percentDelta.HasValue)
        {
            AddFieldFinding(
                findings,
                code,
                ValidationSeverity.Broken,
                in asset,
                flatField ?? statField,
                fieldPrefix,
                $"{label} must specify FlatDelta or PercentDelta.");
        }
        else if ((flatDelta ?? 0f) == 0f && (percentDelta ?? 0f) == 0f)
        {
            AddFieldFinding(
                findings,
                code,
                ValidationSeverity.Style,
                in asset,
                flatField ?? pctField ?? statField,
                fieldPrefix,
                $"{label} has zero FlatDelta and PercentDelta (no-op).");
        }
    }

    private static List<TomlTableSpan> FindOwnedSubTables(TomlDocument? doc, int ownerTableIndex, string ownerHeaderPath, string targetHeaderPath)
    {
        List<TomlTableSpan> matches = [];
        if (doc == null || ownerTableIndex < 0 || ownerTableIndex >= doc.Tables.Count)
        {
            return matches;
        }

        string ownerPrefix = ownerHeaderPath + ".";
        for (int i = ownerTableIndex + 1; i < doc.Tables.Count; i++)
        {
            TomlTableSpan next = doc.Tables[i];
            if (next.IsArrayTable && string.Equals(next.HeaderPath, ownerHeaderPath, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.Equals(next.HeaderPath, targetHeaderPath, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(next);
                continue;
            }

            if (!next.HeaderPath.StartsWith(ownerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return matches;
    }

    private static void ValidateStatistics(
        in IndexedAsset asset,
        TomlDocument? doc,
        string subTablePath,
        string ownerKind,
        Dictionary<string, float>? statistics,
        List<ValidationFinding> findings)
    {
        if (statistics == null)
        {
            return;
        }

        foreach (KeyValuePair<string, float> entry in statistics)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                AddFieldFinding(
                    findings,
                    "STAT001",
                    ValidationSeverity.Broken,
                    in asset,
                    field: null,
                    "Statistics",
                    $"Invalid {ownerKind} statistic: key must not be empty.");
                continue;
            }

            bool invalidMiningSpeed = string.Equals(entry.Key, StatisticIds.MINING_SPEED, StringComparison.Ordinal)
                && entry.Value <= 0f;
            bool invalidHardness = string.Equals(entry.Key, StatisticIds.HARDNESS, StringComparison.Ordinal)
                && entry.Value < 0f;
            if (!float.IsFinite(entry.Value) || invalidMiningSpeed || invalidHardness)
            {
                TomlFieldSpan? statField = FindEntryField(doc, asset.Provenance.TableIndex, entry.Key, subTablePath);
                string expected = invalidMiningSpeed
                    ? "Expected a finite positive number."
                    : (invalidHardness ? "Expected a finite non-negative number." : "Expected a finite number.");
                AddFieldFinding(
                    findings,
                    "STAT001",
                    ValidationSeverity.Broken,
                    in asset,
                    statField,
                    $"Statistics.{entry.Key}",
                    $"Invalid {ownerKind} statistic '{entry.Key}' value '{entry.Value}'. {expected}");
            }
        }
    }

    private static void ValidateModelDefinition(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlDocument? doc,
        string subTablePath,
        string fieldPrefix,
        in ModelDefinition model,
        ValidationSeverity missingSeverity,
        List<ValidationFinding> findings)
    {
        if (!string.IsNullOrWhiteSpace(model.Mesh))
        {
            ValidateMeshReference(workspace, in asset, doc, subTablePath, $"{fieldPrefix}.Mesh", "Mesh", model.Mesh, missingSeverity, findings);
        }

        if (!string.IsNullOrWhiteSpace(model.Material))
        {
            ValidateMaterialReference(workspace, in asset, doc, subTablePath, $"{fieldPrefix}.Material", "Material", model.Material, missingSeverity, findings);
        }
    }

    private static void ValidateBrick(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        in BrickDefinition brick,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        if (!string.IsNullOrWhiteSpace(brick.Mesh))
        {
            ValidateMeshReference(workspace, in asset, doc, null, "Mesh", "Mesh", brick.Mesh, ValidationSeverity.Broken, findings);
        }

        ValidateStatistics(in asset, doc, "Bricks.Statistics", "brick", brick.Statistics, findings);

        if (!string.IsNullOrWhiteSpace(brick.Color) && !IsStrictSixDigitHexRgb(brick.Color))
        {
            TomlFieldSpan? colorField = FindEntryField(doc, asset.Provenance.TableIndex, "Color");
            (string? suggestedColor, bool isSafeColorFix, string? fixDesc) = TrySuggestNormalizedHexColor(brick.Color);
            AddFieldFinding(
                findings,
                "COLOR001",
                ValidationSeverity.Broken,
                in asset,
                colorField,
                "Color",
                $"Invalid 6-digit hex RGB color '{brick.Color}'.",
                suggestedFixValue: suggestedColor,
                fixKind: suggestedColor != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                isSafeBulkFix: isSafeColorFix,
                fixDescription: fixDesc);
        }

        if (brick.Colliders != null)
        {
            ValidateBrickColliders(in asset, doc, brick.Colliders, findings);
        }

        if (brick.Entity != null)
        {
            ValidateBrickEntity(workspace, in asset, doc, brick.Entity.Value, findings);
        }

        ValidateBrickFaceList(workspace, in asset, doc, "Bricks.Textures", "Default", brick.Textures.Default, brick.Textures.Connected, findings);
        ValidateBrickFaceList(workspace, in asset, doc, "Bricks.Textures", "Top", brick.Textures.Top, brick.Textures.Connected, findings);
        ValidateBrickFaceList(workspace, in asset, doc, "Bricks.Textures", "Bottom", brick.Textures.Bottom, brick.Textures.Connected, findings);
        ValidateBrickFaceList(workspace, in asset, doc, "Bricks.Textures", "Front", brick.Textures.Front, brick.Textures.Connected, findings);
        ValidateBrickFaceList(workspace, in asset, doc, "Bricks.Textures", "Back", brick.Textures.Back, brick.Textures.Connected, findings);
        ValidateBrickFaceList(workspace, in asset, doc, "Bricks.Textures", "Left", brick.Textures.Left, brick.Textures.Connected, findings);
        ValidateBrickFaceList(workspace, in asset, doc, "Bricks.Textures", "Right", brick.Textures.Right, brick.Textures.Connected, findings);

        if (brick.Sounds != null)
        {
            foreach (KeyValuePair<BrickEventKind, string[]> kvp in brick.Sounds)
            {
                ValidateBrickSoundTags(workspace, in asset, doc, kvp.Key.ToString(), kvp.Value, findings);
            }
        }
    }

    private static void ValidateBrickEntity(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlDocument? doc,
        in BrickEntityDefinition entity,
        List<ValidationFinding> findings)
    {
        if (entity.Light != null)
        {
            BrickLightDefinition light = entity.Light.Value;
            if (!string.IsNullOrWhiteSpace(light.Color) && !IsStrictSixDigitHexRgb(light.Color))
            {
                TomlFieldSpan? colorField = FindEntryField(doc, asset.Provenance.TableIndex, "Color", "Bricks.Entity.Light");
                (string? suggestedColor, bool isSafeColorFix, string? fixDesc) = TrySuggestNormalizedHexColor(light.Color);
                AddFieldFinding(
                    findings,
                    "COLOR001",
                    ValidationSeverity.Broken,
                    in asset,
                    colorField,
                    "Entity.Light.Color",
                    $"Invalid 6-digit hex RGB light color '{light.Color}'.",
                    suggestedFixValue: suggestedColor,
                    fixKind: suggestedColor != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                    isSafeBulkFix: isSafeColorFix,
                    fixDescription: fixDesc);
            }

            if (light.Radius.HasValue && (!float.IsFinite(light.Radius.Value) || light.Radius.Value < 0f))
            {
                TomlFieldSpan? radiusField = FindEntryField(doc, asset.Provenance.TableIndex, "Radius", "Bricks.Entity.Light");
                AddFieldFinding(
                    findings,
                    "BRICK001",
                    ValidationSeverity.Broken,
                    in asset,
                    radiusField,
                    "Entity.Light.Radius",
                    $"Brick entity light Radius must be finite and non-negative, got {light.Radius.Value}.");
            }

            if (!float.IsFinite(light.Size) || light.Size <= 0f)
            {
                TomlFieldSpan? sizeField = FindEntryField(doc, asset.Provenance.TableIndex, "Size", "Bricks.Entity.Light");
                AddFieldFinding(
                    findings,
                    "BRICK001",
                    ValidationSeverity.Broken,
                    in asset,
                    sizeField,
                    "Entity.Light.Size",
                    $"Brick entity light Size must be finite and positive, got {light.Size}.");
            }

            if (!float.IsFinite(light.Intensity) || light.Intensity < 0f)
            {
                TomlFieldSpan? intensityField = FindEntryField(doc, asset.Provenance.TableIndex, "Intensity", "Bricks.Entity.Light");
                AddFieldFinding(
                    findings,
                    "BRICK001",
                    ValidationSeverity.Broken,
                    in asset,
                    intensityField,
                    "Entity.Light.Intensity",
                    $"Brick entity light Intensity must be finite and non-negative, got {light.Intensity}.");
            }

            if (!light.Offset.IsFinite)
            {
                TomlFieldSpan? offsetField = FindEntryField(doc, asset.Provenance.TableIndex, "Offset", "Bricks.Entity.Light");
                AddFieldFinding(
                    findings,
                    "BRICK001",
                    ValidationSeverity.Broken,
                    in asset,
                    offsetField,
                    "Entity.Light.Offset",
                    $"Brick entity light Offset must have finite components.");
            }
        }

        if (entity.SlotGroups != null && entity.SlotGroups.Count > 0)
        {
            IReadOnlyList<IndexedAsset> knownSlotGroups = workspace.GetWinningAssetsByKind(AssetKind.SlotGroup);
            HashSet<string>? knownSlotGroupIds = null;
            if (knownSlotGroups.Count > 0)
            {
                knownSlotGroupIds = new HashSet<string>(knownSlotGroups.Count, StringComparer.Ordinal);
                for (int k = 0; k < knownSlotGroups.Count; k++)
                {
                    knownSlotGroupIds.Add(knownSlotGroups[k].Id);
                }
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entity.SlotGroups.Count; i++)
            {
                BrickSlotGroupDefinition group = entity.SlotGroups[i];
                string groupId = group.ID?.Trim() ?? string.Empty;
                if (groupId.Length == 0)
                {
                    AddFieldFinding(
                        findings,
                        "BRICK002",
                        ValidationSeverity.Broken,
                        in asset,
                        null,
                        $"Entity.SlotGroups[{i}].ID",
                        "Brick entity slot group ID cannot be empty.");
                    continue;
                }

                if (!seen.Add(groupId))
                {
                    AddFieldFinding(
                        findings,
                        "BRICK002",
                        ValidationSeverity.Broken,
                        in asset,
                        null,
                        $"Entity.SlotGroups[{i}].ID",
                        $"Duplicate brick entity slot group '{groupId}'.");
                }

                if (group.Capacity.HasValue && group.Capacity.Value < 0)
                {
                    AddFieldFinding(
                        findings,
                        "BRICK002",
                        ValidationSeverity.Broken,
                        in asset,
                        null,
                        $"Entity.SlotGroups[{i}].Capacity",
                        $"Brick entity slot group '{groupId}' has negative capacity {group.Capacity.Value}.");
                }

                if (knownSlotGroupIds != null && !knownSlotGroupIds.Contains(groupId))
                {
                    AddFieldFinding(
                        findings,
                        "BRICK002",
                        ValidationSeverity.Broken,
                        in asset,
                        null,
                        $"Entity.SlotGroups[{i}].ID",
                        $"Brick entity slot group '{groupId}' does not match any loaded SlotGroup definition.");
                }
            }
        }
    }

    private static void ValidateBrickSoundTags(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlDocument? doc,
        string eventKey,
        string[]? tags,
        List<ValidationFinding> findings)
    {
        if (tags == null)
        {
            return;
        }

        TomlFieldSpan? field = FindEntryField(doc, asset.Provenance.TableIndex, eventKey, "Bricks.Sounds");
        for (int i = 0; i < tags.Length; i++)
        {
            string value = tags[i] ?? string.Empty;
            TomlTextSpan? span = GetArrayElementSpan(field, i);
            int line = span.HasValue ? span.Value.Line : ResolveEntryLine(doc, asset.Provenance.TableIndex);
            int col = span.HasValue ? span.Value.Column : 1;

            if (LooksLikeAudioFilePath(value))
            {
                string? suggestedSoundTag = FindUniqueSoundTagForAudioClip(workspace, value);
                findings.Add(new ValidationFinding(
                    "REF002",
                    ValidationSeverity.Broken,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    col,
                    $"Sounds.{eventKey}[{i}]",
                    $"Brick sound '{value}' uses an audio file path instead of an invariant sound tag name.",
                    SuggestedFixValue: suggestedSoundTag,
                    FieldSpan: span,
                    FixKind: suggestedSoundTag != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                    IsSafeBulkFix: suggestedSoundTag != null,
                    FixDescription: suggestedSoundTag != null ? "Replace audio clip path with unique sound tag" : null));
                continue;
            }

            if (!workspace.HasTag(value, locale: string.Empty))
            {
                string? caseMatchTag = FindInvariantTagCaseInsensitive(workspace, value);
                if (caseMatchTag != null)
                {
                    findings.Add(new ValidationFinding(
                        "PATH001",
                        ValidationSeverity.CrossPlatform,
                        asset.Provenance.ProviderRootId,
                        asset.Provenance.FilePath,
                        line,
                        col,
                        $"Sounds.{eventKey}[{i}]",
                        $"Invariant sound tag '{value}' differs by case from '{caseMatchTag}'.",
                        SuggestedFixValue: caseMatchTag,
                        FieldSpan: span,
                        FixKind: ValidationFixKind.ReplaceValue,
                        IsSafeBulkFix: true,
                        FixDescription: "Fix path/ID casing"));
                    continue;
                }

                string? suggestedTag = SuggestInvariantTagReference(workspace, value, soundTagsOnly: true);
                findings.Add(new ValidationFinding(
                    "REF001",
                    ValidationSeverity.Broken,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    col,
                    $"Sounds.{eventKey}[{i}]",
                    $"Invariant sound tag '{value}' was not found in lang/tags.",
                    SuggestedFixValue: suggestedTag,
                    FieldSpan: span,
                    FixKind: suggestedTag != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                    IsSafeBulkFix: false,
                    FixDescription: suggestedTag != null ? "Replace with closest sound tag" : null));
            }
        }
    }

    private static void ValidatePaint(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        in PaintDefinition paint,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        bool hasTextures = paint.Textures != null && paint.Textures.Length > 0;
        bool hasColor = !string.IsNullOrWhiteSpace(paint.Color);

        if (hasTextures == hasColor)
        {
            int line = ResolveEntryLine(doc, asset.Provenance.TableIndex);
            string detail = hasTextures
                ? $"Paint '{paint.ID}' specifies both Textures and Color; set exactly one."
                : $"Paint '{paint.ID}' specifies neither Textures nor Color; set exactly one.";

            TomlFieldSpan? colorField = hasTextures
                ? FindEntryField(doc, asset.Provenance.TableIndex, "Color")
                : null;
            TomlTextSpan? span = colorField?.LineSpan;
            int findingLine = colorField.HasValue ? colorField.Value.KeySpan.Line : line;
            int findingCol = colorField.HasValue ? colorField.Value.KeySpan.Column : 1;

            findings.Add(new ValidationFinding(
                "PAINT001",
                ValidationSeverity.Broken,
                asset.Provenance.ProviderRootId,
                asset.Provenance.FilePath,
                findingLine,
                findingCol,
                "Textures/Color",
                detail,
                FieldSpan: span,
                FixKind: colorField.HasValue ? ValidationFixKind.RemoveField : ValidationFixKind.None,
                IsSafeBulkFix: false,
                FixDescription: colorField.HasValue ? "Remove conflicting Color field" : null));
        }

        if (hasColor && !IsStrictSixDigitHexRgb(paint.Color))
        {
            TomlFieldSpan? colorField = FindEntryField(doc, asset.Provenance.TableIndex, "Color");
            (string? suggestedColor, bool isSafeColorFix, string? fixDesc) = TrySuggestNormalizedHexColor(paint.Color);
            AddFieldFinding(
                findings,
                "COLOR001",
                ValidationSeverity.Broken,
                in asset,
                colorField,
                "Color",
                $"Invalid 6-digit hex RGB color '{paint.Color}'.",
                suggestedFixValue: suggestedColor,
                fixKind: suggestedColor != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                isSafeBulkFix: isSafeColorFix,
                fixDescription: fixDesc);
        }

        if (!string.IsNullOrWhiteSpace(paint.Name))
        {
            ValidateLocalizationReference(workspace, in asset, doc, null, "Name", paint.Name, ValidationSeverity.Broken, findings);
        }

        if (hasTextures)
        {
            ValidateBrickFaceList(workspace, in asset, doc, null, "Textures", paint.Textures, paint.Connected, findings);
        }
    }

    private static void ValidateSkill(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        in SkillDefinition skill,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        if (!string.IsNullOrWhiteSpace(skill.Name))
        {
            ValidateLocalizationReference(workspace, in asset, doc, null, "Name", skill.Name, ValidationSeverity.Broken, findings);
        }

        if (!string.IsNullOrWhiteSpace(skill.Category))
        {
            ValidateLocalizationReference(workspace, in asset, doc, null, "Category", skill.Category, ValidationSeverity.Informational, findings);
        }

        if (!string.IsNullOrWhiteSpace(skill.Icon))
        {
            ValidateTextureReference(workspace, in asset, doc, null, "Icon", skill.Icon, ValidationSeverity.Informational, findings);
        }

        ValidateSkillLevels(in asset, in skill, doc, findings);

        int resolvedSources = 0;
        if (skill.Sources != null)
        {
            foreach (KeyValuePair<XPSource, Dictionary<string, int>> group in skill.Sources)
            {
                if (group.Value != null)
                {
                    resolvedSources += ValidateSkillSourceGroup(workspace, in asset, doc, group.Key, $"Skills.Sources.{group.Key}", group.Value, findings);
                }
            }
        }

        if (resolvedSources == 0)
        {
            int line = ResolveEntryLine(doc, asset.Provenance.TableIndex);
            findings.Add(new ValidationFinding(
                "SKILL003",
                ValidationSeverity.Style,
                asset.Provenance.ProviderRootId,
                asset.Provenance.FilePath,
                line,
                Column: 1,
                "Sources",
                $"Skill '{skill.ID}' has zero resolved XP sources."));
        }
    }

    private static void ValidateBody(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        in BodyDefinition body,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        if (body.States == null)
        {
            return;
        }

        foreach (KeyValuePair<string, Dictionary<string, string?[]>> statePair in body.States)
        {
            if (statePair.Value != null)
            {
                ValidateBodyStateDirections(workspace, in asset, doc, statePair.Key, statePair.Value, findings);
            }
        }
    }

    private static void ValidateRecipe(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        in RecipeDefinition recipe,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        TomlFieldSpan? targetTagField = FindEntryField(doc, asset.Provenance.TableIndex, "TargetTag");
        if (string.IsNullOrWhiteSpace(recipe.TargetTag))
        {
            AddFieldFinding(
                findings,
                "RECIPE001",
                ValidationSeverity.Broken,
                in asset,
                targetTagField,
                "TargetTag",
                $"Recipe '{recipe.ID}' must specify a non-empty TargetTag.");
        }

        TomlFieldSpan? durationField = FindEntryField(doc, asset.Provenance.TableIndex, "DurationSeconds");
        if (!float.IsFinite(recipe.DurationSeconds) || recipe.DurationSeconds < 0f)
        {
            AddFieldFinding(
                findings,
                "RECIPE002",
                ValidationSeverity.Broken,
                in asset,
                durationField,
                "DurationSeconds",
                $"Recipe '{recipe.ID}' DurationSeconds ({recipe.DurationSeconds.ToString(CultureInfo.InvariantCulture)}) must be finite and >= 0.");
        }

        TomlFieldSpan? powerField = FindEntryField(doc, asset.Provenance.TableIndex, "PowerKw");
        if (!float.IsFinite(recipe.PowerKw))
        {
            AddFieldFinding(
                findings,
                "RECIPE002",
                ValidationSeverity.Broken,
                in asset,
                powerField,
                "PowerKw",
                $"Recipe '{recipe.ID}' PowerKw must be a finite number.");
        }

        if (recipe.Inputs == null || recipe.Inputs.Count == 0)
        {
            int line = ResolveEntryLine(doc, asset.Provenance.TableIndex);
            findings.Add(new ValidationFinding(
                "RECIPE001",
                ValidationSeverity.Broken,
                asset.Provenance.ProviderRootId,
                asset.Provenance.FilePath,
                line,
                Column: 1,
                "Inputs",
                $"Recipe '{recipe.ID}' must define at least one input ingredient."));
        }
        else
        {
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                RecipeIngredientDefinition input = recipe.Inputs[i];
                string itemId = (input.ItemId ?? string.Empty).Trim();
                int line = ResolveEntryLine(doc, asset.Provenance.TableIndex);

                if (string.IsNullOrEmpty(itemId))
                {
                    findings.Add(new ValidationFinding(
                        "RECIPE001",
                        ValidationSeverity.Broken,
                        asset.Provenance.ProviderRootId,
                        asset.Provenance.FilePath,
                        line,
                        Column: 1,
                        $"Inputs[{i}].ItemId",
                        $"Recipe '{recipe.ID}' input #{i + 1} is missing an ItemId."));
                }
                else if (itemId.StartsWith("tag:", StringComparison.Ordinal))
                {
                    string tagName = itemId.Substring(4);
                    if (string.IsNullOrWhiteSpace(tagName))
                    {
                        findings.Add(new ValidationFinding(
                            "REF001",
                            ValidationSeverity.Broken,
                            asset.Provenance.ProviderRootId,
                            asset.Provenance.FilePath,
                            line,
                            Column: 1,
                            $"Inputs[{i}].ItemId",
                            $"Recipe '{recipe.ID}' input #{i + 1} has an empty tag selector."));
                    }
                    else if (!workspace.HasTag(tagName, locale: string.Empty))
                    {
                        string? caseMatch = FindInvariantTagCaseInsensitive(workspace, tagName);
                        if (caseMatch != null)
                        {
                            findings.Add(new ValidationFinding(
                                "PATH001",
                                ValidationSeverity.CrossPlatform,
                                asset.Provenance.ProviderRootId,
                                asset.Provenance.FilePath,
                                line,
                                Column: 1,
                                $"Inputs[{i}].ItemId",
                                $"Recipe input tag '{tagName}' differs by case from '{caseMatch}'.",
                                SuggestedFixValue: string.Concat("tag:", caseMatch)));
                        }
                        else
                        {
                            findings.Add(new ValidationFinding(
                                "REF001",
                                ValidationSeverity.Broken,
                                asset.Provenance.ProviderRootId,
                                asset.Provenance.FilePath,
                                line,
                                Column: 1,
                                $"Inputs[{i}].ItemId",
                                $"Recipe input tag '{tagName}' was not found in invariant tags."));
                        }
                    }
                }
                else if (!workspace.GetAsset(AssetKind.Item, itemId).Success)
                {
                    string? caseMatchItem = FindCaseInsensitiveAssetId(workspace, AssetKind.Item, itemId);
                    if (caseMatchItem != null)
                    {
                        findings.Add(new ValidationFinding(
                            "PATH001",
                            ValidationSeverity.CrossPlatform,
                            asset.Provenance.ProviderRootId,
                            asset.Provenance.FilePath,
                            line,
                            Column: 1,
                            $"Inputs[{i}].ItemId",
                            $"Recipe input item '{itemId}' differs by case from '{caseMatchItem}'.",
                            SuggestedFixValue: caseMatchItem));
                    }
                    else
                    {
                        string? suggested = SuggestAssetReference(workspace, AssetKind.Item, itemId);
                        findings.Add(new ValidationFinding(
                            "REF001",
                            ValidationSeverity.Broken,
                            asset.Provenance.ProviderRootId,
                            asset.Provenance.FilePath,
                            line,
                            Column: 1,
                            $"Inputs[{i}].ItemId",
                            $"Recipe input item '{itemId}' was not found.",
                            SuggestedFixValue: suggested));
                    }
                }

                if (input.Count < 1)
                {
                    findings.Add(new ValidationFinding(
                        "RECIPE002",
                        ValidationSeverity.Broken,
                        asset.Provenance.ProviderRootId,
                        asset.Provenance.FilePath,
                        line,
                        Column: 1,
                        $"Inputs[{i}].Count",
                        $"Recipe '{recipe.ID}' input #{i + 1} Count ({input.Count}) must be >= 1."));
                }
            }
        }

        bool isGenerator = float.IsFinite(recipe.PowerKw) && recipe.PowerKw > 0f;
        if (recipe.Outputs == null || recipe.Outputs.Count == 0)
        {
            if (!isGenerator)
            {
                int line = ResolveEntryLine(doc, asset.Provenance.TableIndex);
                findings.Add(new ValidationFinding(
                    "RECIPE001",
                    ValidationSeverity.Broken,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    Column: 1,
                    "Outputs",
                    $"Recipe '{recipe.ID}' must define at least one output unless PowerKw > 0."));
            }
        }
        else
        {
            for (int i = 0; i < recipe.Outputs.Count; i++)
            {
                RecipeOutputDefinition output = recipe.Outputs[i];
                string itemId = (output.ItemId ?? string.Empty).Trim();
                int line = ResolveEntryLine(doc, asset.Provenance.TableIndex);

                if (string.IsNullOrEmpty(itemId))
                {
                    findings.Add(new ValidationFinding(
                        "RECIPE001",
                        ValidationSeverity.Broken,
                        asset.Provenance.ProviderRootId,
                        asset.Provenance.FilePath,
                        line,
                        Column: 1,
                        $"Outputs[{i}].ItemId",
                        $"Recipe '{recipe.ID}' output #{i + 1} is missing an ItemId."));
                }
                else if (!workspace.GetAsset(AssetKind.Item, itemId).Success)
                {
                    string? caseMatchItem = FindCaseInsensitiveAssetId(workspace, AssetKind.Item, itemId);
                    if (caseMatchItem != null)
                    {
                        findings.Add(new ValidationFinding(
                            "PATH001",
                            ValidationSeverity.CrossPlatform,
                            asset.Provenance.ProviderRootId,
                            asset.Provenance.FilePath,
                            line,
                            Column: 1,
                            $"Outputs[{i}].ItemId",
                            $"Recipe output item '{itemId}' differs by case from '{caseMatchItem}'.",
                            SuggestedFixValue: caseMatchItem));
                    }
                    else
                    {
                        string? suggested = SuggestAssetReference(workspace, AssetKind.Item, itemId);
                        findings.Add(new ValidationFinding(
                            "REF001",
                            ValidationSeverity.Broken,
                            asset.Provenance.ProviderRootId,
                            asset.Provenance.FilePath,
                            line,
                            Column: 1,
                            $"Outputs[{i}].ItemId",
                            $"Recipe output item '{itemId}' was not found.",
                            SuggestedFixValue: suggested));
                    }
                }

                if (output.Count < 1)
                {
                    findings.Add(new ValidationFinding(
                        "RECIPE002",
                        ValidationSeverity.Broken,
                        asset.Provenance.ProviderRootId,
                        asset.Provenance.FilePath,
                        line,
                        Column: 1,
                        $"Outputs[{i}].Count",
                        $"Recipe '{recipe.ID}' output #{i + 1} Count ({output.Count}) must be >= 1."));
                }

                if (output.Chance.HasValue && (!float.IsFinite(output.Chance.Value) || output.Chance.Value <= 0f || output.Chance.Value > 1f))
                {
                    findings.Add(new ValidationFinding(
                        "RECIPE002",
                        ValidationSeverity.Broken,
                        asset.Provenance.ProviderRootId,
                        asset.Provenance.FilePath,
                        line,
                        Column: 1,
                        $"Outputs[{i}].Chance",
                        $"Recipe '{recipe.ID}' output #{i + 1} Chance ({output.Chance.Value.ToString(CultureInfo.InvariantCulture)}) must be in (0, 1]."));
                }
            }
        }
    }

    private static void ValidateSlotGroup(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        in SlotGroupDefinition slotGroup,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        TomlFieldSpan? capacityField = FindEntryField(doc, asset.Provenance.TableIndex, "Capacity");
        if (slotGroup.Capacity < 0)
        {
            AddFieldFinding(
                findings,
                "SLOTGROUP001",
                ValidationSeverity.Broken,
                in asset,
                capacityField,
                "Capacity",
                $"SlotGroup '{slotGroup.ID}' Capacity ({slotGroup.Capacity}) must be >= 0.");
        }

        TomlFieldSpan? groupAllowedTagsField = FindEntryField(doc, asset.Provenance.TableIndex, "AllowedTags");
        ValidateSlotGroupTagList(
            workspace,
            in asset,
            doc,
            groupAllowedTagsField,
            fieldPrefix: "AllowedTags",
            slotGroup.AllowedTags,
            findings);

        if (slotGroup.Slots == null || slotGroup.Slots.Count == 0)
        {
            return;
        }

        List<TomlTableSpan> slotTables = FindOwnedSlotGroupSubTables(doc, asset.Provenance.TableIndex);
        HashSet<int> seenIndices = [];

        for (int s = 0; s < slotGroup.Slots.Count; s++)
        {
            SlotFilterDefinition slotDef = slotGroup.Slots[s];
            TomlTableSpan? slotTable = s < slotTables.Count ? slotTables[s] : null;
            TomlFieldSpan? indexField = null;
            TomlFieldSpan? slotTagsField = null;
            if (doc != null && slotTable.HasValue)
            {
                Result<TomlFieldSpan> idxRes = doc.GetFieldInTable(slotTable.Value.Index, "Index");
                if (idxRes.Success)
                {
                    indexField = idxRes.Value;
                }

                Result<TomlFieldSpan> tagsRes = doc.GetFieldInTable(slotTable.Value.Index, "AllowedTags");
                if (tagsRes.Success)
                {
                    slotTagsField = tagsRes.Value;
                }
            }

            if (slotDef.Index < 0)
            {
                AddFieldFinding(
                    findings,
                    "SLOTGROUP001",
                    ValidationSeverity.Broken,
                    in asset,
                    indexField,
                    $"Slots[{s}].Index",
                    $"SlotGroup '{slotGroup.ID}' slot override #{s + 1} Index ({slotDef.Index}) must be >= 0.");
            }
            else
            {
                if (slotGroup.Capacity >= 0 && slotDef.Index >= slotGroup.Capacity)
                {
                    AddFieldFinding(
                        findings,
                        "SLOTGROUP001",
                        ValidationSeverity.Broken,
                        in asset,
                        indexField,
                        $"Slots[{s}].Index",
                        $"SlotGroup '{slotGroup.ID}' slot override Index ({slotDef.Index}) must be < Capacity ({slotGroup.Capacity}).");
                }

                if (!seenIndices.Add(slotDef.Index))
                {
                    AddFieldFinding(
                        findings,
                        "SLOTGROUP002",
                        ValidationSeverity.Broken,
                        in asset,
                        indexField,
                        $"Slots[{s}].Index",
                        $"SlotGroup '{slotGroup.ID}' defines duplicate slot override Index {slotDef.Index}.");
                }
            }

            ValidateSlotGroupTagList(
                workspace,
                in asset,
                doc,
                slotTagsField,
                fieldPrefix: $"Slots[{s}].AllowedTags",
                slotDef.AllowedTags,
                findings);
        }
    }

    private static void ValidateSlotGroupTagList(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlDocument? doc,
        TomlFieldSpan? field,
        string fieldPrefix,
        List<string>? tags,
        List<ValidationFinding> findings)
    {
        if (tags == null || tags.Count == 0)
        {
            return;
        }

        HashSet<string> seenTags = new(StringComparer.Ordinal);
        for (int i = 0; i < tags.Count; i++)
        {
            string rawTag = tags[i] ?? string.Empty;
            string tagName = rawTag.Trim();
            TomlTextSpan? span = GetArrayElementSpan(field, i);
            int line = span.HasValue ? span.Value.Line : ResolveEntryLine(doc, asset.Provenance.TableIndex);
            int col = span.HasValue ? span.Value.Column : 1;
            string fieldLabel = $"{fieldPrefix}[{i}]";

            if (string.IsNullOrEmpty(tagName))
            {
                findings.Add(new ValidationFinding(
                    "SLOTGROUP002",
                    ValidationSeverity.Broken,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    col,
                    fieldLabel,
                    $"SlotGroup '{asset.Id}' {fieldPrefix} entry #{i + 1} cannot be empty.",
                    FieldSpan: span));
                continue;
            }

            if (!seenTags.Add(tagName))
            {
                findings.Add(new ValidationFinding(
                    "TAG001",
                    ValidationSeverity.Style,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    col,
                    fieldLabel,
                    $"Duplicate tag '{tagName}' in SlotGroup '{asset.Id}' {fieldPrefix}.",
                    FieldSpan: span,
                    FixKind: span.HasValue ? ValidationFixKind.RemoveArrayElement : ValidationFixKind.None,
                    IsSafeBulkFix: span.HasValue,
                    FixDescription: span.HasValue ? "Remove duplicate tag entry" : null));
            }

            if (workspace.HasTag(tagName, locale: string.Empty))
            {
                continue;
            }

            string? caseMatch = FindInvariantTagCaseInsensitive(workspace, tagName);
            if (caseMatch != null)
            {
                findings.Add(new ValidationFinding(
                    "PATH001",
                    ValidationSeverity.CrossPlatform,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    col,
                    fieldLabel,
                    $"Invariant tag '{tagName}' differs by case from '{caseMatch}'.",
                    SuggestedFixValue: caseMatch,
                    FieldSpan: span,
                    FixKind: span.HasValue ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                    IsSafeBulkFix: span.HasValue,
                    FixDescription: span.HasValue ? "Fix path/ID casing" : null));
                continue;
            }

            string? suggestedTag = SuggestInvariantTagReference(workspace, tagName, soundTagsOnly: false);
            findings.Add(new ValidationFinding(
                "REF001",
                ValidationSeverity.Broken,
                asset.Provenance.ProviderRootId,
                asset.Provenance.FilePath,
                line,
                col,
                fieldLabel,
                $"Invariant tag '{tagName}' was not found in lang/tags.",
                SuggestedFixValue: suggestedTag,
                FieldSpan: span,
                FixKind: suggestedTag != null && span.HasValue ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                IsSafeBulkFix: false,
                FixDescription: suggestedTag != null && span.HasValue ? "Replace with closest invariant tag" : null));
        }
    }

    private static List<TomlTableSpan> FindOwnedSlotGroupSubTables(TomlDocument? doc, int ownerTableIndex)
    {
        List<TomlTableSpan> matches = [];
        if (doc == null || ownerTableIndex < 0 || ownerTableIndex >= doc.Tables.Count)
        {
            return matches;
        }

        for (int i = ownerTableIndex + 1; i < doc.Tables.Count; i++)
        {
            TomlTableSpan next = doc.Tables[i];
            if (next.IsArrayTable && string.Equals(next.HeaderPath, "SlotGroups", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.Equals(next.HeaderPath, "SlotGroups.Slots", StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(next);
                continue;
            }

            if (!next.HeaderPath.StartsWith("SlotGroups.", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return matches;
    }

    private static void ValidateBar(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        in BarDefinition bar,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(bar.StatId))
        {
            TomlFieldSpan? statField = FindEntryField(doc, asset.Provenance.TableIndex, "StatId");
            AddFieldFinding(
                findings,
                "BAR001",
                ValidationSeverity.Broken,
                in asset,
                statField,
                "StatId",
                $"Bar '{asset.Id}' is missing StatId.");
        }

        string? maxStatId = string.IsNullOrWhiteSpace(bar.MaxStatId) ? null : bar.MaxStatId.Trim();
        if (bar.DefaultMax.HasValue && (!float.IsFinite(bar.DefaultMax.Value) || bar.DefaultMax.Value <= 0f))
        {
            TomlFieldSpan? defaultMaxField = FindEntryField(doc, asset.Provenance.TableIndex, "DefaultMax");
            AddFieldFinding(
                findings,
                "BAR001",
                ValidationSeverity.Broken,
                in asset,
                defaultMaxField,
                "DefaultMax",
                $"Bar '{asset.Id}' DefaultMax must be finite and > 0.");
        }

        if (maxStatId == null && !bar.DefaultMax.HasValue)
        {
            TomlFieldSpan? maxField = FindEntryField(doc, asset.Provenance.TableIndex, "MaxStatId");
            AddFieldFinding(
                findings,
                "BAR001",
                ValidationSeverity.Broken,
                in asset,
                maxField,
                "MaxStatId",
                $"Bar '{asset.Id}' must specify MaxStatId or a positive DefaultMax.");
        }

        int iconCount = bar.IconCount ?? Bar.DEFAULT_ICON_COUNT;
        if (iconCount < 1)
        {
            TomlFieldSpan? iconCountField = FindEntryField(doc, asset.Provenance.TableIndex, "IconCount");
            AddFieldFinding(
                findings,
                "BAR001",
                ValidationSeverity.Broken,
                in asset,
                iconCountField,
                "IconCount",
                $"Bar '{asset.Id}' IconCount ({iconCount}) must be >= 1.");
        }

        ValidateBarIconField(workspace, in asset, doc, "IconFull", bar.IconFull, findings);
        ValidateBarIconField(workspace, in asset, doc, "IconHalf", bar.IconHalf, findings);
        ValidateBarIconField(workspace, in asset, doc, "IconEmpty", bar.IconEmpty, findings);

        float warn = bar.WarnThreshold ?? Bar.DEFAULT_WARN_THRESHOLD;
        if (!float.IsFinite(warn) || warn < 0f || warn > 1f)
        {
            TomlFieldSpan? warnField = FindEntryField(doc, asset.Provenance.TableIndex, "WarnThreshold");
            AddFieldFinding(
                findings,
                "BAR001",
                ValidationSeverity.Broken,
                in asset,
                warnField,
                "WarnThreshold",
                $"Bar '{asset.Id}' WarnThreshold must be within [0, 1].");
        }

        float critical = bar.CriticalThreshold ?? Bar.DEFAULT_CRITICAL_THRESHOLD;
        if (!float.IsFinite(critical) || critical < 0f || critical > 1f)
        {
            TomlFieldSpan? criticalField = FindEntryField(doc, asset.Provenance.TableIndex, "CriticalThreshold");
            AddFieldFinding(
                findings,
                "BAR001",
                ValidationSeverity.Broken,
                in asset,
                criticalField,
                "CriticalThreshold",
                $"Bar '{asset.Id}' CriticalThreshold must be within [0, 1].");
        }
        else if (float.IsFinite(warn) && critical > warn)
        {
            TomlFieldSpan? criticalField = FindEntryField(doc, asset.Provenance.TableIndex, "CriticalThreshold");
            AddFieldFinding(
                findings,
                "BAR001",
                ValidationSeverity.Broken,
                in asset,
                criticalField,
                "CriticalThreshold",
                $"Bar '{asset.Id}' CriticalThreshold ({critical}) cannot exceed WarnThreshold ({warn}).");
        }

        if (bar.ColorTint != null && (string.IsNullOrWhiteSpace(bar.ColorTint) || !HexColor.TryParseRgba(bar.ColorTint, out _)))
        {
            TomlFieldSpan? colorField = FindEntryField(doc, asset.Provenance.TableIndex, "ColorTint");
            AddFieldFinding(
                findings,
                "BAR001",
                ValidationSeverity.Broken,
                in asset,
                colorField,
                "ColorTint",
                $"Bar '{asset.Id}' has invalid ColorTint '{bar.ColorTint}'.");
        }
    }

    private static void ValidateBarIconField(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlDocument? doc,
        string fieldName,
        string? value,
        List<ValidationFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            TomlFieldSpan? field = FindEntryField(doc, asset.Provenance.TableIndex, fieldName);
            AddFieldFinding(
                findings,
                "BAR001",
                ValidationSeverity.Broken,
                in asset,
                field,
                fieldName,
                $"Bar '{asset.Id}' is missing {fieldName}.");
            return;
        }

        ValidateTextureReference(workspace, in asset, doc, null, fieldName, value.Trim(), ValidationSeverity.Broken, findings);
    }

    private static void ValidateBuff(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        in BuffDefinition buff,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(buff.Name))
        {
            TomlFieldSpan? nameField = FindEntryField(doc, asset.Provenance.TableIndex, "Name");
            AddFieldFinding(
                findings,
                "BUFF001",
                ValidationSeverity.Broken,
                in asset,
                nameField,
                "Name",
                $"Buff '{asset.Id}' is missing Name localization key.");
        }
        else
        {
            ValidateLocalizationReference(workspace, in asset, doc, null, "Name", buff.Name.Trim(), ValidationSeverity.Broken, findings);
        }

        if (!string.IsNullOrWhiteSpace(buff.Description))
        {
            ValidateLocalizationReference(workspace, in asset, doc, null, "Description", buff.Description.Trim(), ValidationSeverity.Style, findings);
        }

        if (!string.IsNullOrWhiteSpace(buff.Icon))
        {
            ValidateTextureReference(workspace, in asset, doc, null, "Icon", buff.Icon.Trim(), ValidationSeverity.Broken, findings);
        }

        if (!float.IsFinite(buff.DurationSeconds) || buff.DurationSeconds <= 0f)
        {
            TomlFieldSpan? durationField = FindEntryField(doc, asset.Provenance.TableIndex, "DurationSeconds");
            AddFieldFinding(
                findings,
                "BUFF001",
                ValidationSeverity.Broken,
                in asset,
                durationField,
                "DurationSeconds",
                $"Buff '{asset.Id}' DurationSeconds ({buff.DurationSeconds}) must be finite and > 0.");
        }

        if (buff.StackPolicy != null)
        {
            string trimmedPolicy = buff.StackPolicy.Trim();
            if (!string.Equals(trimmedPolicy, "Refresh", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(trimmedPolicy, "Extend", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(trimmedPolicy, "Ignore", StringComparison.OrdinalIgnoreCase))
            {
                TomlFieldSpan? policyField = FindEntryField(doc, asset.Provenance.TableIndex, "StackPolicy");
                AddFieldFinding(
                    findings,
                    "BUFF001",
                    ValidationSeverity.Broken,
                    in asset,
                    policyField,
                    "StackPolicy",
                    $"Buff '{asset.Id}' has invalid StackPolicy '{buff.StackPolicy}'; expected Refresh, Extend, or Ignore.");
            }
        }

        if (buff.StatModifiers != null)
        {
            List<TomlTableSpan> modTables = FindOwnedBuffSubTables(doc, asset.Provenance.TableIndex, "Buffs.StatModifiers");
            for (int i = 0; i < buff.StatModifiers.Count; i++)
            {
                BuffStatModifierDefinition mod = buff.StatModifiers[i];
                TomlTableSpan? modTable = i < modTables.Count ? modTables[i] : null;
                TomlFieldSpan? statField = ResolveSubTableField(doc, modTable, "StatId");
                TomlFieldSpan? flatField = ResolveSubTableField(doc, modTable, "FlatDelta");
                TomlFieldSpan? pctField = ResolveSubTableField(doc, modTable, "PercentDelta");

                if (string.IsNullOrWhiteSpace(mod.StatId))
                {
                    AddFieldFinding(
                        findings,
                        "BUFF001",
                        ValidationSeverity.Broken,
                        in asset,
                        statField,
                        $"StatModifiers[{i}].StatId",
                        $"Buff '{asset.Id}' StatModifiers[{i}] is missing StatId.");
                }

                if (mod.FlatDelta.HasValue && !float.IsFinite(mod.FlatDelta.Value))
                {
                    AddFieldFinding(
                        findings,
                        "BUFF001",
                        ValidationSeverity.Broken,
                        in asset,
                        flatField,
                        $"StatModifiers[{i}].FlatDelta",
                        $"Buff '{asset.Id}' StatModifiers[{i}] FlatDelta must be finite.");
                }

                if (mod.PercentDelta.HasValue && !float.IsFinite(mod.PercentDelta.Value))
                {
                    AddFieldFinding(
                        findings,
                        "BUFF001",
                        ValidationSeverity.Broken,
                        in asset,
                        pctField,
                        $"StatModifiers[{i}].PercentDelta",
                        $"Buff '{asset.Id}' StatModifiers[{i}] PercentDelta must be finite.");
                }

                if (!mod.FlatDelta.HasValue && !mod.PercentDelta.HasValue)
                {
                    AddFieldFinding(
                        findings,
                        "BUFF001",
                        ValidationSeverity.Broken,
                        in asset,
                        flatField ?? statField,
                        $"StatModifiers[{i}]",
                        $"Buff '{asset.Id}' StatModifiers[{i}] must specify FlatDelta or PercentDelta.");
                }
                else if ((mod.FlatDelta ?? 0f) == 0f && (mod.PercentDelta ?? 0f) == 0f)
                {
                    AddFieldFinding(
                        findings,
                        "BUFF001",
                        ValidationSeverity.Broken,
                        in asset,
                        flatField ?? pctField,
                        $"StatModifiers[{i}]",
                        $"Buff '{asset.Id}' StatModifiers[{i}] must have a non-zero delta.");
                }
            }
        }

        if (buff.RegenRates != null)
        {
            List<TomlTableSpan> regenTables = FindOwnedBuffSubTables(doc, asset.Provenance.TableIndex, "Buffs.RegenRates");
            for (int i = 0; i < buff.RegenRates.Count; i++)
            {
                BuffRegenRateDefinition regen = buff.RegenRates[i];
                TomlTableSpan? regenTable = i < regenTables.Count ? regenTables[i] : null;
                TomlFieldSpan? statField = ResolveSubTableField(doc, regenTable, "StatId");
                TomlFieldSpan? rateField = ResolveSubTableField(doc, regenTable, "PerSecond");

                if (string.IsNullOrWhiteSpace(regen.StatId))
                {
                    AddFieldFinding(
                        findings,
                        "BUFF001",
                        ValidationSeverity.Broken,
                        in asset,
                        statField,
                        $"RegenRates[{i}].StatId",
                        $"Buff '{asset.Id}' RegenRates[{i}] is missing StatId.");
                }

                if (!float.IsFinite(regen.PerSecond) || regen.PerSecond == 0f)
                {
                    AddFieldFinding(
                        findings,
                        "BUFF001",
                        ValidationSeverity.Broken,
                        in asset,
                        rateField,
                        $"RegenRates[{i}].PerSecond",
                        $"Buff '{asset.Id}' RegenRates[{i}] PerSecond must be finite and non-zero.");
                }
            }
        }
    }

    private static TomlFieldSpan? ResolveSubTableField(TomlDocument? doc, TomlTableSpan? table, string fieldName)
    {
        if (doc == null || !table.HasValue)
        {
            return null;
        }

        Result<TomlFieldSpan> res = doc.GetFieldInTable(table.Value.Index, fieldName);
        return res.Success ? res.Value : null;
    }

    private static List<TomlTableSpan> FindOwnedBuffSubTables(TomlDocument? doc, int ownerTableIndex, string headerPath)
    {
        List<TomlTableSpan> matches = [];
        if (doc == null || ownerTableIndex < 0 || ownerTableIndex >= doc.Tables.Count)
        {
            return matches;
        }

        for (int i = ownerTableIndex + 1; i < doc.Tables.Count; i++)
        {
            TomlTableSpan next = doc.Tables[i];
            if (next.IsArrayTable && string.Equals(next.HeaderPath, "Buffs", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.Equals(next.HeaderPath, headerPath, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(next);
                continue;
            }

            if (!next.HeaderPath.StartsWith("Buffs.", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return matches;
    }

    private static void ValidateMaterial(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        in MaterialDefinition material,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        TomlFieldSpan? shaderField = FindEntryField(doc, entryTableIndex: -1, "Shader");
        if (string.IsNullOrWhiteSpace(material.Shader))
        {
            AddFieldFinding(
                findings,
                "REF001",
                ValidationSeverity.Broken,
                in asset,
                shaderField,
                "Shader",
                "Material is missing required Shader reference.");
        }
        else if (HasShaderGrammarError(workspace, material.Shader))
        {
            string? suggestedShader = TrySuggestNormalizedShader(workspace, material.Shader);
            AddFieldFinding(
                findings,
                "REF002",
                ValidationSeverity.Broken,
                in asset,
                shaderField,
                "Shader",
                $"Shader reference '{material.Shader}' must omit file extension and use '/' separators.",
                suggestedFixValue: suggestedShader,
                fixKind: suggestedShader != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                isSafeBulkFix: suggestedShader != null,
                fixDescription: suggestedShader != null ? "Normalize shader reference" : null);
        }
        else if (!workspace.GetResource(ResourceKind.Shader, material.Shader).Success)
        {
            Result<IndexedResource> caseMatch = workspace.FindResourceCaseInsensitive(ResourceKind.Shader, material.Shader);
            if (caseMatch.Success)
            {
                AddFieldFinding(
                    findings,
                    "PATH001",
                    ValidationSeverity.CrossPlatform,
                    in asset,
                    shaderField,
                    "Shader",
                    $"Shader path '{material.Shader}' differs by case from '{caseMatch.Value.Id}'.",
                    suggestedFixValue: caseMatch.Value.Id,
                    fixKind: ValidationFixKind.ReplaceValue,
                    isSafeBulkFix: true,
                    fixDescription: "Fix path/ID casing");
            }
            else
            {
                string? suggestedShader = SuggestResourceReference(workspace, ResourceKind.Shader, material.Shader);
                AddFieldFinding(
                    findings,
                    "REF001",
                    ValidationSeverity.Broken,
                    in asset,
                    shaderField,
                    "Shader",
                    $"Shader '{material.Shader}' was not found under shaders/.",
                    suggestedFixValue: suggestedShader,
                    fixKind: suggestedShader != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                    isSafeBulkFix: false,
                    fixDescription: suggestedShader != null ? "Replace with closest matching shader" : null);
            }
        }

        if (material.Textures == null)
        {
            return;
        }

        TomlFieldSpan? texturesField = FindEntryField(doc, entryTableIndex: -1, "Textures");
        for (int i = 0; i < material.Textures.Length; i++)
        {
            string texPath = material.Textures[i] ?? string.Empty;
            TomlTextSpan? span = GetArrayElementSpan(texturesField, i);
            ValidateTextureReferenceSpan(
                workspace,
                in asset,
                span,
                $"Textures[{i}]",
                texPath,
                ValidationSeverity.Broken,
                findings);
        }
    }

    private static void ValidateBrickFaceList(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlDocument? doc,
        string? subTablePath,
        string faceKey,
        string?[]? layers,
        bool connected,
        List<ValidationFinding> findings)
    {
        if (layers == null || layers.Length == 0)
        {
            return;
        }

        TomlFieldSpan? field = FindEntryField(doc, asset.Provenance.TableIndex, faceKey, subTablePath);
        string fieldLabel = string.IsNullOrEmpty(subTablePath) ? faceKey : $"Textures.{faceKey}";

        for (int i = 0; i < layers.Length; i++)
        {
            string layer = layers[i] ?? string.Empty;
            TomlTextSpan? span = GetArrayElementSpan(field, i);
            int line = span.HasValue ? span.Value.Line : ResolveEntryLine(doc, asset.Provenance.TableIndex);
            int col = span.HasValue ? span.Value.Column : 1;

            if (!IsValidBrickFaceGrammar(layer))
            {
                string? suggestedLayer = TrySuggestNormalizedBlockLayer(workspace, layer);
                findings.Add(new ValidationFinding(
                    "REF002",
                    ValidationSeverity.Broken,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    col,
                    $"{fieldLabel}[{i}]",
                    $"Brick face layer '{layer}' must be a bare albedo layer name without directory, extension, or PBR suffix.",
                    SuggestedFixValue: suggestedLayer,
                    FieldSpan: span,
                    FixKind: suggestedLayer != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                    IsSafeBulkFix: suggestedLayer != null,
                    FixDescription: suggestedLayer != null ? "Normalize block texture layer name" : null));
                continue;
            }

            if (!workspace.WinningBlockAlbedoLayers.ContainsKey(layer))
            {
                AddMissingBlockLayerFinding(workspace, in asset, layer, line, col, $"{fieldLabel}[{i}]", span, findings);
                continue;
            }

            if (connected)
            {
                ValidateConnectedSequence(workspace, in asset, layer, line, col, $"{fieldLabel}[{i}]", span, findings);
            }
        }
    }

    private static void AddMissingBlockLayerFinding(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        string layer,
        int line,
        int col,
        string fieldLabel,
        TomlTextSpan? span,
        List<ValidationFinding> findings)
    {
        Result<IndexedResource> caseMatch = workspace.FindBlockAlbedoCaseInsensitive(layer);
        if (caseMatch.Success)
        {
            findings.Add(new ValidationFinding(
                "PATH001",
                ValidationSeverity.CrossPlatform,
                asset.Provenance.ProviderRootId,
                asset.Provenance.FilePath,
                line,
                col,
                fieldLabel,
                $"Block texture layer '{layer}' differs by case from '{caseMatch.Value.Id}'.",
                SuggestedFixValue: caseMatch.Value.Id,
                FieldSpan: span,
                FixKind: ValidationFixKind.ReplaceValue,
                IsSafeBulkFix: true,
                FixDescription: "Fix path/ID casing"));
            return;
        }

        string? suggestedLayer = SuggestBlockLayerReference(workspace, layer);
        findings.Add(new ValidationFinding(
            "REF001",
            ValidationSeverity.Broken,
            asset.Provenance.ProviderRootId,
            asset.Provenance.FilePath,
            line,
            col,
            fieldLabel,
            $"Block albedo texture layer '{layer}' was not found under textures/block/; game falls back to layer 0.",
            SuggestedFixValue: suggestedLayer,
            FieldSpan: span,
            FixKind: suggestedLayer != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
            IsSafeBulkFix: false,
            FixDescription: suggestedLayer != null ? "Replace with closest block texture layer" : null));
    }

    private static void AddSyntaxDiagnostics(TomlDocument doc, string rootId, string filePath, List<ValidationFinding> findings)
    {
        for (int i = 0; i < doc.Diagnostics.Count; i++)
        {
            TomlDiagnostic diag = doc.Diagnostics[i];
            if (!diag.IsError)
            {
                continue;
            }

            findings.Add(new ValidationFinding(
                "TOML001",
                ValidationSeverity.Fatal,
                rootId,
                filePath,
                diag.Span.Line,
                diag.Span.Column,
                string.Empty,
                diag.Message,
                FieldSpan: diag.Span));
        }
    }

    private static void ValidateBrickColliders(
        in IndexedAsset asset,
        TomlDocument? doc,
        List<BrickColliderDefinition> colliders,
        List<ValidationFinding> findings)
    {
        int line = ResolveEntryLine(doc, asset.Provenance.TableIndex);
        for (int i = 0; i < colliders.Count; i++)
        {
            Result<BrickColliderShape> built = colliders[i].Build();
            if (built.Success)
            {
                continue;
            }

            findings.Add(new ValidationFinding(
                "COLLIDER001",
                ValidationSeverity.Broken,
                asset.Provenance.ProviderRootId,
                asset.Provenance.FilePath,
                line,
                Column: 1,
                $"Colliders[{i}]",
                built.Message ?? "Invalid collider definition."));
        }
    }

    private static void ValidateSkillLevels(
        in IndexedAsset asset,
        in SkillDefinition skill,
        TomlDocument? doc,
        List<ValidationFinding> findings)
    {
        if (skill.Levels == null || skill.Levels.Count == 0)
        {
            TomlTextSpan? headerLineSpan = doc != null &&
                                           asset.Provenance.TableIndex >= 0 &&
                                           asset.Provenance.TableIndex < doc.Tables.Count
                ? doc.Tables[asset.Provenance.TableIndex].HeaderLineSpan
                : null;
            int line = headerLineSpan.HasValue ? headerLineSpan.Value.Line : ResolveEntryLine(doc, asset.Provenance.TableIndex);

            findings.Add(new ValidationFinding(
                "SKILL001",
                ValidationSeverity.Broken,
                asset.Provenance.ProviderRootId,
                asset.Provenance.FilePath,
                line,
                Column: 1,
                "Levels",
                $"Skill '{skill.ID}' must define a non-empty Levels table.",
                FieldSpan: headerLineSpan,
                FixKind: headerLineSpan.HasValue ? ValidationFixKind.InsertDefaultSkillLevels : ValidationFixKind.None,
                IsSafeBulkFix: false,
                FixDescription: headerLineSpan.HasValue ? "Add default Levels (1 = 100)" : null));
            return;
        }

        foreach (KeyValuePair<int, int> level in skill.Levels)
        {
            if (level.Key >= 1 && level.Value >= 1)
            {
                continue;
            }

            TomlFieldSpan? levelSpan = FindEntryField(doc, asset.Provenance.TableIndex, level.Key.ToString(CultureInfo.InvariantCulture), "Skills.Levels");
            bool canClampXp = level.Key >= 1 && level.Value < 1 && levelSpan.HasValue;
            AddFieldFinding(
                findings,
                "SKILL002",
                ValidationSeverity.Broken,
                in asset,
                levelSpan,
                $"Levels.{level.Key}",
                $"Skill level key ({level.Key}) and XP requirement ({level.Value}) must both be >= 1.",
                suggestedFixValue: canClampXp ? "1" : null,
                fixKind: canClampXp ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                isSafeBulkFix: false,
                fixDescription: canClampXp ? "Clamp XP requirement to 1" : null);
        }
    }

    private static int ValidateSkillSourceGroup(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlDocument? doc,
        XPSource sourceKind,
        string subPath,
        Dictionary<string, int> sources,
        List<ValidationFinding> findings)
    {
        int resolved = 0;
        bool isCraftSource = sourceKind is XPSource.Craft or XPSource.Process;
        foreach (KeyValuePair<string, int> source in sources)
        {
            TomlFieldSpan? sourceSpan = FindEntryField(doc, asset.Provenance.TableIndex, source.Key, subPath);
            TomlTextSpan? keySpan = sourceSpan?.KeySpan;
            int line = keySpan.HasValue ? keySpan.Value.Line : ResolveEntryLine(doc, asset.Provenance.TableIndex);
            int col = keySpan.HasValue ? keySpan.Value.Column : 1;

            if (isCraftSource && source.Key.StartsWith("station:", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(source.Key.Substring(8)))
                {
                    resolved++;
                }

                continue;
            }

            if (isCraftSource && source.Key.StartsWith("recipe:", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(source.Key.Substring(7)))
                {
                    resolved++;
                }

                continue;
            }

            if (source.Key.StartsWith("tag:", StringComparison.Ordinal))
            {
                string tagName = source.Key.Substring(4);
                if (!workspace.HasTag(tagName, locale: string.Empty))
                {
                    string? caseMatchTag = FindInvariantTagCaseInsensitive(workspace, tagName);
                    if (caseMatchTag != null)
                    {
                        findings.Add(new ValidationFinding(
                            "PATH001",
                            ValidationSeverity.CrossPlatform,
                            asset.Provenance.ProviderRootId,
                            asset.Provenance.FilePath,
                            line,
                            col,
                            $"{subPath}.{source.Key}",
                            $"Skill XP source tag '{tagName}' differs by case from '{caseMatchTag}'.",
                            SuggestedFixValue: string.Concat("tag:", caseMatchTag),
                            FieldSpan: keySpan,
                            FixKind: keySpan.HasValue ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                            IsSafeBulkFix: keySpan.HasValue,
                            FixDescription: keySpan.HasValue ? "Fix path/ID casing" : null));
                        continue;
                    }

                    string? suggestedTag = SuggestInvariantTagReference(workspace, tagName);
                    string? suggestedKey = suggestedTag != null ? string.Concat("tag:", suggestedTag) : null;
                    findings.Add(new ValidationFinding(
                        "REF001",
                        ValidationSeverity.Broken,
                        asset.Provenance.ProviderRootId,
                        asset.Provenance.FilePath,
                        line,
                        col,
                        $"{subPath}.{source.Key}",
                        $"Skill XP source tag '{tagName}' was not found in invariant tags.",
                        SuggestedFixValue: suggestedKey,
                        FieldSpan: keySpan,
                        FixKind: suggestedKey != null && keySpan.HasValue ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                        IsSafeBulkFix: false,
                        FixDescription: suggestedKey != null ? "Replace with closest invariant tag" : null));
                    continue;
                }

                if (workspace.GetEffectiveTagValues(tagName, locale: string.Empty).Count > 0)
                {
                    resolved++;
                }

                continue;
            }

            if (isCraftSource)
            {
                if (!string.IsNullOrWhiteSpace(source.Key))
                {
                    resolved++;
                }

                continue;
            }

            if (!workspace.GetAsset(AssetKind.Brick, source.Key).Success)
            {
                string? caseMatchBrick = FindCaseInsensitiveAssetId(workspace, AssetKind.Brick, source.Key);
                if (caseMatchBrick != null)
                {
                    findings.Add(new ValidationFinding(
                        "PATH001",
                        ValidationSeverity.CrossPlatform,
                        asset.Provenance.ProviderRootId,
                        asset.Provenance.FilePath,
                        line,
                        col,
                        $"{subPath}.{source.Key}",
                        $"Skill XP source brick '{source.Key}' differs by case from '{caseMatchBrick}'.",
                        SuggestedFixValue: caseMatchBrick,
                        FieldSpan: keySpan,
                        FixKind: keySpan.HasValue ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                        IsSafeBulkFix: keySpan.HasValue,
                        FixDescription: keySpan.HasValue ? "Fix path/ID casing" : null));
                    continue;
                }

                string? suggestedBrick = SuggestAssetReference(workspace, AssetKind.Brick, source.Key);
                findings.Add(new ValidationFinding(
                    "REF001",
                    ValidationSeverity.Broken,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    col,
                    $"{subPath}.{source.Key}",
                    $"Skill XP source brick '{source.Key}' was not found.",
                    SuggestedFixValue: suggestedBrick,
                    FieldSpan: keySpan,
                    FixKind: suggestedBrick != null && keySpan.HasValue ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                    IsSafeBulkFix: false,
                    FixDescription: suggestedBrick != null ? "Replace with closest matching brick" : null));
                continue;
            }

            resolved++;
        }

        return resolved;
    }

    private static void ValidateBodyStateDirections(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlDocument? doc,
        string stateKey,
        Dictionary<string, string?[]> directions,
        List<ValidationFinding> findings)
    {
        string subTablePath = $"Bodies.States.{stateKey}";
        foreach (KeyValuePair<string, string?[]> dirPair in directions)
        {
            if (dirPair.Value == null)
            {
                continue;
            }

            TomlFieldSpan? field = FindEntryField(doc, asset.Provenance.TableIndex, dirPair.Key, subTablePath);
            for (int i = 0; i < dirPair.Value.Length; i++)
            {
                string path = dirPair.Value[i] ?? string.Empty;
                TomlTextSpan? span = GetArrayElementSpan(field, i);
                ValidateTextureReferenceSpan(
                    workspace,
                    in asset,
                    span,
                    $"States.{stateKey}.{dirPair.Key}[{i}]",
                    path,
                    ValidationSeverity.Broken,
                    findings);
            }
        }
    }

    private static void ValidateConnectedSequence(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        string baseLayer,
        int line,
        int col,
        string fieldLabel,
        TomlTextSpan? span,
        List<ValidationFinding> findings)
    {
        IReadOnlyList<string> ordered = workspace.OrderedAlbedoLayerNames;
        int baseIndex = -1;
        for (int i = 1; i < ordered.Count; i++)
        {
            if (string.Equals(ordered[i], baseLayer, StringComparison.Ordinal))
            {
                baseIndex = i;
                break;
            }
        }

        if (baseIndex < 0)
        {
            return;
        }

        if (!baseLayer.EndsWith(".0", StringComparison.Ordinal))
        {
            if (baseIndex + 15 >= ordered.Count)
            {
                findings.Add(new ValidationFinding(
                    "TEX002",
                    ValidationSeverity.Broken,
                    asset.Provenance.ProviderRootId,
                    asset.Provenance.FilePath,
                    line,
                    col,
                    fieldLabel,
                    $"Connected texture '{baseLayer}' does not have 16 contiguous albedo layers.",
                    FieldSpan: span));
            }

            return;
        }

        string stem = baseLayer.Substring(0, baseLayer.Length - 2);
        for (int mask = 0; mask < 16; mask++)
        {
            string expected = $"{stem}.{mask}";
            int slot = baseIndex + mask;
            if (slot < ordered.Count && string.Equals(ordered[slot], expected, StringComparison.Ordinal))
            {
                continue;
            }

            findings.Add(new ValidationFinding(
                "TEX002",
                ValidationSeverity.Broken,
                asset.Provenance.ProviderRootId,
                asset.Provenance.FilePath,
                line,
                col,
                fieldLabel,
                $"Connected texture '{baseLayer}' is missing contiguous 16-layer entry '{expected}'.",
                FieldSpan: span));
            return;
        }
    }

    private static void ValidateTextureReference(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlDocument? doc,
        string? subTablePath,
        string fieldName,
        string value,
        ValidationSeverity missingSeverity,
        List<ValidationFinding> findings)
    {
        TomlFieldSpan? field = FindEntryField(doc, asset.Provenance.TableIndex, fieldName, subTablePath);
        ValidateTextureReferenceSpan(workspace, in asset, field?.ValueSpan, fieldName, value, missingSeverity, findings);
    }

    private static void ValidateTextureReferenceSpan(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlTextSpan? span,
        string fieldLabel,
        string value,
        ValidationSeverity missingSeverity,
        List<ValidationFinding> findings)
    {
        int line = span.HasValue ? span.Value.Line : 1;
        int col = span.HasValue ? span.Value.Column : 1;

        bool hasAccidentalPrefix = HasAccidentalCategoryPrefix(value, "textures/") &&
                                   !workspace.GetResource(ResourceKind.Texture, value).Success;
        if (!HasAllowedExtension(value, TEXTURE_EXTENSIONS) || value.Contains('\\') || value.StartsWith('/') || hasAccidentalPrefix)
        {
            string? suggestedTexture = TrySuggestNormalizedResource(workspace, ResourceKind.Texture, value);
            findings.Add(new ValidationFinding(
                "REF002",
                missingSeverity,
                asset.Provenance.ProviderRootId,
                asset.Provenance.FilePath,
                line,
                col,
                fieldLabel,
                $"Texture reference '{value}' must be a relative path with extension (for example 'items/drill.png').",
                SuggestedFixValue: suggestedTexture,
                FieldSpan: span,
                FixKind: suggestedTexture != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                IsSafeBulkFix: suggestedTexture != null,
                FixDescription: suggestedTexture != null ? "Normalize texture path" : null));
            return;
        }

        if (workspace.GetResource(ResourceKind.Texture, value).Success)
        {
            return;
        }

        Result<IndexedResource> caseMatch = workspace.FindResourceCaseInsensitive(ResourceKind.Texture, value);
        if (caseMatch.Success)
        {
            findings.Add(new ValidationFinding(
                "PATH001",
                ValidationSeverity.CrossPlatform,
                asset.Provenance.ProviderRootId,
                asset.Provenance.FilePath,
                line,
                col,
                fieldLabel,
                $"Texture path '{value}' differs by case from '{caseMatch.Value.Id}'.",
                SuggestedFixValue: caseMatch.Value.Id,
                FieldSpan: span,
                FixKind: ValidationFixKind.ReplaceValue,
                IsSafeBulkFix: true,
                FixDescription: "Fix path/ID casing"));
            return;
        }

        string? suggestedClose = SuggestResourceReference(workspace, ResourceKind.Texture, value);
        findings.Add(new ValidationFinding(
            "REF001",
            missingSeverity,
            asset.Provenance.ProviderRootId,
            asset.Provenance.FilePath,
            line,
            col,
            fieldLabel,
            $"Texture '{value}' was not found under textures/.",
            SuggestedFixValue: suggestedClose,
            FieldSpan: span,
            FixKind: suggestedClose != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
            IsSafeBulkFix: false,
            FixDescription: suggestedClose != null ? "Replace with closest matching texture" : null));
    }

    private static void ValidateMeshReference(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlDocument? doc,
        string? subTablePath,
        string fieldLabel,
        string tomlKey,
        string value,
        ValidationSeverity missingSeverity,
        List<ValidationFinding> findings)
    {
        TomlFieldSpan? field = FindEntryField(doc, asset.Provenance.TableIndex, tomlKey, subTablePath);
        bool hasAccidentalPrefix = HasAccidentalCategoryPrefix(value, "meshes/") &&
                                   !workspace.GetResource(ResourceKind.Mesh, value).Success;
        if (!value.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) || value.Contains('\\') || value.StartsWith('/') || hasAccidentalPrefix)
        {
            string? suggestedMesh = TrySuggestNormalizedResource(workspace, ResourceKind.Mesh, value);
            AddFieldFinding(
                findings,
                "REF002",
                missingSeverity,
                in asset,
                field,
                fieldLabel,
                $"Mesh reference '{value}' must be a relative path with '.obj' extension.",
                suggestedFixValue: suggestedMesh,
                fixKind: suggestedMesh != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                isSafeBulkFix: suggestedMesh != null,
                fixDescription: suggestedMesh != null ? "Normalize mesh path" : null);
            return;
        }

        if (workspace.GetResource(ResourceKind.Mesh, value).Success)
        {
            return;
        }

        Result<IndexedResource> caseMatch = workspace.FindResourceCaseInsensitive(ResourceKind.Mesh, value);
        if (caseMatch.Success)
        {
            AddFieldFinding(
                findings,
                "PATH001",
                ValidationSeverity.CrossPlatform,
                in asset,
                field,
                fieldLabel,
                $"Mesh path '{value}' differs by case from '{caseMatch.Value.Id}'.",
                suggestedFixValue: caseMatch.Value.Id,
                fixKind: ValidationFixKind.ReplaceValue,
                isSafeBulkFix: true,
                fixDescription: "Fix path/ID casing");
            return;
        }

        string? suggestedClose = SuggestResourceReference(workspace, ResourceKind.Mesh, value);
        AddFieldFinding(
            findings,
            "REF001",
            missingSeverity,
            in asset,
            field,
            fieldLabel,
            $"Mesh '{value}' was not found under meshes/.",
            suggestedFixValue: suggestedClose,
            fixKind: suggestedClose != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
            isSafeBulkFix: false,
            fixDescription: suggestedClose != null ? "Replace with closest matching mesh" : null);
    }

    private static void ValidateMaterialReference(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlDocument? doc,
        string? subTablePath,
        string fieldLabel,
        string tomlKey,
        string value,
        ValidationSeverity missingSeverity,
        List<ValidationFinding> findings)
    {
        TomlFieldSpan? field = FindEntryField(doc, asset.Provenance.TableIndex, tomlKey, subTablePath);
        bool hasAccidentalPrefix = HasAccidentalCategoryPrefix(value, "materials/") &&
                                   !workspace.GetAsset(AssetKind.Material, value).Success;
        if (value.EndsWith(".toml", StringComparison.OrdinalIgnoreCase) || value.Contains('\\') || value.StartsWith('/') || hasAccidentalPrefix)
        {
            string? suggestedMaterial = TrySuggestNormalizedMaterial(workspace, value);
            AddFieldFinding(
                findings,
                "REF002",
                missingSeverity,
                in asset,
                field,
                fieldLabel,
                $"Material reference '{value}' must omit the '.toml' extension.",
                suggestedFixValue: suggestedMaterial,
                fixKind: suggestedMaterial != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                isSafeBulkFix: suggestedMaterial != null,
                fixDescription: suggestedMaterial != null ? "Normalize material reference" : null);
            return;
        }

        if (workspace.GetAsset(AssetKind.Material, value).Success)
        {
            return;
        }

        string? caseMatch = FindCaseInsensitiveAssetId(workspace, AssetKind.Material, value);
        if (caseMatch != null)
        {
            AddFieldFinding(
                findings,
                "PATH001",
                ValidationSeverity.CrossPlatform,
                in asset,
                field,
                fieldLabel,
                $"Material reference '{value}' differs by case from '{caseMatch}'.",
                suggestedFixValue: caseMatch,
                fixKind: ValidationFixKind.ReplaceValue,
                isSafeBulkFix: true,
                fixDescription: "Fix path/ID casing");
            return;
        }

        string? suggestedClose = SuggestAssetReference(workspace, AssetKind.Material, value);
        AddFieldFinding(
            findings,
            "REF001",
            missingSeverity,
            in asset,
            field,
            fieldLabel,
            $"Material '{value}' was not found under materials/.",
            suggestedFixValue: suggestedClose,
            fixKind: suggestedClose != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
            isSafeBulkFix: false,
            fixDescription: suggestedClose != null ? "Replace with closest matching material" : null);
    }

    private static void ValidateLocalizationReference(
        WorkspaceIndex workspace,
        in IndexedAsset asset,
        TomlDocument? doc,
        string? subTablePath,
        string fieldName,
        string key,
        ValidationSeverity missingSeverity,
        List<ValidationFinding> findings)
    {
        TomlFieldSpan? field = FindEntryField(doc, asset.Provenance.TableIndex, fieldName, subTablePath);
        if (!key.Contains('.') || key.Contains(' ') || key.Contains('/'))
        {
            string? suggestedKey = TrySuggestNormalizedLocalizationKey(workspace, key);
            AddFieldFinding(
                findings,
                "REF002",
                missingSeverity,
                in asset,
                field,
                fieldName,
                $"Localization key '{key}' should use dot-key grammar (for example 'item.base.core').",
                suggestedFixValue: suggestedKey,
                fixKind: suggestedKey != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                isSafeBulkFix: suggestedKey != null,
                fixDescription: suggestedKey != null ? "Normalize localization key" : null);
            return;
        }

        if (workspace.HasLocalizationKey(key, locale: "en"))
        {
            return;
        }

        string? caseMatchKey = FindLocalizationKeyCaseInsensitive(workspace, key, locale: "en");
        if (caseMatchKey != null)
        {
            AddFieldFinding(
                findings,
                "PATH001",
                ValidationSeverity.CrossPlatform,
                in asset,
                field,
                fieldName,
                $"Localization key '{key}' differs by case from '{caseMatchKey}'.",
                suggestedFixValue: caseMatchKey,
                fixKind: ValidationFixKind.ReplaceValue,
                isSafeBulkFix: true,
                fixDescription: "Fix path/ID casing");
            return;
        }

        AddFieldFinding(
            findings,
            "LOC001",
            missingSeverity,
            in asset,
            field,
            fieldName,
            $"Localization key '{key}' is missing from English translation tables.",
            suggestedFixValue: key,
            fixKind: ValidationFixKind.CreateLocalizationKey,
            isSafeBulkFix: false,
            fixDescription: "Create missing localization key");
    }

    private static void ValidateLocalization(WorkspaceIndex workspace, List<ValidationFinding> findings)
    {
        Dictionary<(string RootId, string Locale, string Key), IndexedLocalizationEntry> seenPerRoot = new();

        for (int i = 0; i < workspace.LocalizationEntries.Count; i++)
        {
            IndexedLocalizationEntry entry = workspace.LocalizationEntries[i];
            var composite = (entry.RootId, entry.Locale.ToLowerInvariant(), entry.Key);
            if (seenPerRoot.TryGetValue(composite, out IndexedLocalizationEntry previous))
            {
                findings.Add(new ValidationFinding(
                    "LOC002",
                    ValidationSeverity.Broken,
                    entry.RootId,
                    entry.FilePath,
                    entry.FieldSpan.KeySpan.Line,
                    entry.FieldSpan.KeySpan.Column,
                    entry.Key,
                    $"Duplicate localization key '{entry.Key}' for locale '{entry.Locale}' (also defined in '{Path.GetFileName(previous.FilePath)}').",
                    FieldSpan: entry.FieldSpan.KeySpan));
            }
            else
            {
                seenPerRoot[composite] = entry;
            }
        }
    }

    private static void ValidateTags(WorkspaceIndex workspace, List<ValidationFinding> findings)
    {
        for (int i = 0; i < workspace.TagEntries.Count; i++)
        {
            IndexedTagEntry tag = workspace.TagEntries[i];
            HashSet<string> seenValues = new(StringComparer.Ordinal);
            string fileName = Path.GetFileName(tag.FilePath);

            for (int j = 0; j < tag.Values.Count; j++)
            {
                string value = tag.Values[j];
                TomlTextSpan? span = j < tag.FieldSpan.ArrayElements.Count
                    ? tag.FieldSpan.ArrayElements[j].ValueSpan
                    : tag.FieldSpan.ValueSpan;
                bool hasElementSpan = j < tag.FieldSpan.ArrayElements.Count;
                int line = span.HasValue ? span.Value.Line : tag.FieldSpan.KeySpan.Line;
                int col = span.HasValue ? span.Value.Column : tag.FieldSpan.KeySpan.Column;

                if (!seenValues.Add(value))
                {
                    findings.Add(new ValidationFinding(
                        "TAG001",
                        ValidationSeverity.Style,
                        tag.RootId,
                        tag.FilePath,
                        line,
                        col,
                        $"{tag.TagName}[{j}]",
                        $"Duplicate tag entry '{value}' in tag '{tag.TagName}'.",
                        FieldSpan: span,
                        FixKind: hasElementSpan ? ValidationFixKind.RemoveArrayElement : ValidationFixKind.None,
                        IsSafeBulkFix: hasElementSpan,
                        FixDescription: hasElementSpan ? "Remove duplicate tag entry" : null));
                }

                ValidateTagTargetValue(workspace, in tag, fileName, value, j, line, col, span, findings);
            }
        }
    }

    private static void ValidateTagTargetValue(
        WorkspaceIndex workspace,
        in IndexedTagEntry tag,
        string fileName,
        string value,
        int index,
        int line,
        int col,
        TomlTextSpan? span,
        List<ValidationFinding> findings)
    {
        if (string.Equals(fileName, "sounds.toml", StringComparison.OrdinalIgnoreCase))
        {
            bool hasAccidentalPrefix = HasAccidentalCategoryPrefix(value, "audio/") &&
                                       !workspace.GetResource(ResourceKind.Audio, value).Success;
            if (!HasAllowedExtension(value, AUDIO_EXTENSIONS) || value.Contains('\\') || value.StartsWith('/') || hasAccidentalPrefix)
            {
                string? suggestedAudio = TrySuggestNormalizedResource(workspace, ResourceKind.Audio, value);
                findings.Add(new ValidationFinding(
                    "REF002",
                    ValidationSeverity.Broken,
                    tag.RootId,
                    tag.FilePath,
                    line,
                    col,
                    $"{tag.TagName}[{index}]",
                    $"Sound clip '{value}' must be a relative audio path with extension.",
                    SuggestedFixValue: suggestedAudio,
                    FieldSpan: span,
                    FixKind: suggestedAudio != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                    IsSafeBulkFix: suggestedAudio != null,
                    FixDescription: suggestedAudio != null ? "Normalize audio clip path" : null));
                return;
            }

            if (workspace.GetResource(ResourceKind.Audio, value).Success)
            {
                return;
            }

            Result<IndexedResource> caseMatch = workspace.FindResourceCaseInsensitive(ResourceKind.Audio, value);
            if (caseMatch.Success)
            {
                findings.Add(new ValidationFinding(
                    "PATH001",
                    ValidationSeverity.CrossPlatform,
                    tag.RootId,
                    tag.FilePath,
                    line,
                    col,
                    $"{tag.TagName}[{index}]",
                    $"Audio clip path '{value}' differs by case from '{caseMatch.Value.Id}'.",
                    SuggestedFixValue: caseMatch.Value.Id,
                    FieldSpan: span,
                    FixKind: ValidationFixKind.ReplaceValue,
                    IsSafeBulkFix: true,
                    FixDescription: "Fix path/ID casing"));
                return;
            }

            string? suggestedCloseAudio = SuggestResourceReference(workspace, ResourceKind.Audio, value);
            findings.Add(new ValidationFinding(
                "REF001",
                ValidationSeverity.Broken,
                tag.RootId,
                tag.FilePath,
                line,
                col,
                $"{tag.TagName}[{index}]",
                $"Audio clip '{value}' was not found under audio/.",
                SuggestedFixValue: suggestedCloseAudio,
                FieldSpan: span,
                FixKind: suggestedCloseAudio != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
                IsSafeBulkFix: false,
                FixDescription: suggestedCloseAudio != null ? "Replace with closest matching audio clip" : null));
            return;
        }

        if (tag.TagName.StartsWith("brick.", StringComparison.Ordinal) ||
            string.Equals(fileName, "bricks.toml", StringComparison.OrdinalIgnoreCase))
        {
            ValidateTagAssetReference(workspace, in tag, AssetKind.Brick, "brick", value, index, line, col, span, findings);
            return;
        }

        if (tag.TagName.StartsWith("paint.", StringComparison.Ordinal) ||
            string.Equals(fileName, "paints.toml", StringComparison.OrdinalIgnoreCase))
        {
            ValidateTagAssetReference(workspace, in tag, AssetKind.Paint, "paint", value, index, line, col, span, findings);
            return;
        }

        if (tag.TagName.StartsWith("item.", StringComparison.Ordinal) ||
            string.Equals(fileName, "items.toml", StringComparison.OrdinalIgnoreCase))
        {
            ValidateTagAssetReference(workspace, in tag, AssetKind.Item, "item", value, index, line, col, span, findings);
        }
    }

    private static void ValidateTagAssetReference(
        WorkspaceIndex workspace,
        in IndexedTagEntry tag,
        AssetKind kind,
        string kindLabel,
        string value,
        int index,
        int line,
        int col,
        TomlTextSpan? span,
        List<ValidationFinding> findings)
    {
        if (workspace.GetAsset(kind, value).Success)
        {
            return;
        }

        string? caseMatch = FindCaseInsensitiveAssetId(workspace, kind, value);
        if (caseMatch != null)
        {
            findings.Add(new ValidationFinding(
                "PATH001",
                ValidationSeverity.CrossPlatform,
                tag.RootId,
                tag.FilePath,
                line,
                col,
                $"{tag.TagName}[{index}]",
                $"{kind} tag entry '{value}' differs by case from '{caseMatch}'.",
                SuggestedFixValue: caseMatch,
                FieldSpan: span,
                FixKind: ValidationFixKind.ReplaceValue,
                IsSafeBulkFix: true,
                FixDescription: "Fix path/ID casing"));
            return;
        }

        string? suggested = SuggestAssetReference(workspace, kind, value);
        findings.Add(new ValidationFinding(
            "REF001",
            ValidationSeverity.Broken,
            tag.RootId,
            tag.FilePath,
            line,
            col,
            $"{tag.TagName}[{index}]",
            $"Dangling {kindLabel} tag value '{value}' in '{tag.TagName}'.",
            SuggestedFixValue: suggested,
            FieldSpan: span,
            FixKind: suggested != null ? ValidationFixKind.ReplaceValue : ValidationFixKind.None,
            IsSafeBulkFix: false,
            FixDescription: suggested != null ? $"Replace with closest matching {kindLabel}" : null));
    }

    private static void AddFieldFinding(
        List<ValidationFinding> findings,
        string code,
        ValidationSeverity severity,
        in IndexedAsset asset,
        TomlFieldSpan? field,
        string fieldLabel,
        string message,
        string? suggestedFixValue = null,
        ValidationFixKind fixKind = ValidationFixKind.None,
        bool isSafeBulkFix = false,
        string? fixDescription = null)
    {
        int line = field.HasValue ? field.Value.ValueSpan.Line : 1;
        int col = field.HasValue ? field.Value.ValueSpan.Column : 1;
        findings.Add(new ValidationFinding(
            code,
            severity,
            asset.Provenance.ProviderRootId,
            asset.Provenance.FilePath,
            line,
            col,
            fieldLabel,
            message,
            SuggestedFixValue: suggestedFixValue,
            FieldSpan: field?.ValueSpan,
            FixKind: fixKind,
            IsSafeBulkFix: isSafeBulkFix,
            FixDescription: fixDescription));
    }

    private static TomlDocument? TryGetDocument(WorkspaceIndex workspace, string filePath)
    {
        return workspace.DocumentsByPath.TryGetValue(filePath, out TomlDocument? doc) ? doc : null;
    }

    private static TomlFieldSpan? FindEntryField(
        TomlDocument? doc,
        int entryTableIndex,
        string key,
        string? subTablePath = null)
    {
        if (doc == null)
        {
            return null;
        }

        if (entryTableIndex < 0)
        {
            Result<TomlFieldSpan> rootField = doc.GetRootField(key);
            return rootField.Success ? rootField.Value : null;
        }

        if (string.IsNullOrEmpty(subTablePath))
        {
            Result<TomlFieldSpan> direct = doc.GetFieldInTable(entryTableIndex, key);
            return direct.Success ? direct.Value : null;
        }

        TomlTableSpan? subTable = FindSubTable(doc, entryTableIndex, subTablePath);
        if (!subTable.HasValue)
        {
            return null;
        }

        Result<TomlFieldSpan> subField = doc.GetFieldInTable(subTable.Value.Index, key);
        return subField.Success ? subField.Value : null;
    }

    private static TomlTableSpan? FindSubTable(TomlDocument doc, int entryTableIndex, string subTablePath)
    {
        if (entryTableIndex < 0 || entryTableIndex >= doc.Tables.Count)
        {
            return null;
        }

        string entryHeader = doc.Tables[entryTableIndex].HeaderPath;
        for (int i = entryTableIndex + 1; i < doc.Tables.Count; i++)
        {
            TomlTableSpan table = doc.Tables[i];
            if (table.IsArrayTable && string.Equals(table.HeaderPath, entryHeader, StringComparison.Ordinal))
            {
                break;
            }

            if (string.Equals(table.HeaderPath, subTablePath, StringComparison.OrdinalIgnoreCase))
            {
                return table;
            }
        }

        return null;
    }

    private static List<TomlTableSpan> FindSubTablesWithPrefix(TomlDocument doc, int entryTableIndex, string prefix)
    {
        List<TomlTableSpan> matches = [];
        if (entryTableIndex < 0 || entryTableIndex >= doc.Tables.Count)
        {
            return matches;
        }

        string entryHeader = doc.Tables[entryTableIndex].HeaderPath;
        for (int i = entryTableIndex + 1; i < doc.Tables.Count; i++)
        {
            TomlTableSpan table = doc.Tables[i];
            if (table.IsArrayTable && string.Equals(table.HeaderPath, entryHeader, StringComparison.Ordinal))
            {
                break;
            }

            if (table.HeaderPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(table);
            }
        }

        return matches;
    }

    private static TomlTextSpan? GetArrayElementSpan(TomlFieldSpan? field, int elementIndex)
    {
        if (!field.HasValue)
        {
            return null;
        }

        if (elementIndex >= 0 && elementIndex < field.Value.ArrayElements.Count)
        {
            return field.Value.ArrayElements[elementIndex].ValueSpan;
        }

        return field.Value.ValueSpan;
    }

    private static int ResolveEntryLine(TomlDocument? doc, int tableIndex)
    {
        if (doc != null && tableIndex >= 0 && tableIndex < doc.Tables.Count)
        {
            return doc.Tables[tableIndex].HeaderSpan.Line;
        }

        return 1;
    }

    private static string ResolveRootIdForPath(WorkspaceIndex workspace, string filePath)
    {
        for (int i = workspace.Roots.Count - 1; i >= 0; i--)
        {
            WorkspaceRoot root = workspace.Roots[i];
            string prefix = Path.GetFullPath(root.RootDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (filePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return root.Id;
            }
        }

        return string.Empty;
    }

    private static string? FindCaseInsensitiveAssetId(WorkspaceIndex workspace, AssetKind kind, string id)
    {
        foreach (KeyValuePair<(AssetKind Kind, string Id), IndexedAsset> kvp in workspace.WinningAssets)
        {
            if (kvp.Key.Kind == kind && string.Equals(kvp.Key.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Key.Id;
            }
        }

        return null;
    }

    private static string? FindLocalizationKeyCaseInsensitive(WorkspaceIndex workspace, string key, string locale)
    {
        for (int i = 0; i < workspace.LocalizationEntries.Count; i++)
        {
            IndexedLocalizationEntry entry = workspace.LocalizationEntries[i];
            if (string.Equals(entry.Locale, locale, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Key;
            }
        }

        return null;
    }

    private static bool IsValidBrickFaceGrammar(string layer)
    {
        if (string.IsNullOrWhiteSpace(layer) ||
            layer.Contains('/') ||
            layer.Contains('\\') ||
            HasAllowedExtension(layer, TEXTURE_EXTENSIONS))
        {
            return false;
        }

        for (int i = 0; i < NON_ALBEDO_LAYER_SUFFIXES.Length; i++)
        {
            if (layer.EndsWith(NON_ALBEDO_LAYER_SUFFIXES[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string? FindInvariantTagCaseInsensitive(WorkspaceIndex workspace, string tagName)
    {
        for (int i = 0; i < workspace.TagEntries.Count; i++)
        {
            IndexedTagEntry entry = workspace.TagEntries[i];
            if (string.IsNullOrEmpty(entry.Locale) &&
                string.Equals(entry.TagName, tagName, StringComparison.OrdinalIgnoreCase))
            {
                return entry.TagName;
            }
        }

        return null;
    }

    private static string? FindUniqueSoundTagForAudioClip(WorkspaceIndex workspace, string audioClipPath)
    {
        string normalized = StripLeadingSlashAndAssets(audioClipPath.Trim().Replace('\\', '/'));
        string strippedAudio = StripPrefixIgnoreCase(normalized, "audio/");
        string? match = null;

        for (int i = 0; i < workspace.TagEntries.Count; i++)
        {
            IndexedTagEntry entry = workspace.TagEntries[i];
            if (!string.IsNullOrEmpty(entry.Locale))
            {
                continue;
            }

            if (!string.Equals(Path.GetFileName(entry.FilePath), "sounds.toml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (int j = 0; j < entry.Values.Count; j++)
            {
                if (!string.Equals(entry.Values[j], normalized, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(entry.Values[j], strippedAudio, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (match == null)
                {
                    match = entry.TagName;
                }
                else if (!string.Equals(match, entry.TagName, StringComparison.Ordinal))
                {
                    return null;
                }

                break;
            }
        }

        return match;
    }

    private static bool HasShaderGrammarError(WorkspaceIndex workspace, string shader)
    {
        if (HasAllowedExtension(shader, SHADER_EXTENSIONS) || shader.Contains('\\') || shader.StartsWith('/'))
        {
            return true;
        }

        return HasAccidentalCategoryPrefix(shader, "shaders/") &&
               !workspace.GetResource(ResourceKind.Shader, shader).Success;
    }

    private static string? TrySuggestNormalizedShader(WorkspaceIndex workspace, string rawShader)
    {
        string normalized = StripLeadingSlashAndAssets(rawShader.Trim().Replace('\\', '/'));
        string? direct = TryResolveShaderCandidate(workspace, normalized);
        if (direct != null)
        {
            return direct;
        }

        string withoutPrefix = StripPrefixIgnoreCase(normalized, "shaders/");
        return !string.Equals(withoutPrefix, normalized, StringComparison.Ordinal)
            ? TryResolveShaderCandidate(workspace, withoutPrefix)
            : null;
    }

    private static string? TryResolveShaderCandidate(WorkspaceIndex workspace, string candidate)
    {
        for (int i = 0; i < SHADER_EXTENSIONS.Length; i++)
        {
            if (candidate.EndsWith(SHADER_EXTENSIONS[i], StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate.Substring(0, candidate.Length - SHADER_EXTENSIONS[i].Length);
                break;
            }
        }

        if (workspace.GetResource(ResourceKind.Shader, candidate).Success)
        {
            return candidate;
        }

        Result<IndexedResource> caseMatch = workspace.FindResourceCaseInsensitive(ResourceKind.Shader, candidate);
        return caseMatch.Success ? caseMatch.Value.Id : null;
    }

    private static string? TrySuggestNormalizedMaterial(WorkspaceIndex workspace, string rawMaterial)
    {
        string normalized = StripLeadingSlashAndAssets(rawMaterial.Trim().Replace('\\', '/'));
        string? direct = TryResolveMaterialCandidate(workspace, normalized);
        if (direct != null)
        {
            return direct;
        }

        string withoutPrefix = StripPrefixIgnoreCase(normalized, "materials/");
        return !string.Equals(withoutPrefix, normalized, StringComparison.Ordinal)
            ? TryResolveMaterialCandidate(workspace, withoutPrefix)
            : null;
    }

    private static string? TryResolveMaterialCandidate(WorkspaceIndex workspace, string candidate)
    {
        if (candidate.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate.Substring(0, candidate.Length - 5);
        }

        if (workspace.GetAsset(AssetKind.Material, candidate).Success)
        {
            return candidate;
        }

        return FindCaseInsensitiveAssetId(workspace, AssetKind.Material, candidate);
    }

    private static string? TrySuggestNormalizedResource(WorkspaceIndex workspace, ResourceKind kind, string rawPath)
    {
        string normalized = StripLeadingSlashAndAssets(rawPath.Trim().Replace('\\', '/'));
        string? direct = TryResolveResourceCandidate(workspace, kind, normalized);
        if (direct != null)
        {
            return direct;
        }

        string? categoryPrefix = kind switch
        {
            ResourceKind.Texture => "textures/",
            ResourceKind.Mesh => "meshes/",
            ResourceKind.Audio => "audio/",
            ResourceKind.Shader => "shaders/",
            _ => null
        };

        if (categoryPrefix != null)
        {
            string withoutPrefix = StripPrefixIgnoreCase(normalized, categoryPrefix);
            if (!string.Equals(withoutPrefix, normalized, StringComparison.Ordinal))
            {
                string? resolved = TryResolveResourceCandidate(workspace, kind, withoutPrefix);
                if (resolved != null)
                {
                    return resolved;
                }
            }
        }

        return null;
    }

    private static string? TryResolveResourceCandidate(WorkspaceIndex workspace, ResourceKind kind, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        if (workspace.GetResource(kind, candidate).Success)
        {
            return candidate;
        }

        Result<IndexedResource> caseMatch = workspace.FindResourceCaseInsensitive(kind, candidate);
        if (caseMatch.Success)
        {
            return caseMatch.Value.Id;
        }

        if (kind == ResourceKind.Mesh && !candidate.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
        {
            string withObj = string.Concat(candidate, ".obj");
            if (workspace.GetResource(ResourceKind.Mesh, withObj).Success)
            {
                return withObj;
            }

            Result<IndexedResource> objCase = workspace.FindResourceCaseInsensitive(ResourceKind.Mesh, withObj);
            if (objCase.Success)
            {
                return objCase.Value.Id;
            }
        }

        string[]? fallbackExtensions = kind switch
        {
            ResourceKind.Texture when !HasAllowedExtension(candidate, TEXTURE_EXTENSIONS) => TEXTURE_EXTENSIONS,
            ResourceKind.Audio when !HasAllowedExtension(candidate, AUDIO_EXTENSIONS) => AUDIO_EXTENSIONS,
            _ => null
        };

        if (fallbackExtensions != null)
        {
            string? uniqueExtMatch = null;
            for (int i = 0; i < fallbackExtensions.Length; i++)
            {
                string withExt = string.Concat(candidate, fallbackExtensions[i]);
                string? matchedId = null;
                if (workspace.GetResource(kind, withExt).Success)
                {
                    matchedId = withExt;
                }
                else
                {
                    Result<IndexedResource> extCase = workspace.FindResourceCaseInsensitive(kind, withExt);
                    if (extCase.Success)
                    {
                        matchedId = extCase.Value.Id;
                    }
                }

                if (matchedId == null)
                {
                    continue;
                }

                if (uniqueExtMatch != null)
                {
                    return null;
                }

                uniqueExtMatch = matchedId;
            }

            return uniqueExtMatch;
        }

        return null;
    }

    private static string? TrySuggestNormalizedBlockLayer(WorkspaceIndex workspace, string rawLayer)
    {
        string normalized = StripLeadingSlashAndAssets(rawLayer.Trim().Replace('\\', '/'));
        int slash = normalized.LastIndexOf('/');
        string fileName = slash >= 0 ? normalized.Substring(slash + 1) : normalized;
        string stem = HasAllowedExtension(fileName, TEXTURE_EXTENSIONS)
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;

        for (int i = 0; i < NON_ALBEDO_LAYER_SUFFIXES.Length; i++)
        {
            string suffix = NON_ALBEDO_LAYER_SUFFIXES[i];
            if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                stem = stem.Substring(0, stem.Length - suffix.Length);
                break;
            }
        }

        if (workspace.WinningBlockAlbedoLayers.ContainsKey(stem))
        {
            return stem;
        }

        Result<IndexedResource> caseMatch = workspace.FindBlockAlbedoCaseInsensitive(stem);
        return caseMatch.Success ? caseMatch.Value.Id : null;
    }

    private static string? TrySuggestNormalizedLocalizationKey(WorkspaceIndex workspace, string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return null;
        }

        var builder = new StringBuilder(rawKey.Length);
        bool lastWasDot = true;
        string trimmed = rawKey.Trim();

        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (c == '/' || c == '\\' || c == ' ' || c == '.')
            {
                if (!lastWasDot)
                {
                    builder.Append('.');
                    lastWasDot = true;
                }
            }
            else
            {
                builder.Append(c);
                lastWasDot = false;
            }
        }

        string candidate = builder.ToString().TrimEnd('.');
        if (!candidate.Contains('.'))
        {
            return null;
        }

        if (workspace.HasLocalizationKey(candidate, "en"))
        {
            return candidate;
        }

        return FindLocalizationKeyCaseInsensitive(workspace, candidate, "en");
    }

    private static bool IsStrictSixDigitHexRgb(string? value)
    {
        return value != null &&
               value.Length == 7 &&
               value[0] == '#' &&
               HexColor.TryParseRgb(value, out _);
    }

    private static (string? Suggested, bool IsSafeBulkFix, string? FixDescription) TrySuggestNormalizedHexColor(string? rawColor)
    {
        if (string.IsNullOrWhiteSpace(rawColor))
        {
            return (null, false, null);
        }

        ReadOnlySpan<char> span = rawColor.AsSpan().Trim();
        if (span.Length >= 2 && span[0] == '0' && (span[1] == 'x' || span[1] == 'X'))
        {
            span = span.Slice(2);
        }
        else if (span.Length > 0 && span[0] == '#')
        {
            span = span.Slice(1);
        }

        for (int i = 0; i < span.Length; i++)
        {
            if (!HexColor.IsHexDigit(span[i]))
            {
                return (null, false, null);
            }
        }

        if (span.Length == 6)
        {
            return ($"#{span.ToString().ToUpperInvariant()}", true, "Normalize hex RGB color");
        }

        if (span.Length == 3)
        {
            char r = char.ToUpperInvariant(span[0]);
            char g = char.ToUpperInvariant(span[1]);
            char b = char.ToUpperInvariant(span[2]);
            return ($"#{r}{r}{g}{g}{b}{b}", true, "Expand 3-digit hex color to 6-digit RGB");
        }

        if (span.Length == 8)
        {
            string rgb = span.Slice(0, 6).ToString().ToUpperInvariant();
            return ($"#{rgb}", false, "Drop alpha channel to 6-digit hex RGB");
        }

        return (null, false, null);
    }

    private static string? FindBestReferenceMatch(string rawValue, IEnumerable<string> candidates)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        string trimmed = rawValue.Trim();
        List<string> uniqueCandidates = [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
            {
                uniqueCandidates.Add(candidate);
            }
        }

        if (uniqueCandidates.Count == 0)
        {
            return null;
        }

        if (!trimmed.Contains(':') && !trimmed.Contains('/') && !trimmed.Contains('\\'))
        {
            string? uniqueBareMatch = null;
            for (int i = 0; i < uniqueCandidates.Count; i++)
            {
                string bare = ExtractBareName(uniqueCandidates[i]);
                if (!string.Equals(bare, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (uniqueBareMatch != null)
                {
                    uniqueBareMatch = null;
                    break;
                }

                uniqueBareMatch = uniqueCandidates[i];
            }

            if (uniqueBareMatch != null)
            {
                return uniqueBareMatch;
            }
        }

        return FindUniqueClosestLevenshtein(trimmed, uniqueCandidates, maxDistance: 2);
    }

    private static string ExtractBareName(string identifier)
    {
        int colon = identifier.IndexOf(':');
        if (colon >= 0 && colon + 1 < identifier.Length)
        {
            return identifier.Substring(colon + 1);
        }

        int slash = identifier.LastIndexOf('/');
        return slash >= 0 && slash + 1 < identifier.Length
            ? identifier.Substring(slash + 1)
            : identifier;
    }

    private static string? FindUniqueClosestLevenshtein(string input, IReadOnlyList<string> candidates, int maxDistance)
    {
        string normalizedInput = input.ToLowerInvariant();
        int bestDistance = maxDistance + 1;
        string? bestCandidate = null;
        int bestTieCount = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            string candidate = candidates[i];
            string normalizedCandidate = candidate.ToLowerInvariant();
            if (Math.Abs(normalizedInput.Length - normalizedCandidate.Length) > maxDistance)
            {
                continue;
            }

            int dist = ComputeLevenshteinDistance(normalizedInput, normalizedCandidate);
            if (dist > maxDistance)
            {
                continue;
            }

            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestCandidate = candidate;
                bestTieCount = 1;
            }
            else if (dist == bestDistance)
            {
                bestTieCount++;
            }
        }

        return bestTieCount == 1 ? bestCandidate : null;
    }

    private static int ComputeLevenshteinDistance(string source, string target)
    {
        int n = source.Length;
        int m = target.Length;
        if (n == 0)
        {
            return m;
        }

        if (m == 0)
        {
            return n;
        }

        int[] prev = new int[m + 1];
        int[] curr = new int[m + 1];
        for (int j = 0; j <= m; j++)
        {
            prev[j] = j;
        }

        for (int i = 1; i <= n; i++)
        {
            curr[0] = i;
            char sc = source[i - 1];
            for (int j = 1; j <= m; j++)
            {
                int cost = sc == target[j - 1] ? 0 : 1;
                int delete = prev[j] + 1;
                int insert = curr[j - 1] + 1;
                int replace = prev[j - 1] + cost;
                curr[j] = Math.Min(Math.Min(delete, insert), replace);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[m];
    }

    private static IEnumerable<string> EnumerateAssetIds(WorkspaceIndex workspace, AssetKind kind)
    {
        foreach ((AssetKind Kind, string Id) key in workspace.WinningAssets.Keys)
        {
            if (key.Kind == kind && !string.IsNullOrWhiteSpace(key.Id))
            {
                yield return key.Id;
            }
        }
    }

    private static IEnumerable<string> EnumerateResourceIds(WorkspaceIndex workspace, ResourceKind kind)
    {
        foreach ((ResourceKind Kind, string Id) key in workspace.WinningResources.Keys)
        {
            if (key.Kind == kind && !string.IsNullOrWhiteSpace(key.Id))
            {
                yield return key.Id;
            }
        }
    }

    private static IEnumerable<string> EnumerateInvariantTagNames(WorkspaceIndex workspace, bool soundTagsOnly)
    {
        bool anySoundTag = false;
        if (soundTagsOnly)
        {
            for (int i = 0; i < workspace.TagEntries.Count; i++)
            {
                IndexedTagEntry entry = workspace.TagEntries[i];
                if (string.IsNullOrEmpty(entry.Locale) &&
                    string.Equals(Path.GetFileName(entry.FilePath), "sounds.toml", StringComparison.OrdinalIgnoreCase))
                {
                    anySoundTag = true;
                    yield return entry.TagName;
                }
            }
        }

        if (soundTagsOnly && anySoundTag)
        {
            yield break;
        }

        for (int i = 0; i < workspace.TagEntries.Count; i++)
        {
            IndexedTagEntry entry = workspace.TagEntries[i];
            if (string.IsNullOrEmpty(entry.Locale))
            {
                yield return entry.TagName;
            }
        }
    }

    private static bool HasAccidentalCategoryPrefix(string value, string categoryPrefix)
    {
        string normalized = StripLeadingSlashAndAssets(value.Trim().Replace('\\', '/'));
        return normalized.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripLeadingSlashAndAssets(string normalized)
    {
        string trimmed = normalized.TrimStart('/');
        if (trimmed.StartsWith("./", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(2);
        }

        return StripPrefixIgnoreCase(trimmed, "assets/");
    }

    private static string StripPrefixIgnoreCase(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value.Substring(prefix.Length)
            : value;
    }

    private static bool LooksLikeAudioFilePath(string value)
    {
        return value.Contains('/') ||
               value.Contains('\\') ||
               HasAllowedExtension(value, AUDIO_EXTENSIONS);
    }

    private static bool HasAllowedExtension(string value, string[] extensions)
    {
        for (int i = 0; i < extensions.Length; i++)
        {
            if (value.EndsWith(extensions[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
