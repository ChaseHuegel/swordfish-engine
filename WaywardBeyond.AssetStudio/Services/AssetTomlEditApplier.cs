using System.Globalization;
using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetStudio.Models;
using ToolsDocuments = WaywardBeyond.AssetTools.Documents;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Applies surgical Tomlyn CST span edits onto existing TOML documents so untouched lines and comments stay intact.
/// </summary>
internal static class AssetTomlEditApplier
{
    /// <summary>
    ///     Applies edits from <paramref name="payload"/> onto <paramref name="document"/> preserving original formatting.
    /// </summary>
    public static Result ApplyPayload(ToolsDocuments.TomlDocument document, DocumentSavePayload payload)
    {
        if (payload.HasSyntaxError || payload.IsDirectRawEdit)
        {
            document.SetRawText(payload.RawTomlText);
            return Result.FromSuccess();
        }

        if (document.HasSyntaxErrors)
        {
            document.SetRawText(payload.RawTomlText);
            return Result.FromSuccess();
        }

        return payload.Kind switch
        {
            AssetKind.Brick => ApplyBrickEdits(document, payload),
            AssetKind.Item => ApplyItemEdits(document, payload),
            AssetKind.Paint => ApplyPaintEdits(document, payload),
            AssetKind.Skill => ApplySkillEdits(document, payload),
            AssetKind.Body => ApplyBodyEdits(document, payload),
            AssetKind.Recipe => ApplyRecipeEdits(document, payload),
            AssetKind.SlotGroup => ApplySlotGroupEdits(document, payload),
            AssetKind.Bar => ApplyBarEdits(document, payload),
            AssetKind.Buff => ApplyBuffEdits(document, payload),
            AssetKind.Material => ApplyMaterialEdits(document, payload),
            AssetKind.Tag => ApplyTagEdits(document, payload),
            AssetKind.Localization => ApplyLocalizationEdits(document, payload),
            _ => Result.FromSuccess(),
        };
    }

    private static Result ApplyBrickEdits(ToolsDocuments.TomlDocument doc, DocumentSavePayload payload)
    {
        var tableIndex = ResolvePrimaryTableIndex(doc, "Bricks", payload.TableIndex, payload.EntryIndex);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "Shape", "Shape", payload, ScalarKind.String);
        ApplyScalar(doc, tableIndex, "Color", "Color", payload, ScalarKind.OptionalString);
        ApplyScalar(doc, tableIndex, "Alpha", "Alpha", payload, ScalarKind.String);
        ApplyScalar(doc, tableIndex, "CastShadows", "CastShadows", payload, ScalarKind.Boolean);
        ApplyScalar(doc, tableIndex, "Passable", "Passable", payload, ScalarKind.Boolean);
        ApplyScalar(doc, tableIndex, "Mesh", "Mesh", payload, ScalarKind.OptionalString);

        var brickOrdinal = FindArrayTableOrdinal(doc, "Bricks", tableIndex);

        var texturesTable = FindChildTableIndex(doc, tableIndex, "Bricks", "Bricks.Textures");
        if (texturesTable >= 0)
        {
            ApplyScalar(doc, texturesTable, "Connected", "Connected", payload, ScalarKind.Boolean);
            ApplySingleItemStringArray(doc, texturesTable, "Default", "TextureDefault", payload);
            ApplyOptionalSingleItemStringArray(doc, texturesTable, "Top", "TextureTop", payload);
            ApplyOptionalSingleItemStringArray(doc, texturesTable, "Bottom", "TextureBottom", payload);
            ApplyOptionalSingleItemStringArray(doc, texturesTable, "Front", "TextureFront", payload);
            ApplyOptionalSingleItemStringArray(doc, texturesTable, "Back", "TextureBack", payload);
            ApplyOptionalSingleItemStringArray(doc, texturesTable, "Left", "TextureLeft", payload);
            ApplyOptionalSingleItemStringArray(doc, texturesTable, "Right", "TextureRight", payload);
        }

        var soundsTable = FindChildTableIndex(doc, tableIndex, "Bricks", "Bricks.Sounds");
        if (soundsTable >= 0)
        {
            ApplyOptionalSingleItemStringArray(doc, soundsTable, "Place", "SoundPlace", payload);
            ApplyOptionalSingleItemStringArray(doc, soundsTable, "Break", "SoundBreak", payload);
        }

        if (payload.StatisticsModified && payload.Statistics != null)
        {
            return ApplyStatisticsEdits(doc, "Bricks", brickOrdinal, payload.Statistics);
        }

