using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Storage;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Plans and applies cross-file asset ID and localization key renames as one undoable draft operation.
/// </summary>
public sealed class RenamePlanner
{
    /// <summary>
    ///     Computes a preview of all writable files, spans, and warnings affected by renaming <paramref name="oldId"/>.
    /// </summary>
    public Result<RenamePreview> PlanRename(
        WorkspaceIndex workspace,
        AssetKind kind,
        string oldId,
        string newId)
    {
        if (workspace == null)
        {
            return Result<RenamePreview>.FromFailure("Workspace is required.");
        }

        if (string.IsNullOrWhiteSpace(oldId) || string.IsNullOrWhiteSpace(newId))
        {
            return Result<RenamePreview>.FromFailure("Both old and new identifiers are required.");
        }

        if (string.Equals(oldId, newId, StringComparison.Ordinal))
        {
            return Result<RenamePreview>.FromFailure("New identifier must differ from the current identifier.");
        }

        if (kind == AssetKind.Localization)
        {
            return PlanLocalizationKeyRename(workspace, oldId, newId);
        }

        if (workspace.GetAsset(kind, newId).Success)
        {
            return Result<RenamePreview>.FromFailure($"Target {kind} ID '{newId}' already exists in workspace.");
        }

        List<string> warnings = [];
        if (kind == AssetKind.Brick)
        {
            warnings.Add($"Renaming brick '{oldId}' to '{newId}' breaks legacy save hash reverse-mapping.");
        }

        HashSet<string> candidateFiles = CollectCandidateFiles(workspace, kind, oldId, warnings);
        List<string> sortedFiles = [.. candidateFiles];
        sortedFiles.Sort(StringComparer.OrdinalIgnoreCase);

        List<RenameFilePlan> filePlans = [];
        for (int i = 0; i < sortedFiles.Count; i++)
        {
            Result<RenameFilePlan?> planResult = BuildFileRenamePlan(workspace, sortedFiles[i], kind, oldId, newId);
            if (!planResult.Success)
            {
                return Result<RenamePreview>.FromFailure(planResult.Message);
            }

            if (planResult.Value.HasValue)
            {
                filePlans.Add(planResult.Value.Value);
            }
        }

        if (filePlans.Count == 0)
        {
            return Result<RenamePreview>.FromFailure($"No writable occurrences of {kind} '{oldId}' were found.");
        }

        return Result<RenamePreview>.FromSuccess(new RenamePreview(kind, oldId, newId, filePlans, warnings));
    }

    /// <summary>
    ///     Applies all planned edits in <paramref name="preview"/> to <paramref name="draftStore"/> as one undoable batch.
    /// </summary>
    public Result<WorkspaceDraftBatch> ApplyToDrafts(in RenamePreview preview, DraftStore draftStore)
    {
        if (draftStore == null)
        {
            return Result<WorkspaceDraftBatch>.FromFailure("DraftStore is required.");
        }

        Dictionary<string, byte[]?> previousDrafts = new(StringComparer.Ordinal);
        for (int i = 0; i < preview.FilePlans.Count; i++)
        {
            string filePath = preview.FilePlans[i].FilePath;
            Result<DraftRecoveryEntry> existing = draftStore.LoadDraft(filePath);
            previousDrafts[filePath] = existing.Success ? existing.Value.DraftBytes : null;
        }

        var batch = new WorkspaceDraftBatch(
            $"Rename {preview.Kind} '{preview.OldId}' -> '{preview.NewId}'",
            preview.FilePlans,
            previousDrafts);

        Result applyResult = batch.Apply(draftStore);
        return applyResult.Success
            ? Result<WorkspaceDraftBatch>.FromSuccess(batch)
            : Result<WorkspaceDraftBatch>.FromFailure(applyResult.Message);
    }