        return Result.FromSuccess();
    }

    private static Result ApplyItemEdits(ToolsDocuments.TomlDocument doc, DocumentSavePayload payload)
    {
        var tableIndex = ResolvePrimaryTableIndex(doc, "Items", payload.TableIndex, payload.EntryIndex);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "Name", "Name", payload, ScalarKind.OptionalString);
        ApplyScalar(doc, tableIndex, "Description", "Description", payload, ScalarKind.OptionalString);
        ApplyScalar(doc, tableIndex, "Icon", "Icon", payload, ScalarKind.OptionalString);
        ApplyScalar(doc, tableIndex, "MaxStack", "MaxStack", payload, ScalarKind.Integer);

        var itemOrdinal = FindArrayTableOrdinal(doc, "Items", tableIndex);
        var placeableResult = ApplyItemPlaceableEdits(doc, itemOrdinal, payload);
        if (!placeableResult.Success)
        {
            return placeableResult;
        }

        tableIndex = ResolveArrayTableByOrdinal(doc, "Items", itemOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        var toolTable = FindChildTableIndex(doc, tableIndex, "Items", "Items.Tool");
        if (toolTable >= 0)
        {
            ApplyScalar(doc, toolTable, "Type", "ToolType", payload, ScalarKind.String);
            ApplyScalar(doc, toolTable, "Target", "ToolTarget", payload, ScalarKind.OptionalString);
        }

        ApplyItemModelSectionEdits(doc, tableIndex, "Items.ViewModel", "ViewModel", payload);
        ApplyItemModelSectionEdits(doc, tableIndex, "Items.WorldModel", "WorldModel", payload);

        if (payload.StatisticsModified && payload.Statistics != null)
        {
            return ApplyStatisticsEdits(doc, "Items", itemOrdinal, payload.Statistics);
        }

        return Result.FromSuccess();
    }

    private static Result ApplyStatisticsEdits(
        ToolsDocuments.TomlDocument doc,
        string arrayTableName,
        int entryOrdinal,
        IReadOnlyList<(string Key, string Value)> statistics)
    {
        var subTableHeader = $"{arrayTableName}.Statistics";
        var ownerTableIndex = ResolveArrayTableByOrdinal(doc, arrayTableName, entryOrdinal);
        if (ownerTableIndex < 0)
        {
            return Result.FromSuccess();
        }

        var desired = new List<(string Key, double Value)>(statistics.Count);
        var desiredByKey = new Dictionary<string, double>(StringComparer.Ordinal);
        for (var i = 0; i < statistics.Count; i++)
        {
            var key = statistics[i].Key?.Trim() ?? string.Empty;
            var rawVal = statistics[i].Value?.Trim() ?? string.Empty;
            if (key.Length == 0)
            {
                return Result.FromFailure($"Cannot save [{subTableHeader}]: row #{i + 1} has an empty statistic key.");
            }

            if (!double.TryParse(rawVal, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedVal) || !double.IsFinite(parsedVal))
            {
                return Result.FromFailure($"Cannot save [{subTableHeader}]: statistic '{key}' has invalid numeric value '{rawVal}'.");
            }

            if (desiredByKey.ContainsKey(key))
            {
                return Result.FromFailure($"Cannot save [{subTableHeader}]: duplicate statistic key '{key}'.");
            }

            desired.Add((key, parsedVal));
            desiredByKey[key] = parsedVal;
        }

        var statsTableIndex = FindOwnedSubTableIndex(doc, ownerTableIndex, arrayTableName, subTableHeader);
        if (desired.Count == 0)
        {
            return statsTableIndex >= 0
                ? RemoveTableSection(doc, statsTableIndex)
                : Result.FromSuccess();
        }

        if (statsTableIndex >= 0)
        {
            var currentTable = doc.Tables[statsTableIndex];
            for (var f = currentTable.Fields.Count - 1; f >= 0; f--)
            {
                var existingField = currentTable.Fields[f];
                if (desiredByKey.ContainsKey(existingField.Key))
                {
                    continue;
                }

                var removeResult = doc.RemoveField(in existingField);
                if (!removeResult.Success)
                {
                    return removeResult;
                }

                ownerTableIndex = ResolveArrayTableByOrdinal(doc, arrayTableName, entryOrdinal);
                statsTableIndex = FindOwnedSubTableIndex(doc, ownerTableIndex, arrayTableName, subTableHeader);
                if (statsTableIndex < 0)
                {
                    break;
                }

                currentTable = doc.Tables[statsTableIndex];
            }

            for (var i = 0; i < desired.Count; i++)
            {
                ownerTableIndex = ResolveArrayTableByOrdinal(doc, arrayTableName, entryOrdinal);
                statsTableIndex = FindOwnedSubTableIndex(doc, ownerTableIndex, arrayTableName, subTableHeader);
                if (statsTableIndex < 0)
                {
                    break;
                }

                var (key, val) = desired[i];
                var existing = doc.GetFieldInTable(statsTableIndex, key);
                if (existing.Success)
                {
                    var currentDbl = existing.Value.TypedValue switch
                    {
                        double d => d,
                        long l => (double)l,
                        _ => double.NaN,
                    };
                    if (double.IsNaN(currentDbl) || Math.Abs(currentDbl - val) > 1e-6)
                    {
                        var setResult = doc.SetFloat(existing.Value, val);
                        if (!setResult.Success)
                        {
                            return setResult;
                        }
                    }
                }
                else
                {
                    var addResult = doc.AddField(doc.Tables[statsTableIndex], key, FormatTomlFloat(val));
                    if (!addResult.Success)
                    {
                        return addResult;
                    }
                }
            }

            return Result.FromSuccess();
        }

        var lastOwnedIndex = FindLastOwnedTableIndex(doc, ownerTableIndex, arrayTableName);
        var insertOffset = Math.Clamp(doc.Tables[lastOwnedIndex].ContentSpan.End, 0, doc.Text.Length);
        var eol = doc.LineEnding;
        var block = new StringBuilder($"[{subTableHeader}]");
        for (var i = 0; i < desired.Count; i++)
        {
            block.Append(eol);
            block.Append(FormatTomlKeySegment(desired[i].Key));
            block.Append(" = ");
            block.Append(FormatTomlFloat(desired[i].Value));
        }

        var before = doc.Text.AsSpan(0, insertOffset);
        var after = doc.Text.AsSpan(insertOffset);
        var leading = before.Length == 0
            || before.EndsWith("\r\n\r\n", StringComparison.Ordinal)
            || before.EndsWith("\n\n", StringComparison.Ordinal)
            ? string.Empty
            : (before.EndsWith("\n", StringComparison.Ordinal) ? eol : string.Concat(eol, eol));
        var trailing = after.Length == 0
            ? (doc.HasTrailingNewline ? eol : string.Empty)
            : (after.StartsWith("\r\n", StringComparison.Ordinal) || after.StartsWith("\n", StringComparison.Ordinal) ? eol : string.Concat(eol, eol));

        var sb = new StringBuilder(doc.Text.Length + leading.Length + block.Length + trailing.Length);
        sb.Append(before);
        sb.Append(leading);
        sb.Append(block);
        sb.Append(trailing);
        sb.Append(after);

        var candidate = sb.ToString();
        var verified = ToolsDocuments.TomlDocument.Parse(candidate, doc.SourcePath);
        if (verified.HasSyntaxErrors)
        {
            return Result.FromFailure("Applying statistic edits produced invalid TOML.");
        }

        doc.SetRawText(candidate);
        return Result.FromSuccess();
    }

    private static Result ApplyItemPlaceableEdits(
        ToolsDocuments.TomlDocument doc,
        int itemOrdinal,
        DocumentSavePayload payload)
    {
        if (payload.ModifiedFieldKeys != null
            && !payload.ModifiedFieldKeys.Contains("PlaceableType")
            && !payload.ModifiedFieldKeys.Contains("PlaceableID"))
        {
            return Result.FromSuccess();
        }

        var currentItemTable = ResolveArrayTableByOrdinal(doc, "Items", itemOrdinal);
        if (currentItemTable < 0)
        {
            return Result.FromSuccess();
        }

        payload.FieldValues.TryGetValue("PlaceableType", out var rawType);
        var placeableType = (rawType ?? string.Empty).Trim();
        var placeableTable = FindChildTableIndex(doc, currentItemTable, "Items", "Items.Placeable");

        if (string.IsNullOrEmpty(placeableType) || string.Equals(placeableType, "none", StringComparison.OrdinalIgnoreCase))
        {
            if (placeableTable >= 0)
            {
                return RemoveTableSection(doc, placeableTable);
            }

            return Result.FromSuccess();
        }

        if (placeableTable < 0)
        {
            var insertResult = InsertItemChildTable(doc, currentItemTable, "Items.Placeable");
            if (!insertResult.Success)
            {
                return insertResult;
            }

            currentItemTable = ResolveArrayTableByOrdinal(doc, "Items", itemOrdinal);
            if (currentItemTable < 0)
            {
                return Result.FromSuccess();
            }

            placeableTable = FindChildTableIndex(doc, currentItemTable, "Items", "Items.Placeable");
            if (placeableTable < 0)
            {
                return Result.FromSuccess();
            }

            //  When creating a new [Items.Placeable] table, write both Type and ID even if only one field changed.
            ApplyScalarUnconditional(doc, placeableTable, "Type", placeableType, ScalarKind.String);
            placeableTable = RelocateItemChildTable(doc, itemOrdinal, "Items.Placeable");
            if (placeableTable >= 0 && payload.FieldValues.TryGetValue("PlaceableID", out var initialId) && !string.IsNullOrWhiteSpace(initialId))
            {
                ApplyScalarUnconditional(doc, placeableTable, "ID", initialId, ScalarKind.OptionalString);
            }

            return Result.FromSuccess();
        }

        ApplyScalar(doc, placeableTable, "Type", "PlaceableType", payload, ScalarKind.String);
        placeableTable = RelocateItemChildTable(doc, itemOrdinal, "Items.Placeable");
        if (placeableTable >= 0)
        {
            ApplyScalar(doc, placeableTable, "ID", "PlaceableID", payload, ScalarKind.OptionalString);
        }

        return Result.FromSuccess();
    }

    private static void ApplyScalarUnconditional(
        ToolsDocuments.TomlDocument doc,
        int tableIndex,
        string key,
        string value,
        ScalarKind kind)
    {
        var existing = doc.GetFieldInTable(tableIndex, key);
        if (kind == ScalarKind.OptionalString && string.IsNullOrWhiteSpace(value))
        {
            if (existing.Success)
            {
                doc.RemoveField(existing.Value);
            }

            return;
        }

        if (existing.Success)
        {
            doc.SetString(existing.Value, value);
        }
        else if (tableIndex >= 0 && tableIndex < doc.Tables.Count)
        {
            doc.AddField(doc.Tables[tableIndex], key, FormatTomlString(value));
        }
    }

    private static void ApplyItemModelSectionEdits(
        ToolsDocuments.TomlDocument doc,
        int parentTableIndex,
        string childHeaderPath,
        string fieldPrefix,
        DocumentSavePayload payload)
    {
        var meshKey = $"{fieldPrefix}Mesh";
        var matKey = $"{fieldPrefix}Material";
        var posKey = $"{fieldPrefix}Position";
        var rotKey = $"{fieldPrefix}Rotation";
        var scaleKey = $"{fieldPrefix}Scale";

        if (payload.ModifiedFieldKeys != null
            && !payload.ModifiedFieldKeys.Contains(meshKey)
            && !payload.ModifiedFieldKeys.Contains(matKey)
            && !payload.ModifiedFieldKeys.Contains(posKey)
            && !payload.ModifiedFieldKeys.Contains(rotKey)
            && !payload.ModifiedFieldKeys.Contains(scaleKey))
        {
            return;
        }

        var itemOrdinal = FindArrayTableOrdinal(doc, "Items", parentTableIndex);
        var currentItemTable = ResolveArrayTableByOrdinal(doc, "Items", itemOrdinal);
        if (currentItemTable < 0)
        {
            return;
        }

        var childTable = FindChildTableIndex(doc, currentItemTable, "Items", childHeaderPath);
        if (childTable < 0)
        {
            var insertResult = InsertItemChildTable(doc, currentItemTable, childHeaderPath);
            if (!insertResult.Success)
            {
                return;
            }

            currentItemTable = ResolveArrayTableByOrdinal(doc, "Items", itemOrdinal);
            if (currentItemTable < 0)
            {
                return;
            }

            childTable = FindChildTableIndex(doc, currentItemTable, "Items", childHeaderPath);
            if (childTable < 0)
            {
                return;
            }
        }

        ApplyScalar(doc, childTable, "Mesh", meshKey, payload, ScalarKind.OptionalString);
        childTable = RelocateItemChildTable(doc, itemOrdinal, childHeaderPath);
        if (childTable < 0)
        {
            return;
        }

        ApplyScalar(doc, childTable, "Material", matKey, payload, ScalarKind.OptionalString);
        ApplyItemVector3Edit(doc, itemOrdinal, childHeaderPath, "Position", posKey, payload);
        ApplyItemVector3Edit(doc, itemOrdinal, childHeaderPath, "Rotation", rotKey, payload);
        ApplyItemVector3Edit(doc, itemOrdinal, childHeaderPath, "Scale", scaleKey, payload);
    }

    private static int RelocateItemChildTable(
        ToolsDocuments.TomlDocument doc,
        int itemOrdinal,
        string childHeaderPath)
    {
        var itemTable = ResolveArrayTableByOrdinal(doc, "Items", itemOrdinal);
        return itemTable >= 0
            ? FindChildTableIndex(doc, itemTable, "Items", childHeaderPath)
            : -1;
    }

    private static void ApplyItemVector3Edit(
        ToolsDocuments.TomlDocument doc,
        int itemOrdinal,
        string childHeaderPath,
        string vectorName,
        string fieldKey,
        DocumentSavePayload payload)
    {
        if (payload.ModifiedFieldKeys != null && !payload.ModifiedFieldKeys.Contains(fieldKey))
        {
            return;
        }

        if (!payload.FieldValues.TryGetValue(fieldKey, out var rawVec) || string.IsNullOrWhiteSpace(rawVec))
        {
            return;
        }

        var parts = rawVec.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < 3
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var vx)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var vy)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var vz)
            || !double.IsFinite(vx)
            || !double.IsFinite(vy)
            || !double.IsFinite(vz))
        {
            return;
        }

        vx = NormalizeZero(Math.Round(vx, 4, MidpointRounding.AwayFromZero));
        vy = NormalizeZero(Math.Round(vy, 4, MidpointRounding.AwayFromZero));
        vz = NormalizeZero(Math.Round(vz, 4, MidpointRounding.AwayFromZero));

        var itemTable = ResolveArrayTableByOrdinal(doc, "Items", itemOrdinal);
        if (itemTable < 0)
        {
            return;
        }

        var childTable = FindChildTableIndex(doc, itemTable, "Items", childHeaderPath);
        if (childTable < 0)
        {
            return;
        }

        // 1. Preserve inline table layout when authored as Vector = { X = ..., Y = ..., Z = ... }.
        var inlineField = doc.GetFieldInTable(childTable, vectorName);
        if (inlineField.Success)
        {
            var inlineExpr = string.Create(
                CultureInfo.InvariantCulture,
                $"{{ X = {FormatTomlFloat(vx)}, Y = {FormatTomlFloat(vy)}, Z = {FormatTomlFloat(vz)} }}");
            if (!string.Equals(inlineField.Value.RawValue.Trim(), inlineExpr, StringComparison.Ordinal))
            {
                doc.SetRawValue(inlineField.Value, inlineExpr);
            }

            return;
        }

        // 2. Preserve explicit nested sub-table [Items.ViewModel.Position] when already present.
        var nestedSubTable = FindChildTableIndex(doc, itemTable, "Items", $"{childHeaderPath}.{vectorName}");
        if (nestedSubTable >= 0)
        {
            UpsertAxisFloat(doc, itemOrdinal, $"{childHeaderPath}.{vectorName}", null, "X", vx);
            UpsertAxisFloat(doc, itemOrdinal, $"{childHeaderPath}.{vectorName}", null, "Y", vy);
            UpsertAxisFloat(doc, itemOrdinal, $"{childHeaderPath}.{vectorName}", null, "Z", vz);
            return;
        }

        // 3. Standard dotted keys (Position.X / Position.Y / Position.Z).
        UpsertAxisFloat(doc, itemOrdinal, childHeaderPath, vectorName, "X", vx);
        UpsertAxisFloat(doc, itemOrdinal, childHeaderPath, vectorName, "Y", vy);
        UpsertAxisFloat(doc, itemOrdinal, childHeaderPath, vectorName, "Z", vz);
    }

    private static void UpsertAxisFloat(
        ToolsDocuments.TomlDocument doc,
        int itemOrdinal,
        string tableHeaderPath,
        string? dottedPrefix,
        string axis,
        double value)
    {
        var tableIndex = RelocateItemChildTable(doc, itemOrdinal, tableHeaderPath);
        if (tableIndex < 0)
        {
            return;
        }

        var logicalKey = string.IsNullOrEmpty(dottedPrefix) ? axis : $"{dottedPrefix}.{axis}";
        var existing = doc.GetFieldInTable(tableIndex, logicalKey);
        if (existing.Success)
        {
            var currentDbl = existing.Value.TypedValue switch
            {
                double d => d,
                long l => (double)l,
                _ => double.NaN,
            };
            if (double.IsNaN(currentDbl) || Math.Abs(currentDbl - value) > 1e-6)
            {
                doc.SetFloat(existing.Value, value);
            }

            return;
        }

        string[] segments = string.IsNullOrEmpty(dottedPrefix) ? [axis] : [dottedPrefix, axis];
        doc.AddField(doc.Tables[tableIndex], segments, FormatTomlFloat(value));
    }

    private static Result InsertItemChildTable(
        ToolsDocuments.TomlDocument doc,
        int itemTableIndex,
        string childHeaderPath)
    {
        var lastOwnedIndex = FindLastOwnedTableIndex(doc, itemTableIndex, "Items");
        var insertOffset = Math.Clamp(doc.Tables[lastOwnedIndex].ContentSpan.End, 0, doc.Text.Length);
        var eol = doc.LineEnding;
        var before = doc.Text.AsSpan(0, insertOffset);
        var after = doc.Text.AsSpan(insertOffset);

        string leading;
        if (before.Length == 0 || before.EndsWith("\n", StringComparison.Ordinal))
        {
            leading = string.Empty;
        }
        else
        {
            leading = eol;
        }

        string trailing;
        if (after.Length == 0)
        {
            trailing = doc.HasTrailingNewline ? eol : string.Empty;
        }
        else if (after.StartsWith("\r\n", StringComparison.Ordinal) || after.StartsWith("\n", StringComparison.Ordinal))
        {
            trailing = string.Empty;
        }
        else
        {
            trailing = eol;
        }

        var block = $"[{childHeaderPath}]";
        var sb = new StringBuilder(doc.Text.Length + leading.Length + block.Length + trailing.Length);
        sb.Append(before);
        sb.Append(leading);
        sb.Append(block);
        sb.Append(trailing);
        sb.Append(after);

        var candidate = sb.ToString();
        var parsed = ToolsDocuments.TomlDocument.Parse(candidate, doc.SourcePath);
        if (parsed.HasSyntaxErrors)
        {
            var err = parsed.Diagnostics.Count > 0 ? parsed.Diagnostics[0].Message : $"Inserting [{childHeaderPath}] produced invalid TOML.";
            return Result.FromFailure(err);
        }

        doc.SetRawText(candidate);
        return Result.FromSuccess();
    }

    private static double NormalizeZero(double value) => Math.Abs(value) < 1e-6 ? 0d : value;

    private static string FormatTomlFloat(double value)
    {
        var clean = NormalizeZero(value);
        return clean.ToString("0.0######", CultureInfo.InvariantCulture);
    }

    private static Result ApplyPaintEdits(ToolsDocuments.TomlDocument doc, DocumentSavePayload payload)
    {
        var tableIndex = ResolvePrimaryTableIndex(doc, "Paints", payload.TableIndex, payload.EntryIndex);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "Name", "Name", payload, ScalarKind.OptionalString);
        ApplyScalar(doc, tableIndex, "Color", "Color", payload, ScalarKind.OptionalString);
        ApplyScalar(doc, tableIndex, "Alpha", "Alpha", payload, ScalarKind.String);
        ApplyScalar(doc, tableIndex, "Connected", "Connected", payload, ScalarKind.Boolean);
        ApplyCsvStringArray(doc, tableIndex, "Textures", "Variants", payload);
        return Result.FromSuccess();
    }

    private static Result ApplyRecipeEdits(ToolsDocuments.TomlDocument doc, DocumentSavePayload payload)
    {
        var tableIndex = ResolvePrimaryTableIndex(doc, "Recipes", payload.TableIndex, payload.EntryIndex);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        var recipeOrdinal = FindArrayTableOrdinal(doc, "Recipes", tableIndex);

        ApplyScalar(doc, tableIndex, "TargetTag", "TargetTag", payload, ScalarKind.String);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Recipes", recipeOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "Mode", "Mode", payload, ScalarKind.String);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Recipes", recipeOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "DurationSeconds", "DurationSeconds", payload, ScalarKind.Float);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Recipes", recipeOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "PowerKw", "PowerKw", payload, ScalarKind.Float);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Recipes", recipeOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "Category", "Category", payload, ScalarKind.OptionalString);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Recipes", recipeOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "Priority", "Priority", payload, ScalarKind.Integer);

        if (payload.RecipeInputsModified && payload.RecipeInputs != null)
        {
            var inputsBlock = BuildRecipeInputsTomlBlock(payload.RecipeInputs, doc.LineEnding);
            var inputsResult = ReplaceOwnedRecipeSubTables(doc, recipeOrdinal, "Inputs", inputsBlock, insertBeforeOutputs: true);
            if (!inputsResult.Success)
            {
                return inputsResult;
            }
        }

        if (payload.RecipeOutputsModified && payload.RecipeOutputs != null)
        {
            var outputsBlock = BuildRecipeOutputsTomlBlock(payload.RecipeOutputs, doc.LineEnding);
            var outputsResult = ReplaceOwnedRecipeSubTables(doc, recipeOrdinal, "Outputs", outputsBlock, insertBeforeOutputs: false);
            if (!outputsResult.Success)
            {
                return outputsResult;
            }
        }

        return Result.FromSuccess();
    }

    private static Result ReplaceOwnedRecipeSubTables(
        ToolsDocuments.TomlDocument doc,
        int recipeOrdinal,
        string subTableSegment,
        string replacementBlock,
        bool insertBeforeOutputs)
    {
        var recipeTableIndex = ResolveArrayTableByOrdinal(doc, "Recipes", recipeOrdinal);
        if (recipeTableIndex < 0)
        {
            return Result.FromSuccess();
        }

        var lastOwned = FindLastOwnedTableIndex(doc, recipeTableIndex, "Recipes");
        var targetSuffix = "." + subTableSegment;

        //  Remove existing matching owned sub-tables from last to first so offsets stay valid.
        for (var i = lastOwned; i > recipeTableIndex; i--)
        {
            var table = doc.Tables[i];
            if (table.HeaderPath.EndsWith(targetSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var removeResult = RemoveTableSection(doc, i);
                if (!removeResult.Success)
                {
                    return removeResult;
                }
            }
        }

        if (string.IsNullOrEmpty(replacementBlock))
        {
            return Result.FromSuccess();
        }

        recipeTableIndex = ResolveArrayTableByOrdinal(doc, "Recipes", recipeOrdinal);
        if (recipeTableIndex < 0)
        {
            return Result.FromSuccess();
        }

        lastOwned = FindLastOwnedTableIndex(doc, recipeTableIndex, "Recipes");
        var insertOffset = Math.Clamp(doc.Tables[lastOwned].ContentSpan.End, 0, doc.Text.Length);

        if (insertBeforeOutputs)
        {
            for (var i = recipeTableIndex + 1; i <= lastOwned; i++)
            {
                if (doc.Tables[i].HeaderPath.EndsWith(".Outputs", StringComparison.OrdinalIgnoreCase))
                {
                    insertOffset = Math.Clamp(doc.Tables[i].HeaderLineSpan.Start, 0, doc.Text.Length);
                    break;
                }
            }
        }

        return InsertRecipeTableBlockAtOffset(doc, insertOffset, replacementBlock);
    }

    private static Result InsertRecipeTableBlockAtOffset(
        ToolsDocuments.TomlDocument doc,
        int insertOffset,
        string block)
    {
        var eol = doc.LineEnding;
        var before = doc.Text.AsSpan(0, insertOffset);
        var after = doc.Text.AsSpan(insertOffset);

        string leading;
        if (before.Length == 0)
        {
            leading = string.Empty;
        }
        else if (before.EndsWith("\r\n\r\n", StringComparison.Ordinal) || before.EndsWith("\n\n", StringComparison.Ordinal))
        {
            leading = string.Empty;
        }
        else if (before.EndsWith("\n", StringComparison.Ordinal))
        {
            leading = eol;
        }
        else
        {
            leading = string.Concat(eol, eol);
        }

        string trailing;
        if (after.Length == 0)
        {
            trailing = doc.HasTrailingNewline ? eol : string.Empty;
        }
        else if (after.StartsWith("\r\n\r\n", StringComparison.Ordinal) || after.StartsWith("\n\n", StringComparison.Ordinal))
        {
            trailing = string.Empty;
        }
        else if (after.StartsWith("\r\n", StringComparison.Ordinal) || after.StartsWith("\n", StringComparison.Ordinal))
        {
            trailing = eol;
        }
        else
        {
            trailing = string.Concat(eol, eol);
        }

        var sb = new StringBuilder(doc.Text.Length + leading.Length + block.Length + trailing.Length);
        sb.Append(before);
        sb.Append(leading);
        sb.Append(block);
        sb.Append(trailing);
        sb.Append(after);
        var candidate = sb.ToString();
        var parsed = ToolsDocuments.TomlDocument.Parse(candidate, doc.SourcePath);
        if (parsed.HasSyntaxErrors)
        {
            var err = parsed.Diagnostics.Count > 0
                ? parsed.Diagnostics[0].Message
                : "Updating recipe sub-tables produced invalid TOML.";
            return Result.FromFailure(err);
        }

        doc.SetRawText(candidate);
        return Result.FromSuccess();
    }

    private static string BuildRecipeInputsTomlBlock(
        IReadOnlyList<(string ItemId, int Count, string SlotGroup)> inputs,
        string eol)
    {
        if (inputs.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < inputs.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(eol);
                sb.Append(eol);
            }

            var item = inputs[i];
            var count = Math.Max(1, item.Count);
            sb.Append("[[Recipes.Inputs]]").Append(eol);
            sb.Append("ItemId = ").Append(FormatTomlString(item.ItemId?.Trim() ?? string.Empty)).Append(eol);
            sb.Append("Count  = ").Append(count.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(item.SlotGroup))
            {
                sb.Append(eol);
                sb.Append("SlotGroup = ").Append(FormatTomlString(item.SlotGroup.Trim()));
            }
        }

        return sb.ToString();
    }

    private static string BuildRecipeOutputsTomlBlock(
        IReadOnlyList<(string ItemId, int Count, string Chance)> outputs,
        string eol)
    {
        if (outputs.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < outputs.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(eol);
                sb.Append(eol);
            }

            var item = outputs[i];
            var count = Math.Max(1, item.Count);
            sb.Append("[[Recipes.Outputs]]").Append(eol);
            sb.Append("ItemId = ").Append(FormatTomlString(item.ItemId?.Trim() ?? string.Empty)).Append(eol);
            sb.Append("Count  = ").Append(count.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(item.Chance)
                && double.TryParse(item.Chance.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var chance)
                && double.IsFinite(chance))
            {
                sb.Append(eol);
                sb.Append("Chance = ").Append(FormatTomlFloat(chance));
            }
        }

        return sb.ToString();
    }

    private static Result ApplySlotGroupEdits(ToolsDocuments.TomlDocument doc, DocumentSavePayload payload)
    {
        var tableIndex = ResolvePrimaryTableIndex(doc, "SlotGroups", payload.TableIndex, payload.EntryIndex);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        var slotGroupOrdinal = FindArrayTableOrdinal(doc, "SlotGroups", tableIndex);

        ApplyScalar(doc, tableIndex, "Capacity", "Capacity", payload, ScalarKind.Integer);
        tableIndex = ResolveArrayTableByOrdinal(doc, "SlotGroups", slotGroupOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        if (payload.SlotGroupAllowedTagsModified && payload.SlotGroupAllowedTags != null)
        {
            var cleanedTags = CleanStringList(payload.SlotGroupAllowedTags);
            var existingField = doc.GetFieldInTable(tableIndex, "AllowedTags");
            if (cleanedTags.Count == 0)
            {
                if (existingField.Success)
                {
                    var removeResult = doc.RemoveField(existingField.Value);
                    if (!removeResult.Success)
                    {
                        return removeResult;
                    }
                }
            }
            else
            {
                var arrayExpr = FormatTomlStringArray(cleanedTags);
                if (existingField.Success)
                {
                    var setResult = doc.SetRawValue(existingField.Value, arrayExpr);
                    if (!setResult.Success)
                    {
                        return setResult;
                    }
                }
                else
                {
                    var addResult = doc.AddField(doc.Tables[tableIndex], "AllowedTags", arrayExpr);
                    if (!addResult.Success)
                    {
                        return addResult;
                    }
                }
            }
        }

        if (payload.SlotGroupSlotsModified && payload.SlotGroupSlots != null)
        {
            var slotsBlock = BuildSlotGroupSlotsTomlBlock(payload.SlotGroupSlots, doc.LineEnding);
            var slotsResult = ReplaceOwnedSlotGroupSubTables(doc, slotGroupOrdinal, slotsBlock);
            if (!slotsResult.Success)
            {
                return slotsResult;
            }
        }

        return Result.FromSuccess();
    }

    private static Result ReplaceOwnedSlotGroupSubTables(
        ToolsDocuments.TomlDocument doc,
        int slotGroupOrdinal,
        string replacementBlock)
    {
        var ownerTableIndex = ResolveArrayTableByOrdinal(doc, "SlotGroups", slotGroupOrdinal);
        if (ownerTableIndex < 0)
        {
            return Result.FromSuccess();
        }

        var lastOwned = FindLastOwnedTableIndex(doc, ownerTableIndex, "SlotGroups");

        //  Remove existing owned [[SlotGroups.Slots]] tables from last to first so spans stay valid.
        for (var i = lastOwned; i > ownerTableIndex; i--)
        {
            var table = doc.Tables[i];
            if (table.HeaderPath.EndsWith(".Slots", StringComparison.OrdinalIgnoreCase))
            {
                var removeResult = RemoveTableSection(doc, i);
                if (!removeResult.Success)
                {
                    return removeResult;
                }
            }
        }

        if (string.IsNullOrEmpty(replacementBlock))
        {
            return Result.FromSuccess();
        }

        ownerTableIndex = ResolveArrayTableByOrdinal(doc, "SlotGroups", slotGroupOrdinal);
        if (ownerTableIndex < 0)
        {
            return Result.FromSuccess();
        }

        lastOwned = FindLastOwnedTableIndex(doc, ownerTableIndex, "SlotGroups");
        var insertOffset = Math.Clamp(doc.Tables[lastOwned].ContentSpan.End, 0, doc.Text.Length);
        return InsertRecipeTableBlockAtOffset(doc, insertOffset, replacementBlock);
    }

    private static string BuildSlotGroupSlotsTomlBlock(
        IReadOnlyList<(int Index, IReadOnlyList<string> AllowedTags)> slots,
        string eol)
    {
        if (slots.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < slots.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(eol);
                sb.Append(eol);
            }

            var slot = slots[i];
            var cleanedTags = CleanStringList(slot.AllowedTags);
            sb.Append("[[SlotGroups.Slots]]").Append(eol);
            sb.Append("Index = ").Append(slot.Index.ToString(CultureInfo.InvariantCulture)).Append(eol);
            sb.Append("AllowedTags = ").Append(FormatTomlStringArray(cleanedTags));
        }

        return sb.ToString();
    }

    private static Result ApplyBarEdits(ToolsDocuments.TomlDocument doc, DocumentSavePayload payload)
    {
        var tableIndex = ResolvePrimaryTableIndex(doc, "Bars", payload.TableIndex, payload.EntryIndex);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        var barOrdinal = FindArrayTableOrdinal(doc, "Bars", tableIndex);

        ApplyScalar(doc, tableIndex, "StatId", "StatId", payload, ScalarKind.String);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Bars", barOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "MaxStatId", "MaxStatId", payload, ScalarKind.OptionalString);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Bars", barOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyOptionalFloatScalar(doc, tableIndex, "DefaultMax", "DefaultMax", payload);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Bars", barOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyOptionalIntegerScalar(doc, tableIndex, "IconCount", "IconCount", payload);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Bars", barOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "IconFull", "IconFull", payload, ScalarKind.OptionalString);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Bars", barOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "IconHalf", "IconHalf", payload, ScalarKind.OptionalString);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Bars", barOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "IconEmpty", "IconEmpty", payload, ScalarKind.OptionalString);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Bars", barOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyOptionalFloatScalar(doc, tableIndex, "WarnThreshold", "WarnThreshold", payload);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Bars", barOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyOptionalFloatScalar(doc, tableIndex, "CriticalThreshold", "CriticalThreshold", payload);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Bars", barOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "Order", "Order", payload, ScalarKind.Integer);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Bars", barOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "HideInCreative", "HideInCreative", payload, ScalarKind.Boolean);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Bars", barOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "ColorTint", "ColorTint", payload, ScalarKind.OptionalString);
        return Result.FromSuccess();
    }

    private static Result ApplyBuffEdits(ToolsDocuments.TomlDocument doc, DocumentSavePayload payload)
    {
        var tableIndex = ResolvePrimaryTableIndex(doc, "Buffs", payload.TableIndex, payload.EntryIndex);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        var buffOrdinal = FindArrayTableOrdinal(doc, "Buffs", tableIndex);

        ApplyScalar(doc, tableIndex, "Name", "Name", payload, ScalarKind.OptionalString);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Buffs", buffOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "Description", "Description", payload, ScalarKind.OptionalString);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Buffs", buffOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "Icon", "Icon", payload, ScalarKind.OptionalString);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Buffs", buffOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "DurationSeconds", "DurationSeconds", payload, ScalarKind.Float);
        tableIndex = ResolveArrayTableByOrdinal(doc, "Buffs", buffOrdinal);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        ApplyScalar(doc, tableIndex, "StackPolicy", "StackPolicy", payload, ScalarKind.String);

        if (payload.BuffStatModifiersModified && payload.BuffStatModifiers != null)
        {
            var modsBlock = BuildBuffStatModifiersTomlBlock(payload.BuffStatModifiers, doc.LineEnding);
            var modsResult = ReplaceOwnedBuffSubTables(doc, buffOrdinal, "StatModifiers", modsBlock, insertBeforeRegen: true);
            if (!modsResult.Success)
            {
                return modsResult;
            }
        }

        if (payload.BuffRegenRatesModified && payload.BuffRegenRates != null)
        {
            var regenBlock = BuildBuffRegenRatesTomlBlock(payload.BuffRegenRates, doc.LineEnding);
            var regenResult = ReplaceOwnedBuffSubTables(doc, buffOrdinal, "RegenRates", regenBlock, insertBeforeRegen: false);
            if (!regenResult.Success)
            {
                return regenResult;
            }
        }

        return Result.FromSuccess();
    }

    private static Result ReplaceOwnedBuffSubTables(
        ToolsDocuments.TomlDocument doc,
        int buffOrdinal,
        string subTableSegment,
        string replacementBlock,
        bool insertBeforeRegen)
    {
        var ownerTableIndex = ResolveArrayTableByOrdinal(doc, "Buffs", buffOrdinal);
        if (ownerTableIndex < 0)
        {
            return Result.FromSuccess();
        }

        var lastOwned = FindLastOwnedTableIndex(doc, ownerTableIndex, "Buffs");
        var targetSuffix = "." + subTableSegment;

        //  Remove existing matching owned sub-tables from last to first so spans stay valid.
        for (var i = lastOwned; i > ownerTableIndex; i--)
        {
            var table = doc.Tables[i];
            if (table.HeaderPath.EndsWith(targetSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var removeResult = RemoveTableSection(doc, i);
                if (!removeResult.Success)
                {
                    return removeResult;
                }
            }
        }

        if (string.IsNullOrEmpty(replacementBlock))
        {
            return Result.FromSuccess();
        }

        ownerTableIndex = ResolveArrayTableByOrdinal(doc, "Buffs", buffOrdinal);
        if (ownerTableIndex < 0)
        {
            return Result.FromSuccess();
        }

        lastOwned = FindLastOwnedTableIndex(doc, ownerTableIndex, "Buffs");
        var insertOffset = Math.Clamp(doc.Tables[lastOwned].ContentSpan.End, 0, doc.Text.Length);

        if (insertBeforeRegen)
        {
            for (var i = ownerTableIndex + 1; i <= lastOwned; i++)
            {
                if (doc.Tables[i].HeaderPath.EndsWith(".RegenRates", StringComparison.OrdinalIgnoreCase))
                {
                    insertOffset = Math.Clamp(doc.Tables[i].HeaderLineSpan.Start, 0, doc.Text.Length);
                    break;
                }
            }
        }

        return InsertRecipeTableBlockAtOffset(doc, insertOffset, replacementBlock);
    }

    private static string BuildBuffStatModifiersTomlBlock(
        IReadOnlyList<(string StatId, string FlatDelta, string PercentDelta)> modifiers,
        string eol)
    {
        if (modifiers.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(eol);
                sb.Append(eol);
            }

            var mod = modifiers[i];
            sb.Append("[[Buffs.StatModifiers]]").Append(eol);
            sb.Append("StatId = ").Append(FormatTomlString(mod.StatId?.Trim() ?? string.Empty));
            if (!string.IsNullOrWhiteSpace(mod.FlatDelta)
                && double.TryParse(mod.FlatDelta.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var flat)
                && double.IsFinite(flat))
            {
                sb.Append(eol);
                sb.Append("FlatDelta = ").Append(FormatTomlFloat(flat));
            }

            if (!string.IsNullOrWhiteSpace(mod.PercentDelta)
                && double.TryParse(mod.PercentDelta.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var pct)
                && double.IsFinite(pct))
            {
                sb.Append(eol);
                sb.Append("PercentDelta = ").Append(FormatTomlFloat(pct));
            }
        }

        return sb.ToString();
    }

    private static string BuildBuffRegenRatesTomlBlock(
        IReadOnlyList<(string StatId, string PerSecond)> regenRates,
        string eol)
    {
        if (regenRates.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < regenRates.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(eol);
                sb.Append(eol);
            }

            var regen = regenRates[i];
            sb.Append("[[Buffs.RegenRates]]").Append(eol);
            sb.Append("StatId = ").Append(FormatTomlString(regen.StatId?.Trim() ?? string.Empty)).Append(eol);
            var perSec = double.TryParse(regen.PerSecond?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                && double.IsFinite(parsed)
                ? parsed
                : 0d;
            sb.Append("PerSecond = ").Append(FormatTomlFloat(perSec));
        }

        return sb.ToString();
    }

    private static void ApplyOptionalFloatScalar(
        ToolsDocuments.TomlDocument doc,
        int tableIndex,
        string tomlKey,
        string fieldKey,
        DocumentSavePayload payload)
    {
        if (payload.ModifiedFieldKeys != null && !payload.ModifiedFieldKeys.Contains(fieldKey))
        {
            return;
        }

        if (!payload.FieldValues.TryGetValue(fieldKey, out var value))
        {
            return;
        }

        var trimmed = value?.Trim() ?? string.Empty;
        var existing = doc.GetFieldInTable(tableIndex, tomlKey);
        if (trimmed.Length == 0)
        {
            if (existing.Success)
            {
                doc.RemoveField(existing.Value);
            }

            return;
        }

        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed))
        {
            return;
        }

        if (existing.Success)
        {
            doc.SetFloat(existing.Value, parsed);
        }
        else if (tableIndex >= 0 && tableIndex < doc.Tables.Count)
        {
            doc.AddField(doc.Tables[tableIndex], tomlKey, FormatTomlFloat(parsed));
        }
    }

    private static void ApplyOptionalIntegerScalar(
        ToolsDocuments.TomlDocument doc,
        int tableIndex,
        string tomlKey,
        string fieldKey,
        DocumentSavePayload payload)
    {
        if (payload.ModifiedFieldKeys != null && !payload.ModifiedFieldKeys.Contains(fieldKey))
        {
            return;
        }

        if (!payload.FieldValues.TryGetValue(fieldKey, out var value))
        {
            return;
        }

        var trimmed = value?.Trim() ?? string.Empty;
        var existing = doc.GetFieldInTable(tableIndex, tomlKey);
        if (trimmed.Length == 0)
        {
            if (existing.Success)
            {
                doc.RemoveField(existing.Value);
            }

            return;
        }

        if (!long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return;
        }

        if (existing.Success)
        {
            doc.SetInteger(existing.Value, parsed);
        }
        else if (tableIndex >= 0 && tableIndex < doc.Tables.Count)
        {
            doc.AddField(doc.Tables[tableIndex], tomlKey, parsed.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static List<string> CleanStringList(IReadOnlyList<string>? values)
    {
        var cleaned = new List<string>();
        if (values == null)
        {
            return cleaned;
        }

        for (var i = 0; i < values.Count; i++)
        {
            var trimmed = values[i]?.Trim() ?? string.Empty;
            if (trimmed.Length > 0)
            {
                cleaned.Add(trimmed);
            }
        }

        return cleaned;
    }

    private static Result ApplySkillEdits(ToolsDocuments.TomlDocument doc, DocumentSavePayload payload)
    {
        var tableIndex = ResolvePrimaryTableIndex(doc, "Skills", payload.TableIndex, payload.EntryIndex);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        var skillOrdinal = FindArrayTableOrdinal(doc, "Skills", tableIndex);
        ApplyScalar(doc, tableIndex, "Name", "Name", payload, ScalarKind.OptionalString);
        ApplyScalar(doc, tableIndex, "Category", "Category", payload, ScalarKind.OptionalString);
        ApplyScalar(doc, tableIndex, "Icon", "Icon", payload, ScalarKind.OptionalString);

        if (!payload.SkillLevelsModified || payload.SkillLevels == null)
        {
            return Result.FromSuccess();
        }

        return ApplySkillLevelEdits(doc, skillOrdinal, payload.SkillLevels);
    }

    private static Result ApplySkillLevelEdits(
        ToolsDocuments.TomlDocument doc,
        int skillOrdinal,
        IReadOnlyList<(int Level, int XpRequired)> levels)
    {
        var skillTableIndex = ResolveArrayTableByOrdinal(doc, "Skills", skillOrdinal);
        if (skillTableIndex < 0)
        {
            return Result.FromSuccess();
        }

        var desired = new List<(int Level, int Xp)>(levels.Count);
        for (var i = 0; i < levels.Count; i++)
        {
            var level = levels[i].Level > 0 ? levels[i].Level : i + 1;
            var xp = Math.Max(1, levels[i].XpRequired);
            desired.Add((level, xp));
        }

        var levelsTableIndex = FindOwnedSubTableIndex(doc, skillTableIndex, "Skills", "Skills.Levels");
        if (levelsTableIndex >= 0)
        {
            var desiredByKey = new Dictionary<string, int>(desired.Count, StringComparer.Ordinal);
            for (var i = 0; i < desired.Count; i++)
            {
                desiredByKey[desired[i].Level.ToString(CultureInfo.InvariantCulture)] = desired[i].Xp;
            }

            var currentTable = doc.Tables[levelsTableIndex];
            for (var f = currentTable.Fields.Count - 1; f >= 0; f--)
            {
                var existingField = currentTable.Fields[f];
                if (desiredByKey.ContainsKey(existingField.Key))
                {
                    continue;
                }

                var removeResult = doc.RemoveField(in existingField);
                if (!removeResult.Success)
                {
                    return removeResult;
                }

                skillTableIndex = ResolveArrayTableByOrdinal(doc, "Skills", skillOrdinal);
                levelsTableIndex = FindOwnedSubTableIndex(doc, skillTableIndex, "Skills", "Skills.Levels");
                if (levelsTableIndex < 0)
                {
                    break;
                }

                currentTable = doc.Tables[levelsTableIndex];
            }

            for (var i = 0; i < desired.Count; i++)
            {
                skillTableIndex = ResolveArrayTableByOrdinal(doc, "Skills", skillOrdinal);
                levelsTableIndex = FindOwnedSubTableIndex(doc, skillTableIndex, "Skills", "Skills.Levels");
                if (levelsTableIndex < 0)
                {
                    break;
                }

                var key = desired[i].Level.ToString(CultureInfo.InvariantCulture);
                var xp = desired[i].Xp;
                var existing = FindFieldInTableCaseInsensitive(doc, levelsTableIndex, key);
                if (existing.HasValue)
                {
                    if (existing.Value.TypedValue is not long curXp || curXp != xp)
                    {
                        var setResult = doc.SetInteger(existing.Value, xp);
                        if (!setResult.Success)
                        {
                            return setResult;
                        }
                    }
                }
                else
                {
                    var addResult = doc.AddField(doc.Tables[levelsTableIndex], key, xp.ToString(CultureInfo.InvariantCulture));
                    if (!addResult.Success)
                    {
                        return addResult;
                    }
                }
            }

            return Result.FromSuccess();
        }

        if (desired.Count == 0)
        {
            return Result.FromSuccess();
        }

        var lastOwnedIndex = FindLastOwnedTableIndex(doc, skillTableIndex, "Skills");
        var insertOffset = Math.Clamp(doc.Tables[lastOwnedIndex].ContentSpan.End, 0, doc.Text.Length);
        var eol = doc.LineEnding;
        var block = new StringBuilder("[Skills.Levels]");
        for (var i = 0; i < desired.Count; i++)
        {
            block.Append(eol);
            block.Append(desired[i].Level.ToString(CultureInfo.InvariantCulture));
            block.Append(" = ");
            block.Append(desired[i].Xp.ToString(CultureInfo.InvariantCulture));
        }

        var before = doc.Text.AsSpan(0, insertOffset);
        var after = doc.Text.AsSpan(insertOffset);
        var leading = before.Length == 0 || before.EndsWith("\r\n\r\n", StringComparison.Ordinal) || before.EndsWith("\n\n", StringComparison.Ordinal)
            ? string.Empty
            : (before.EndsWith("\n", StringComparison.Ordinal) ? eol : string.Concat(eol, eol));
        var trailing = after.Length == 0
            ? (doc.HasTrailingNewline ? eol : string.Empty)
            : (after.StartsWith("\r\n", StringComparison.Ordinal) || after.StartsWith("\n", StringComparison.Ordinal) ? eol : string.Concat(eol, eol));

        var sb = new StringBuilder(doc.Text.Length + leading.Length + block.Length + trailing.Length);
        sb.Append(before);
        sb.Append(leading);
        sb.Append(block);
        sb.Append(trailing);
        sb.Append(after);

        var candidate = sb.ToString();
        var verified = ToolsDocuments.TomlDocument.Parse(candidate, doc.SourcePath);
        if (verified.HasSyntaxErrors)
        {
            return Result.FromFailure("Applying skill level edits produced invalid TOML.");
        }

        doc.SetRawText(candidate);
        return Result.FromSuccess();
    }

    private static int FindOwnedSubTableIndex(
        ToolsDocuments.TomlDocument doc,
        int parentTableIndex,
        string arrayTableName,
        string subTableHeaderPath)
    {
        var prefix = arrayTableName + ".";
        for (var i = parentTableIndex + 1; i < doc.Tables.Count; i++)
        {
            var table = doc.Tables[i];
            if (table.IsArrayTable && string.Equals(table.HeaderPath, arrayTableName, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!table.HeaderPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!table.IsArrayTable && string.Equals(table.HeaderPath, subTableHeaderPath, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static Result ApplyBodyEdits(ToolsDocuments.TomlDocument doc, DocumentSavePayload payload)
    {
        var tableIndex = ResolvePrimaryTableIndex(doc, "Bodies", payload.TableIndex, payload.EntryIndex);
        if (tableIndex < 0)
        {
            return Result.FromSuccess();
        }

        var bodyOrdinal = FindArrayTableOrdinal(doc, "Bodies", tableIndex);
        ApplyScalar(doc, tableIndex, "Species", "Species", payload, ScalarKind.OptionalString);
        ApplyScalar(doc, tableIndex, "Gender", "Gender", payload, ScalarKind.String);

        if (!payload.BodySpritesModified)
        {
            return Result.FromSuccess();
        }

        return ApplyBodyStateEdits(doc, bodyOrdinal, payload.BodySprites ?? []);
    }

    private static Result ApplyBodyStateEdits(
        ToolsDocuments.TomlDocument doc,
        int bodyOrdinal,
        IReadOnlyList<(string State, string Direction, string TexturePath)> sprites)
    {
        var desiredStates = BuildDesiredBodyStates(sprites);

        var bodyTableIndex = ResolveArrayTableByOrdinal(doc, "Bodies", bodyOrdinal);
        if (bodyTableIndex < 0)
        {
            return Result.FromSuccess();
        }

        // Remove state sub-tables that are no longer present in the edited body sprites.
        var existingTables = CollectOwnedBodyStateTables(doc, bodyTableIndex);
        for (var i = existingTables.Count - 1; i >= 0; i--)
        {
            var existing = existingTables[i];
            if (FindDesiredStateIndex(desiredStates, existing.StateName) >= 0)
            {
                continue;
            }

            var removed = RemoveTableSection(doc, existing.TableIndex);
            if (!removed.Success)
            {
                return removed;
            }
        }

        // Update existing state sub-tables in place (fields added, updated, or removed).
        for (var s = 0; s < desiredStates.Count; s++)
        {
            var desiredState = desiredStates[s];
            bodyTableIndex = ResolveArrayTableByOrdinal(doc, "Bodies", bodyOrdinal);
            if (bodyTableIndex < 0)
            {
                return Result.FromFailure("Failed relocating [[Bodies]] entry while updating states.");
            }

            var stateTableIdx = FindOwnedBodyStateTableIndex(doc, bodyTableIndex, desiredState.State);
            if (stateTableIdx < 0)
            {
                continue;
            }

            var currentTable = doc.Tables[stateTableIdx];
            for (var f = currentTable.Fields.Count - 1; f >= 0; f--)
            {
                var field = currentTable.Fields[f];
                if (FindDesiredDirectionIndex(desiredState.Directions, field.Key) < 0)
                {
                    var removeResult = doc.RemoveField(in field);
                    if (!removeResult.Success)
                    {
                        return removeResult;
                    }

                    bodyTableIndex = ResolveArrayTableByOrdinal(doc, "Bodies", bodyOrdinal);
                    stateTableIdx = FindOwnedBodyStateTableIndex(doc, bodyTableIndex, desiredState.State);
                    if (stateTableIdx < 0)
                    {
                        break;
                    }

                    currentTable = doc.Tables[stateTableIdx];
                }
            }

            for (var d = 0; d < desiredState.Directions.Count; d++)
            {
                var (direction, paths) = desiredState.Directions[d];
                bodyTableIndex = ResolveArrayTableByOrdinal(doc, "Bodies", bodyOrdinal);
                stateTableIdx = FindOwnedBodyStateTableIndex(doc, bodyTableIndex, desiredState.State);
                if (stateTableIdx < 0)
                {
                    break;
                }

                var existingField = FindFieldInTableCaseInsensitive(doc, stateTableIdx, direction);
                if (existingField.HasValue)
                {
                    var currentPaths = ReadStringArray(existingField.Value);
                    if (!SequenceEqualsOrdinal(currentPaths, paths))
                    {
                        var setResult = doc.SetStringArray(existingField.Value, paths);
                        if (!setResult.Success)
                        {
                            return setResult;
                        }
                    }
                }
                else
                {
                    var addResult = doc.AddField(doc.Tables[stateTableIdx], direction, FormatTomlStringArray(paths));
                    if (!addResult.Success)
                    {
                        return addResult;
                    }
                }
            }
        }

        // Insert newly added states directly under this [[Bodies]] entry (preserving multi-entry boundaries).
        for (var s = 0; s < desiredStates.Count; s++)
        {
            var desiredState = desiredStates[s];
            bodyTableIndex = ResolveArrayTableByOrdinal(doc, "Bodies", bodyOrdinal);
            if (bodyTableIndex < 0)
            {
                return Result.FromFailure("Failed relocating [[Bodies]] entry while adding states.");
            }

            if (FindOwnedBodyStateTableIndex(doc, bodyTableIndex, desiredState.State) >= 0)
            {
                continue;
            }

            var insertResult = InsertBodyStateTable(doc, bodyTableIndex, desiredState.State, desiredState.Directions);
            if (!insertResult.Success)
            {
                return insertResult;
            }
        }

        return Result.FromSuccess();
    }

    private static Result ApplyMaterialEdits(ToolsDocuments.TomlDocument doc, DocumentSavePayload payload)
    {
        ApplyRootScalar(doc, "Shader", "Shader", payload, ScalarKind.String);
        ApplyRootScalar(doc, "AlphaMode", "AlphaMode", payload, ScalarKind.String);
        ApplyRootScalar(doc, "AlphaCutoff", "AlphaCutoff", payload, ScalarKind.Float);
        ApplyRootCsvStringArray(doc, "Textures", "Textures", payload);
        return Result.FromSuccess();
    }

    private static Result ApplyTagEdits(ToolsDocuments.TomlDocument doc, DocumentSavePayload payload)
    {
        var field = doc.GetField("Tags", payload.AssetId);
        if (!field.Success)
        {
            return Result.FromSuccess();
        }

        var existing = ReadStringArray(field.Value);
        if (SequenceEqualsOrdinal(existing, payload.TagFileValues))
        {
            return Result.FromSuccess();
        }

        return doc.SetStringArray(field.Value, payload.TagFileValues);
    }

    private static Result ApplyLocalizationEdits(ToolsDocuments.TomlDocument doc, DocumentSavePayload payload)
    {
        var effectiveRows = BuildLocalizationEditRows(payload);
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < effectiveRows.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(effectiveRows[i].Key))
            {
                activeKeys.Add(effectiveRows[i].Key);
            }
        }

        var translations = doc.GetTable("Translations");
        if (!translations.Success)
        {
            if (activeKeys.Count == 0)
            {
                return Result.FromSuccess();
            }

            var addedTable = doc.AddTable("Translations", []);
            if (!addedTable.Success)
            {
                return Result.FromFailure(addedTable.Message);
            }

            translations = doc.GetTable("Translations");
            if (!translations.Success)
            {
                return Result.FromSuccess();
            }
        }

        var keysToRemove = new HashSet<string>(StringComparer.Ordinal);
        if (payload.RemovedLocalizationKeys != null)
        {
            for (var i = 0; i < payload.RemovedLocalizationKeys.Count; i++)
            {
                var removedKey = payload.RemovedLocalizationKeys[i]?.Trim() ?? string.Empty;
                if (removedKey.Length > 0 && !activeKeys.Contains(removedKey))
                {
                    keysToRemove.Add(removedKey);
                }
            }
        }

        // Rename keys in place first so surrounding comments and line order stay intact.
        for (var i = 0; i < effectiveRows.Count; i++)
        {
            var (originalKey, key, _) = effectiveRows[i];
            if (string.IsNullOrWhiteSpace(originalKey)
                || string.Equals(originalKey, key, StringComparison.Ordinal)
                || activeKeys.Contains(originalKey))
            {
                continue;
            }

            translations = doc.GetTable("Translations");
            if (!translations.Success)
            {
                return Result.FromSuccess();
            }

            var tableIdx = translations.Value.Index;
            var existingOrig = doc.GetFieldInTable(tableIdx, originalKey);
            if (!existingOrig.Success)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                var existingTarget = doc.GetFieldInTable(tableIdx, key);
                if (!existingTarget.Success)
                {
                    var renamed = doc.RenameKey(existingOrig.Value, key);
                    if (renamed.Success)
                    {
                        keysToRemove.Remove(originalKey);
                        continue;
                    }
                }
            }

            keysToRemove.Add(originalKey);
        }

        // Remove explicitly deleted keys and any orphaned original keys that could not be renamed in place.
        foreach (var staleKey in keysToRemove)
        {
            translations = doc.GetTable("Translations");
            if (!translations.Success)
            {
                break;
            }

            var staleField = doc.GetFieldInTable(translations.Value.Index, staleKey);
            if (staleField.Success)
            {
                var removeResult = doc.RemoveField(staleField.Value);
                if (!removeResult.Success)
                {
                    return removeResult;
                }
            }
        }

        // Upsert all active localization rows in place or append new entries cleanly.
        for (var i = 0; i < effectiveRows.Count; i++)
        {
            var (_, key, value) = effectiveRows[i];
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            translations = doc.GetTable("Translations");
            if (!translations.Success)
            {
                return Result.FromSuccess();
            }

            var existing = doc.GetFieldInTable(translations.Value.Index, key);
            if (existing.Success)
            {
                if (existing.Value.TypedValue is string current && string.Equals(current, value, StringComparison.Ordinal))
                {
                    continue;
                }

                var setResult = doc.SetString(existing.Value, value);
                if (!setResult.Success)
                {
                    return setResult;
                }
            }
            else
            {
                var addResult = doc.AddField(translations.Value, key, FormatTomlString(value));
                if (!addResult.Success)
                {
                    return addResult;
                }
            }
        }

        return Result.FromSuccess();
    }

    private static List<(string OriginalKey, string Key, string Value)> BuildLocalizationEditRows(DocumentSavePayload payload)
    {
        if (payload.LocalizationKeyEdits != null)
        {
            var edits = new List<(string OriginalKey, string Key, string Value)>(payload.LocalizationKeyEdits.Count);
            for (var i = 0; i < payload.LocalizationKeyEdits.Count; i++)
            {
                var row = payload.LocalizationKeyEdits[i];
                edits.Add((
                    row.OriginalKey?.Trim() ?? string.Empty,
                    row.Key?.Trim() ?? string.Empty,
                    row.Value ?? string.Empty));
            }

            return edits;
        }

        var rows = new List<(string OriginalKey, string Key, string Value)>(payload.LocalizationRows.Count);
        for (var i = 0; i < payload.LocalizationRows.Count; i++)
        {
            var row = payload.LocalizationRows[i];
            var key = row.Key?.Trim() ?? string.Empty;
            rows.Add((key, key, row.Value ?? string.Empty));
        }

        return rows;
    }

    private static void ApplyScalar(
        ToolsDocuments.TomlDocument doc,
        int tableIndex,
        string tomlKey,
        string fieldKey,
        DocumentSavePayload payload,
        ScalarKind kind)
    {
        if (payload.ModifiedFieldKeys != null && !payload.ModifiedFieldKeys.Contains(fieldKey))
        {
            return;
        }

        if (!payload.FieldValues.TryGetValue(fieldKey, out var value))
        {
            return;
        }

        var isRawMode = payload.FieldRawModeFlags.TryGetValue(fieldKey, out var rawFlag) && rawFlag;
        var fieldResult = doc.GetFieldInTable(tableIndex, tomlKey);
        if (!fieldResult.Success)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (tableIndex < 0 || tableIndex >= doc.Tables.Count)
            {
                return;
            }

            var formatted = FormatForAdd(value, fieldKey, isRawMode, payload, kind);
            if (!string.IsNullOrWhiteSpace(formatted))
            {
                doc.AddField(doc.Tables[tableIndex], tomlKey, formatted);
            }

            return;
        }

        var field = fieldResult.Value;
        if (isRawMode && payload.FieldRawTokens.TryGetValue(fieldKey, out var rawToken) && !string.IsNullOrWhiteSpace(rawToken))
        {
            if (!string.Equals(field.RawValue.Trim(), rawToken.Trim(), StringComparison.Ordinal))
            {
                doc.SetRawValue(in field, rawToken.Trim());
            }

            return;
        }

        ApplyTypedScalarEdit(doc, in field, value, kind);
    }

    private static void ApplyRootScalar(
        ToolsDocuments.TomlDocument doc,
        string tomlKey,
        string fieldKey,
        DocumentSavePayload payload,
        ScalarKind kind)
    {
        if (payload.ModifiedFieldKeys != null && !payload.ModifiedFieldKeys.Contains(fieldKey))
        {
            return;
        }

        if (!payload.FieldValues.TryGetValue(fieldKey, out var value))
        {
            return;
        }

        var isRawMode = payload.FieldRawModeFlags.TryGetValue(fieldKey, out var rawFlag) && rawFlag;
        var fieldResult = doc.GetRootField(tomlKey);
        if (!fieldResult.Success)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var formatted = FormatForAdd(value, fieldKey, isRawMode, payload, kind);
            if (!string.IsNullOrWhiteSpace(formatted))
            {
                doc.AddRootField(tomlKey, formatted);
            }

            return;
        }

        var field = fieldResult.Value;
        if (isRawMode && payload.FieldRawTokens.TryGetValue(fieldKey, out var rawToken) && !string.IsNullOrWhiteSpace(rawToken))
        {
            if (!string.Equals(field.RawValue.Trim(), rawToken.Trim(), StringComparison.Ordinal))
            {
                doc.SetRawValue(in field, rawToken.Trim());
            }

            return;
        }

        ApplyTypedScalarEdit(doc, in field, value, kind);
    }

    private static void ApplyTypedScalarEdit(
        ToolsDocuments.TomlDocument doc,
        in ToolsDocuments.TomlFieldSpan field,
        string value,
        ScalarKind kind)
    {
        switch (kind)
        {
            case ScalarKind.Boolean:
                if (bool.TryParse(value, out var boolVal)
                    && (field.TypedValue is not bool existingBool || existingBool != boolVal))
                {
                    doc.SetBoolean(in field, boolVal);
                }

                break;

            case ScalarKind.Integer:
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal)
                    && (field.TypedValue is not long existingLong || existingLong != intVal))
                {
                    doc.SetInteger(in field, intVal);
                }

                break;

            case ScalarKind.Float:
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblVal))
                {
                    var currentDbl = field.TypedValue switch
                    {
                        double d => d,
                        long l => (double)l,
                        _ => double.NaN,
                    };
                    if (double.IsNaN(currentDbl) || Math.Abs(currentDbl - dblVal) > 1e-6)
                    {
                        doc.SetFloat(in field, dblVal);
                    }
                }

                break;

            case ScalarKind.OptionalString:
                if (field.TypedValue is string optExisting && string.Equals(optExisting, value, StringComparison.Ordinal))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    doc.SetString(in field, value);
                }

                break;

            default:
                if (field.TypedValue is string strExisting && string.Equals(strExisting, value, StringComparison.Ordinal))
                {
                    return;
                }

                doc.SetString(in field, value);
                break;
        }
    }

    private static void ApplySingleItemStringArray(
        ToolsDocuments.TomlDocument doc,
        int tableIndex,
        string tomlKey,
        string fieldKey,
        DocumentSavePayload payload)
    {
        if (payload.ModifiedFieldKeys != null && !payload.ModifiedFieldKeys.Contains(fieldKey))
        {
            return;
        }

        if (!payload.FieldValues.TryGetValue(fieldKey, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var fieldResult = doc.GetFieldInTable(tableIndex, tomlKey);
        var items = SplitCommaItems(value);
        if (!fieldResult.Success)
        {
            if (tableIndex >= 0 && tableIndex < doc.Tables.Count)
            {
                var rawArr = FormatTomlStringArray(items);
                doc.AddField(doc.Tables[tableIndex], tomlKey, rawArr);
            }

            return;
        }

        var existing = ReadStringArray(fieldResult.Value);
        if (!SequenceEqualsOrdinal(existing, items))
        {
            doc.SetStringArray(fieldResult.Value, items);
        }
    }

    private static void ApplyOptionalSingleItemStringArray(
        ToolsDocuments.TomlDocument doc,
        int tableIndex,
        string tomlKey,
        string fieldKey,
        DocumentSavePayload payload)
    {
        if (!payload.FieldValues.TryGetValue(fieldKey, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        ApplySingleItemStringArray(doc, tableIndex, tomlKey, fieldKey, payload);
    }

    private static void ApplyCsvStringArray(
        ToolsDocuments.TomlDocument doc,
        int tableIndex,
        string tomlKey,
        string fieldKey,
        DocumentSavePayload payload)
    {
        if (payload.ModifiedFieldKeys != null && !payload.ModifiedFieldKeys.Contains(fieldKey))
        {
            return;
        }

        if (!payload.FieldValues.TryGetValue(fieldKey, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var fieldResult = doc.GetFieldInTable(tableIndex, tomlKey);
        if (!fieldResult.Success)
        {
            return;
        }

        var items = SplitCommaItems(value);
        var existing = ReadStringArray(fieldResult.Value);
        if (!SequenceEqualsOrdinal(existing, items))
        {
            doc.SetStringArray(fieldResult.Value, items);
        }
    }

    private static void ApplyRootCsvStringArray(
        ToolsDocuments.TomlDocument doc,
        string tomlKey,
        string fieldKey,
        DocumentSavePayload payload)
    {
        if (payload.ModifiedFieldKeys != null && !payload.ModifiedFieldKeys.Contains(fieldKey))
        {
            return;
        }

        if (!payload.FieldValues.TryGetValue(fieldKey, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var fieldResult = doc.GetRootField(tomlKey);
        if (!fieldResult.Success)
        {
            return;
        }

        var items = SplitCommaItems(value);
        var existing = ReadStringArray(fieldResult.Value);
        if (!SequenceEqualsOrdinal(existing, items))
        {
            doc.SetStringArray(fieldResult.Value, items);
        }
    }

    private static int ResolvePrimaryTableIndex(
        ToolsDocuments.TomlDocument doc,
        string headerPath,
        int tableIndex,
        int entryIndex)
    {
        if (tableIndex >= 0 && tableIndex < doc.Tables.Count
            && string.Equals(doc.Tables[tableIndex].HeaderPath, headerPath, StringComparison.Ordinal))
        {
            return tableIndex;
        }

        var tables = doc.GetTables(headerPath);
        if (entryIndex >= 0 && entryIndex < tables.Count)
        {
            return tables[entryIndex].Index;
        }

        return tables.Count > 0 ? tables[0].Index : -1;
    }

    private static int FindChildTableIndex(
        ToolsDocuments.TomlDocument doc,
        int parentTableIndex,
        string parentHeaderPath,
        string childHeaderPath)
    {
        for (var i = parentTableIndex + 1; i < doc.Tables.Count; i++)
        {
            var table = doc.Tables[i];
            if (string.Equals(table.HeaderPath, parentHeaderPath, StringComparison.Ordinal))
            {
                break;
            }

            if (string.Equals(table.HeaderPath, childHeaderPath, StringComparison.Ordinal))
            {
                return table.Index;
            }
        }

        return -1;
    }

    private static string? FormatForAdd(
        string value,
        string fieldKey,
        bool isRawMode,
        DocumentSavePayload payload,
        ScalarKind kind)
    {
        if (isRawMode && payload.FieldRawTokens.TryGetValue(fieldKey, out var rawToken) && !string.IsNullOrWhiteSpace(rawToken))
        {
            return rawToken.Trim();
        }

        return kind switch
        {
            ScalarKind.Boolean when bool.TryParse(value, out var b) => b ? "true" : "false",
            ScalarKind.Integer when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i.ToString(CultureInfo.InvariantCulture),
            ScalarKind.Float when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) => f.ToString("0.0######", CultureInfo.InvariantCulture),
            _ => FormatTomlString(value),
        };
    }

    private static string FormatTomlString(string value)
    {
        var escaped = (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static string FormatTomlStringArray(IReadOnlyList<string> items)
    {
        var formatted = new string[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            formatted[i] = FormatTomlString(items[i]);
        }

        return "[" + string.Join(", ", formatted) + "]";
    }

    private static List<string> ReadStringArray(in ToolsDocuments.TomlFieldSpan field)
    {
        var list = new List<string>(field.ArrayElements.Count);
        for (var i = 0; i < field.ArrayElements.Count; i++)
        {
            if (field.ArrayElements[i].TypedValue is string s)
            {
                list.Add(s);
            }
        }

        return list;
    }

    private static List<string> SplitCommaItems(string csv)
    {
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<string>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(parts[i]))
            {
                list.Add(parts[i]);
            }
        }

        return list;
    }

    private static bool SequenceEqualsOrdinal(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static List<(string State, List<(string Direction, List<string> Paths)> Directions)> BuildDesiredBodyStates(
        IReadOnlyList<(string State, string Direction, string TexturePath)> sprites)
    {
        var result = new List<(string State, List<(string Direction, List<string> Paths)> Directions)>();
        for (var i = 0; i < sprites.Count; i++)
        {
            var state = sprites[i].State?.Trim() ?? string.Empty;
            var direction = sprites[i].Direction?.Trim() ?? string.Empty;
            var texRaw = sprites[i].TexturePath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(direction) || string.IsNullOrWhiteSpace(texRaw))
            {
                continue;
            }

            var paths = SplitCommaItems(texRaw);
            if (paths.Count == 0)
            {
                continue;
            }

            var stateIndex = FindDesiredStateIndex(result, state);
            if (stateIndex < 0)
            {
                result.Add((state, [(direction, paths)]));
                continue;
            }

            var directions = result[stateIndex].Directions;
            var dirIndex = FindDesiredDirectionIndex(directions, direction);
            if (dirIndex < 0)
            {
                directions.Add((direction, paths));
            }
            else
            {
                var targetPaths = directions[dirIndex].Paths;
                for (var p = 0; p < paths.Count; p++)
                {
                    if (!targetPaths.Contains(paths[p], StringComparer.Ordinal))
                    {
                        targetPaths.Add(paths[p]);
                    }
                }
            }
        }

        return result;
    }

    private static int FindDesiredStateIndex(
        List<(string State, List<(string Direction, List<string> Paths)> Directions)> states,
        string stateName)
    {
        for (var i = 0; i < states.Count; i++)
        {
            if (string.Equals(states[i].State, stateName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindDesiredDirectionIndex(
        List<(string Direction, List<string> Paths)> directions,
        string directionName)
    {
        for (var i = 0; i < directions.Count; i++)
        {
            if (string.Equals(directions[i].Direction, directionName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindArrayTableOrdinal(ToolsDocuments.TomlDocument doc, string headerPath, int tableIndex)
    {
        var ordinal = 0;
        for (var i = 0; i < doc.Tables.Count; i++)
        {
            var table = doc.Tables[i];
            if (table.IsArrayTable && string.Equals(table.HeaderPath, headerPath, StringComparison.Ordinal))
            {
                if (table.Index == tableIndex)
                {
                    return ordinal;
                }

                ordinal++;
            }
        }

        return 0;
    }

    private static int ResolveArrayTableByOrdinal(ToolsDocuments.TomlDocument doc, string headerPath, int ordinal)
    {
        var seen = 0;
        for (var i = 0; i < doc.Tables.Count; i++)
        {
            var table = doc.Tables[i];
            if (table.IsArrayTable && string.Equals(table.HeaderPath, headerPath, StringComparison.Ordinal))
            {
                if (seen == ordinal)
                {
                    return table.Index;
                }

                seen++;
            }
        }

        return -1;
    }

    private static List<(int TableIndex, string StateName)> CollectOwnedBodyStateTables(
        ToolsDocuments.TomlDocument doc,
        int bodyTableIndex)
    {
        var matches = new List<(int TableIndex, string StateName)>();
        for (var i = bodyTableIndex + 1; i < doc.Tables.Count; i++)
        {
            var table = doc.Tables[i];
            if (table.IsArrayTable && string.Equals(table.HeaderPath, "Bodies", StringComparison.Ordinal))
            {
                break;
            }

            if (!table.HeaderPath.StartsWith("Bodies.", StringComparison.Ordinal))
            {
                break;
            }

            if (table.HeaderSegments.Count >= 3
                && string.Equals(table.HeaderSegments[0], "Bodies", StringComparison.Ordinal)
                && string.Equals(table.HeaderSegments[1], "States", StringComparison.Ordinal))
            {
                var stateName = table.HeaderSegments.Count == 3
                    ? table.HeaderSegments[2]
                    : string.Join('.', table.HeaderSegments.Skip(2));
                matches.Add((table.Index, stateName));
            }
        }

        return matches;
    }

    private static int FindOwnedBodyStateTableIndex(
        ToolsDocuments.TomlDocument doc,
        int bodyTableIndex,
        string stateName)
    {
        var tables = CollectOwnedBodyStateTables(doc, bodyTableIndex);
        for (var i = 0; i < tables.Count; i++)
        {
            if (string.Equals(tables[i].StateName, stateName, StringComparison.OrdinalIgnoreCase))
            {
                return tables[i].TableIndex;
            }
        }

        return -1;
    }

    private static int FindLastOwnedTableIndex(
        ToolsDocuments.TomlDocument doc,
        int parentTableIndex,
        string parentHeaderPath)
    {
        var childPrefix = parentHeaderPath + ".";
        var lastOwned = parentTableIndex;
        for (var i = parentTableIndex + 1; i < doc.Tables.Count; i++)
        {
            var next = doc.Tables[i];
            if (next.IsArrayTable && string.Equals(next.HeaderPath, parentHeaderPath, StringComparison.Ordinal))
            {
                break;
            }

            if (next.HeaderPath.StartsWith(childPrefix, StringComparison.Ordinal))
            {
                lastOwned = i;
                continue;
            }

            break;
        }

        return lastOwned;
    }

    private static ToolsDocuments.TomlFieldSpan? FindFieldInTableCaseInsensitive(
        ToolsDocuments.TomlDocument doc,
        int tableIndex,
        string key)
    {
        if (tableIndex < 0 || tableIndex >= doc.Tables.Count)
        {
            return null;
        }

        var fields = doc.Tables[tableIndex].Fields;
        for (var i = 0; i < fields.Count; i++)
        {
            if (string.Equals(fields[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return fields[i];
            }
        }

        return null;
    }

    private static Result RemoveTableSection(ToolsDocuments.TomlDocument doc, int tableIndex)
    {
        if (tableIndex < 0 || tableIndex >= doc.Tables.Count)
        {
            return Result.FromSuccess();
        }

        var table = doc.Tables[tableIndex];
        var text = doc.Text;
        var start = Math.Clamp(table.HeaderLineSpan.Start, 0, text.Length);
        var end = Math.Clamp(table.ContentSpan.End, start, text.Length);

        end = AdvanceOverBlankLines(text, end);
        if (end >= text.Length)
        {
            start = RewindOverBlankLines(text, start);
            if (!doc.HasTrailingNewline && start > 0 && text[start - 1] == '\n')
            {
                start--;
                if (start > 0 && text[start - 1] == '\r')
                {
                    start--;
                }
            }
        }

        var candidate = string.Concat(text.AsSpan(0, start), text.AsSpan(end));
        var parsed = ToolsDocuments.TomlDocument.Parse(candidate, doc.SourcePath);
        if (parsed.HasSyntaxErrors)
        {
            var err = parsed.Diagnostics.Count > 0 ? parsed.Diagnostics[0].Message : "Removing body state table produced invalid TOML.";
            return Result.FromFailure(err);
        }

        doc.SetRawText(candidate);
        return Result.FromSuccess();
    }

    private static Result InsertBodyStateTable(
        ToolsDocuments.TomlDocument doc,
        int bodyTableIndex,
        string stateName,
        List<(string Direction, List<string> Paths)> directions)
    {
        var lastOwnedIndex = FindLastOwnedTableIndex(doc, bodyTableIndex, "Bodies");
        var insertOffset = Math.Clamp(doc.Tables[lastOwnedIndex].ContentSpan.End, 0, doc.Text.Length);
        var eol = doc.LineEnding;

        var formattedKeys = new string[directions.Count];
        var maxKeyLen = 0;
        for (var i = 0; i < directions.Count; i++)
        {
            formattedKeys[i] = FormatTomlKeySegment(directions[i].Direction);
            if (formattedKeys[i].Length > maxKeyLen)
            {
                maxKeyLen = formattedKeys[i].Length;
            }
        }

        var block = new StringBuilder();
        block.Append("[Bodies.States.");
        block.Append(FormatTomlKeySegment(stateName));
        block.Append(']');

        for (var i = 0; i < directions.Count; i++)
        {
            block.Append(eol);
            block.Append(formattedKeys[i]);
            var pad = directions.Count > 1 ? (maxKeyLen - formattedKeys[i].Length) + 1 : 1;
            block.Append(' ', pad);
            block.Append("= ");
            block.Append(FormatTomlStringArray(directions[i].Paths));
        }

        var before = doc.Text.AsSpan(0, insertOffset);
        var after = doc.Text.AsSpan(insertOffset);

        string leading;
        if (before.Length == 0
            || before.EndsWith("\r\n\r\n", StringComparison.Ordinal)
            || before.EndsWith("\n\n", StringComparison.Ordinal))
        {
            leading = string.Empty;
        }
        else if (before.EndsWith("\n", StringComparison.Ordinal))
        {
            leading = eol;
        }
        else
        {
            leading = string.Concat(eol, eol);
        }

        string trailing;
        if (after.Length == 0)
        {
            trailing = doc.HasTrailingNewline ? eol : string.Empty;
        }
        else if (after.StartsWith("\r\n", StringComparison.Ordinal) || after.StartsWith("\n", StringComparison.Ordinal))
        {
            trailing = eol;
        }
        else
        {
            trailing = string.Concat(eol, eol);
        }

        var sb = new StringBuilder(doc.Text.Length + leading.Length + block.Length + trailing.Length);
        sb.Append(before);
        sb.Append(leading);
        sb.Append(block);
        sb.Append(trailing);
        sb.Append(after);
        var candidate = sb.ToString();
        var parsed = ToolsDocuments.TomlDocument.Parse(candidate, doc.SourcePath);
        if (parsed.HasSyntaxErrors)
        {
            var err = parsed.Diagnostics.Count > 0 ? parsed.Diagnostics[0].Message : "Inserting body state table produced invalid TOML.";
            return Result.FromFailure(err);
        }

        doc.SetRawText(candidate);
        return Result.FromSuccess();
    }

    private static string FormatTomlKeySegment(string segment)
    {
        if (IsBareTomlKey(segment))
        {
            return segment;
        }

        return FormatTomlString(segment);
    }

    private static bool IsBareTomlKey(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return false;
        }

        for (var i = 0; i < segment.Length; i++)
        {
            var c = segment[i];
            var ok = (c >= 'A' && c <= 'Z')
                || (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9')
                || c == '_'
                || c == '-';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static int AdvanceOverBlankLines(string text, int offset)
    {
        var cursor = offset;
        while (cursor < text.Length)
        {
            var lineStart = cursor;
            while (cursor < text.Length && (text[cursor] == ' ' || text[cursor] == '\t'))
            {
                cursor++;
            }

            if (cursor < text.Length && text[cursor] == '\r')
            {
                cursor++;
            }

            if (cursor < text.Length && text[cursor] == '\n')
            {
                cursor++;
                continue;
            }

            return cursor >= text.Length ? text.Length : lineStart;
        }

        return cursor;
    }

    private static int RewindOverBlankLines(string text, int offset)
    {
        var cursor = offset;
        while (cursor > 0)
        {
            if (text[cursor - 1] != '\n')
            {
                break;
            }

            var scan = cursor - 1;
            if (scan > 0 && text[scan - 1] == '\r')
            {
                scan--;
            }

            var lineStart = scan;
            while (lineStart > 0 && text[lineStart - 1] != '\n')
            {
                lineStart--;
            }

            var isBlank = true;
            for (var i = lineStart; i < scan; i++)
            {
                if (text[i] != ' ' && text[i] != '\t')
                {
                    isBlank = false;
                    break;
                }
            }

            if (!isBlank)
            {
                break;
            }

            cursor = lineStart;
        }

        return cursor;
    }

    private enum ScalarKind
    {
        String,
        OptionalString,
        Boolean,
        Integer,
        Float,
    }
}