    private static Result<RenamePreview> PlanLocalizationKeyRename(
        WorkspaceIndex workspace,
        string oldKey,
        string newKey)
    {
        List<string> warnings = [];
        HashSet<string> files = new(StringComparer.Ordinal);

        for (int i = 0; i < workspace.LocalizationEntries.Count; i++)
        {
            IndexedLocalizationEntry entry = workspace.LocalizationEntries[i];
            if (!string.Equals(entry.Key, oldKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (entry.IsWritable)
            {
                files.Add(entry.FilePath);
            }
            else
            {
                warnings.Add($"Read-only reference root '{entry.RootId}' also defines key '{oldKey}' and will not be modified.");
            }
        }

        IReadOnlyList<InboundReference> inbound = workspace.GetInboundReferences(oldKey);
        for (int i = 0; i < inbound.Count; i++)
        {
            if (IsFileWritable(workspace, inbound[i].SourceFilePath))
            {
                files.Add(inbound[i].SourceFilePath);
            }
            else
            {
                warnings.Add($"Read-only file '{inbound[i].SourceFilePath}' references '{oldKey}' and will not be modified.");
            }
        }

        List<string> sortedFiles = [.. files];
        sortedFiles.Sort(StringComparer.OrdinalIgnoreCase);

        List<RenameFilePlan> plans = [];
        for (int i = 0; i < sortedFiles.Count; i++)
        {
            Result<RenameFilePlan?> filePlan = BuildLocalizationFilePlan(workspace, sortedFiles[i], oldKey, newKey);
            if (!filePlan.Success)
            {
                return Result<RenamePreview>.FromFailure(filePlan.Message);
            }

            if (filePlan.Value.HasValue)
            {
                plans.Add(filePlan.Value.Value);
            }
        }

        if (plans.Count == 0)
        {
            return Result<RenamePreview>.FromFailure($"No writable occurrences of localization key '{oldKey}' were found.");
        }

        return Result<RenamePreview>.FromSuccess(new RenamePreview(AssetKind.Localization, oldKey, newKey, plans, warnings));
    }

    private static HashSet<string> CollectCandidateFiles(
        WorkspaceIndex workspace,
        AssetKind kind,
        string oldId,
        List<string> warnings)
    {
        HashSet<string> files = new(StringComparer.Ordinal);
        for (int i = 0; i < workspace.AllAssets.Count; i++)
        {
            IndexedAsset asset = workspace.AllAssets[i];
            if (asset.Kind != kind || !string.Equals(asset.Id, oldId, StringComparison.Ordinal))
            {
                continue;
            }

            if (asset.Provenance.IsWritable)
            {
                files.Add(asset.Provenance.FilePath);
            }
            else
            {
                warnings.Add($"Read-only reference root '{asset.Provenance.ProviderRootId}' defines '{oldId}' and will not be modified.");
            }
        }

        IReadOnlyList<InboundReference> inbound = workspace.GetInboundReferences(oldId);
        for (int i = 0; i < inbound.Count; i++)
        {
            if (IsFileWritable(workspace, inbound[i].SourceFilePath))
            {
                files.Add(inbound[i].SourceFilePath);
            }
            else
            {
                warnings.Add($"Read-only file '{inbound[i].SourceFilePath}' references '{oldId}' and will not be modified.");
            }
        }

        return files;
    }

    private static Result<RenameFilePlan?> BuildFileRenamePlan(
        WorkspaceIndex workspace,
        string filePath,
        AssetKind kind,
        string oldId,
        string newId)
    {
        if (!workspace.DocumentsByPath.TryGetValue(filePath, out TomlDocument? sourceDoc))
        {
            Result<TomlDocument> loaded = TomlDocument.Load(filePath);
            if (!loaded.Success)
            {
                return Result<RenameFilePlan?>.FromFailure(loaded.Message);
            }

            sourceDoc = loaded.Value;
        }

        if (sourceDoc.HasSyntaxErrors)
        {
            return Result<RenameFilePlan?>.FromFailure($"Cannot plan rename inside file with syntax errors: '{filePath}'.");
        }

        byte[] originalBytes = sourceDoc.ToBytes();
        string baseHash = ContentHash.ComputeSha256Hex(originalBytes);
        TomlDocument working = TomlDocument.Parse(originalBytes, filePath);

        List<RenameEditSpan> recordedEdits = [];
        CollectMatchingStringSpans(working, filePath, oldId, newId, recordedEdits);

        Result<int> valueRename = working.RenameStringValue(oldId, newId);
        if (!valueRename.Success)
        {
            return Result<RenameFilePlan?>.FromFailure(valueRename.Message);
        }

        if (kind == AssetKind.Brick || kind == AssetKind.Item)
        {
            Result keyRename = RenameSkillSourceKeys(working, filePath, oldId, newId, recordedEdits);
            if (!keyRename.Success)
            {
                return Result<RenameFilePlan?>.FromFailure(keyRename.Message);
            }
        }
        else if (kind == AssetKind.Recipe)
        {
            Result keyRename = RenameSkillSourceKeys(working, filePath, $"recipe:{oldId}", $"recipe:{newId}", recordedEdits);
            if (!keyRename.Success)
            {
                return Result<RenameFilePlan?>.FromFailure(keyRename.Message);
            }
        }

        if (recordedEdits.Count == 0)
        {
            return Result<RenameFilePlan?>.FromSuccess(null);
        }

        return Result<RenameFilePlan?>.FromSuccess(new RenameFilePlan(
            filePath,
            originalBytes,
            working.ToBytes(),
            baseHash,
            recordedEdits));
    }

    private static Result<RenameFilePlan?> BuildLocalizationFilePlan(
        WorkspaceIndex workspace,
        string filePath,
        string oldKey,
        string newKey)
    {
        if (!workspace.DocumentsByPath.TryGetValue(filePath, out TomlDocument? sourceDoc))
        {
            Result<TomlDocument> loaded = TomlDocument.Load(filePath);
            if (!loaded.Success)
            {
                return Result<RenameFilePlan?>.FromFailure(loaded.Message);
            }

            sourceDoc = loaded.Value;
        }

        if (sourceDoc.HasSyntaxErrors)
        {
            return Result<RenameFilePlan?>.FromFailure($"Cannot plan rename inside file with syntax errors: '{filePath}'.");
        }

        byte[] originalBytes = sourceDoc.ToBytes();
        string baseHash = ContentHash.ComputeSha256Hex(originalBytes);
        TomlDocument working = TomlDocument.Parse(originalBytes, filePath);
        List<RenameEditSpan> edits = [];

        Result<TomlFieldSpan> translationField = working.GetField("Translations", oldKey);
        if (translationField.Success)
        {
            string formattedKey = TomlValueFormatter.FormatKeySegment(newKey);
            edits.Add(new RenameEditSpan(
                filePath,
                $"Translations.{oldKey}",
                translationField.Value.KeySpan.Line,
                translationField.Value.KeySpan,
                translationField.Value.RawKey,
                formattedKey));

            Result renamed = working.RenameKey(translationField.Value, newKey);
            if (!renamed.Success)
            {
                return Result<RenameFilePlan?>.FromFailure(renamed.Message);
            }
        }

        CollectMatchingStringSpans(working, filePath, oldKey, newKey, edits);
        Result<int> valueRename = working.RenameStringValue(oldKey, newKey);
        if (!valueRename.Success)
        {
            return Result<RenameFilePlan?>.FromFailure(valueRename.Message);
        }

        if (edits.Count == 0)
        {
            return Result<RenameFilePlan?>.FromSuccess(null);
        }

        return Result<RenameFilePlan?>.FromSuccess(new RenameFilePlan(
            filePath,
            originalBytes,
            working.ToBytes(),
            baseHash,
            edits));
    }

    private static void CollectMatchingStringSpans(
        TomlDocument doc,
        string filePath,
        string oldValue,
        string newValue,
        List<RenameEditSpan> edits)
    {
        for (int i = 0; i < doc.AllFields.Count; i++)
        {
            TomlFieldSpan field = doc.AllFields[i];
            if (field.ValueKind == TomlValueKind.String &&
                field.TypedValue is string current &&
                string.Equals(current, oldValue, StringComparison.Ordinal))
            {
                string replacement = TomlValueFormatter.FormatString(newValue, field.RawValue);
                edits.Add(new RenameEditSpan(filePath, field.Key, field.ValueSpan.Line, field.ValueSpan, field.RawValue, replacement));
                continue;
            }

            if (field.ValueKind != TomlValueKind.Array)
            {
                continue;
            }

            for (int j = 0; j < field.ArrayElements.Count; j++)
            {
                TomlArrayElementSpan elem = field.ArrayElements[j];
                if (elem.ValueKind == TomlValueKind.String &&
                    elem.TypedValue is string elemStr &&
                    string.Equals(elemStr, oldValue, StringComparison.Ordinal))
                {
                    string replacement = TomlValueFormatter.FormatString(newValue, elem.RawValue);
                    edits.Add(new RenameEditSpan(filePath, $"{field.Key}[{j}]", elem.ValueSpan.Line, elem.ValueSpan, elem.RawValue, replacement));
                }
            }
        }
    }

    private static Result RenameSkillSourceKeys(
        TomlDocument working,
        string filePath,
        string oldSourceKey,
        string newSourceKey,
        List<RenameEditSpan> edits)
    {
        for (int t = 0; t < working.Tables.Count; t++)
        {
            TomlTableSpan table = working.Tables[t];
            if (!table.HeaderPath.StartsWith("Skills.Sources.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Result<TomlFieldSpan> fieldResult = working.GetFieldInTable(table.Index, oldSourceKey);
            if (!fieldResult.Success)
            {
                continue;
            }

            TomlFieldSpan field = fieldResult.Value;
            string formattedKey = TomlValueFormatter.FormatKeySegment(newSourceKey);
            edits.Add(new RenameEditSpan(
                filePath,
                $"{table.HeaderPath}.{oldSourceKey}",
                field.KeySpan.Line,
                field.KeySpan,
                field.RawKey,
                formattedKey));

            Result renamed = working.RenameKey(in field, newSourceKey);
            if (!renamed.Success)
            {
                return renamed;
            }
        }

        return Result.FromSuccess();
    }

    private static bool IsFileWritable(WorkspaceIndex workspace, string filePath)
    {
        string full = Path.GetFullPath(filePath);
        for (int i = workspace.Roots.Count - 1; i >= 0; i--)
        {
            WorkspaceRoot root = workspace.Roots[i];
            string prefix = Path.GetFullPath(root.RootDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return root.IsWritable;
            }
        }

        return false;
    }
}
