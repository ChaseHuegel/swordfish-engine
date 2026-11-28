using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Storage;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Safely deletes standalone asset files or surgically removes a single entry from multi-entry TOML files.
/// </summary>
public sealed class AssetDeleter
{
    private readonly AtomicFileSaver _atomicSaver;

    /// <summary>
    ///     Creates an asset deleter using <paramref name="atomicSaver"/> for multi-entry file updates.
    /// </summary>
    public AssetDeleter(AtomicFileSaver? atomicSaver = null)
    {
        _atomicSaver = atomicSaver ?? new AtomicFileSaver();
    }

    /// <summary>
    ///     Deletes the asset represented by <paramref name="asset"/> within a writable workspace root.
    /// </summary>
    public Result<AssetDeleteResult> Delete(WorkspaceIndex workspace, in IndexedAsset asset)
    {
        if (workspace == null)
        {
            return Result<AssetDeleteResult>.FromFailure("Workspace is required.");
        }

        if (!asset.Provenance.IsWritable)
        {
            return Result<AssetDeleteResult>.FromFailure(
                $"Refusing to delete '{asset.Provenance.FilePath}': asset belongs to read-only root '{asset.Provenance.ProviderRootId}'.");
        }

        return Delete(
            workspace,
            asset.Kind,
            asset.Provenance.FilePath,
            asset.Id,
            asset.Provenance.TableIndex >= 0 ? asset.Provenance.TableIndex : null,
            asset.Provenance.EntryIndex >= 0 ? asset.Provenance.EntryIndex : null);
    }

    /// <summary>
    ///     Deletes one tag key from its owning <c>[Tags]</c> TOML file or deletes the file when it is the only tag.
    /// </summary>
    public Result<AssetDeleteResult> Delete(WorkspaceIndex workspace, in IndexedTagEntry tagEntry)
    {
        if (workspace == null)
        {
            return Result<AssetDeleteResult>.FromFailure("Workspace is required.");
        }

        if (!tagEntry.IsWritable)
        {
            return Result<AssetDeleteResult>.FromFailure(
                $"Refusing to delete tag '{tagEntry.TagName}' in '{tagEntry.FilePath}': root '{tagEntry.RootId}' is read-only.");
        }

        return Delete(workspace, AssetKind.Tag, tagEntry.FilePath, tagEntry.TagName);
    }

    /// <summary>
    ///     Deletes a standalone asset file or removes one entry from a multi-entry TOML file inside a writable root.
    /// </summary>
    public Result<AssetDeleteResult> Delete(
        WorkspaceIndex workspace,
        AssetKind kind,
        string filePath,
        string? assetId = null,
        int? tableIndex = null,
        int? entryIndex = null)
    {
        if (workspace == null)
        {
            return Result<AssetDeleteResult>.FromFailure("Workspace is required.");
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<AssetDeleteResult>.FromFailure("File path is required.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            return Result<AssetDeleteResult>.FromFailure(ex);
        }

        Result writableCheck = ValidateWritableWorkspaceFile(workspace, fullPath);
        if (!writableCheck.Success)
        {
            return Result<AssetDeleteResult>.FromFailure(writableCheck.Message);
        }

        if (!File.Exists(fullPath))
        {
            return Result<AssetDeleteResult>.FromFailure($"File not found: '{fullPath}'.");
        }

        return kind switch
        {
            AssetKind.Material or AssetKind.Blueprint or AssetKind.Localization => DeleteWholeFile(fullPath),
            AssetKind.Tag => DeleteTagEntry(fullPath, assetId),
            AssetKind.Brick => DeleteArrayTableEntry(fullPath, "Bricks", assetId, tableIndex, entryIndex),
            AssetKind.Item => DeleteArrayTableEntry(fullPath, "Items", assetId, tableIndex, entryIndex),
            AssetKind.Paint => DeleteArrayTableEntry(fullPath, "Paints", assetId, tableIndex, entryIndex),
            AssetKind.Skill => DeleteArrayTableEntry(fullPath, "Skills", assetId, tableIndex, entryIndex),
            AssetKind.Body => DeleteArrayTableEntry(fullPath, "Bodies", assetId, tableIndex, entryIndex),
            AssetKind.Recipe => DeleteArrayTableEntry(fullPath, "Recipes", assetId, tableIndex, entryIndex),
            AssetKind.SlotGroup => DeleteArrayTableEntry(fullPath, "SlotGroups", assetId, tableIndex, entryIndex),
            _ => Result<AssetDeleteResult>.FromFailure($"Deletion is not supported for asset kind '{kind}'.")
        };
    }

    private Result<AssetDeleteResult> DeleteArrayTableEntry(
        string fullPath,
        string tableName,
        string? assetId,
        int? tableIndex,
        int? entryIndex)
    {
        Result<TomlDocument> loaded = TomlDocument.Load(fullPath);
        if (!loaded.Success)
        {
            return Result<AssetDeleteResult>.FromFailure(loaded.Message);
        }

        TomlDocument doc = loaded.Value;
        if (doc.HasSyntaxErrors)
        {
            return Result<AssetDeleteResult>.FromFailure($"Cannot delete entry from file with TOML syntax errors: '{fullPath}'.");
        }

        List<TomlTableSpan> entryTables = CollectArrayTables(doc, tableName);
        if (entryTables.Count == 0)
        {
            return Result<AssetDeleteResult>.FromFailure($"No [[{tableName}]] entry was found in '{fullPath}'.");
        }

        TomlTableSpan? targetTable = ResolveTargetEntryTable(doc, entryTables, tableName, assetId, tableIndex, entryIndex);
        if (!targetTable.HasValue)
        {
            string descriptor = !string.IsNullOrWhiteSpace(assetId)
                ? $"'{assetId}'"
                : $"index {tableIndex ?? entryIndex ?? -1}";
            return Result<AssetDeleteResult>.FromFailure($"Could not locate [[{tableName}]] entry {descriptor} in '{fullPath}'.");
        }

        int startTableIdx = targetTable.Value.Index;
        int lastOwnedTableIdx = FindLastOwnedTableIndex(doc, startTableIdx, tableName);
        bool hasOtherTables = startTableIdx > 0 || lastOwnedTableIdx < doc.Tables.Count - 1;
        if (entryTables.Count == 1 && !hasOtherTables)
        {
            return DeleteWholeFile(fullPath);
        }

        string updatedText = RemoveSpanPreservingSiblingSpacing(
            doc.Text,
            doc.HasTrailingNewline,
            doc.Tables[startTableIdx].HeaderLineSpan.Start,
            doc.Tables[lastOwnedTableIdx].ContentSpan.End);

        return SaveUpdatedDocument(fullPath, doc, updatedText);
    }

    private Result<AssetDeleteResult> DeleteTagEntry(string fullPath, string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return Result<AssetDeleteResult>.FromFailure("Tag name is required to delete a tag entry.");
        }

        Result<TomlDocument> loaded = TomlDocument.Load(fullPath);
        if (!loaded.Success)
        {
            return Result<AssetDeleteResult>.FromFailure(loaded.Message);
        }

        TomlDocument doc = loaded.Value;
        if (doc.HasSyntaxErrors)
        {
            return Result<AssetDeleteResult>.FromFailure($"Cannot delete tag from file with TOML syntax errors: '{fullPath}'.");
        }

        Result<TomlTableSpan> tagsTableResult = doc.GetTable("Tags");
        if (!tagsTableResult.Success)
        {
            return Result<AssetDeleteResult>.FromFailure($"File '{fullPath}' does not contain a [Tags] table.");
        }

        TomlTableSpan tagsTable = tagsTableResult.Value;
        Result<TomlFieldSpan> fieldResult = doc.GetFieldInTable(tagsTable.Index, tagName.Trim());
        if (!fieldResult.Success)
        {
            return Result<AssetDeleteResult>.FromFailure($"Tag '{tagName}' was not found in '{fullPath}'.");
        }

        if (tagsTable.Fields.Count == 1 && doc.Tables.Count == 1)
        {
            return DeleteWholeFile(fullPath);
        }

        string baseHash = ContentHash.ComputeSha256Hex(doc.ToBytes());
        TomlFieldSpan field = fieldResult.Value;
        bool isAligned = tagsTable.IsColumnAligned;
        bool hadTrailingNewline = doc.HasTrailingNewline;

        if (isAligned)
        {
            Result removed = doc.RemoveField(in field);
            if (!removed.Success)
            {
                return Result<AssetDeleteResult>.FromFailure(removed.Message);
            }

            string tidied = CollapseOrphanBlankLineAt(doc.Text, field.LineSpan.Start, hadTrailingNewline);
            return CommitAndSaveUpdatedDocument(fullPath, doc, baseHash, tidied);
        }

        string updatedText = RemoveSpanPreservingSiblingSpacing(
            doc.Text,
            hadTrailingNewline,
            field.LineSpan.Start,
            field.LineSpan.End);
        return CommitAndSaveUpdatedDocument(fullPath, doc, baseHash, updatedText);
    }

    private Result<AssetDeleteResult> SaveUpdatedDocument(string fullPath, TomlDocument doc, string updatedText)
    {
        string baseHash = ContentHash.ComputeSha256Hex(doc.ToBytes());
        return CommitAndSaveUpdatedDocument(fullPath, doc, baseHash, updatedText);
    }

    private Result<AssetDeleteResult> CommitAndSaveUpdatedDocument(
        string fullPath,
        TomlDocument doc,
        string baseHash,
        string updatedText)
    {
        TomlDocument verified = TomlDocument.Parse(updatedText, fullPath);
        if (verified.HasSyntaxErrors)
        {
            string reason = verified.Diagnostics.Count > 0
                ? verified.Diagnostics[0].Message
                : "Updated TOML document failed syntax validation.";
            return Result<AssetDeleteResult>.FromFailure($"Deleting entry produced invalid TOML: {reason}");
        }

        doc.SetRawText(updatedText);
        var saveRequest = new FileSaveRequest(fullPath, doc.ToBytes(), ExpectedBaseSha256: baseHash);
        AtomicSaveResult saved = _atomicSaver.Save(in saveRequest);
        if (!saved.Success)
        {
            return Result<AssetDeleteResult>.FromFailure(saved.ErrorMessage ?? "Failed saving updated TOML file.");
        }

        return Result<AssetDeleteResult>.FromSuccess(new AssetDeleteResult(fullPath, DeletedWholeFile: false, UpdatedText: doc.Text));
    }

    private static Result<AssetDeleteResult> DeleteWholeFile(string fullPath)
    {
        try
        {
            File.Delete(fullPath);
            return Result<AssetDeleteResult>.FromSuccess(new AssetDeleteResult(fullPath, DeletedWholeFile: true, UpdatedText: null));
        }
        catch (Exception ex)
        {
            return Result<AssetDeleteResult>.FromFailure(ex);
        }
    }

    private static Result ValidateWritableWorkspaceFile(WorkspaceIndex workspace, string fullPath)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        WorkspaceRoot? bestRoot = null;
        int bestPrefixLength = -1;

        for (int i = 0; i < workspace.Roots.Count; i++)
        {
            WorkspaceRoot root = workspace.Roots[i];
            if (!root.IsEnabled)
            {
                continue;
            }

            ConsiderRootPrefix(root, root.RootDirectory, fullPath, comparison, ref bestRoot, ref bestPrefixLength);
            ConsiderRootPrefix(root, root.AssetsDirectory, fullPath, comparison, ref bestRoot, ref bestPrefixLength);
        }

        if (!bestRoot.HasValue)
        {
            return Result.FromFailure($"Refusing to delete '{fullPath}': file is outside writable workspace roots.");
        }

        if (!bestRoot.Value.IsWritable)
        {
            return Result.FromFailure($"Refusing to delete '{fullPath}': file belongs to read-only root '{bestRoot.Value.Id}'.");
        }

        return Result.FromSuccess();
    }

    private static void ConsiderRootPrefix(
        in WorkspaceRoot root,
        string directory,
        string fullFilePath,
        StringComparison comparison,
        ref WorkspaceRoot? bestRoot,
        ref int bestPrefixLength)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        string normalizedDir = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string prefix = normalizedDir + Path.DirectorySeparatorChar;
        if (fullFilePath.StartsWith(prefix, comparison) && prefix.Length > bestPrefixLength)
        {
            bestPrefixLength = prefix.Length;
            bestRoot = root;
        }
    }

    private static List<TomlTableSpan> CollectArrayTables(TomlDocument doc, string tableName)
    {
        List<TomlTableSpan> matches = [];
        for (int i = 0; i < doc.Tables.Count; i++)
        {
            TomlTableSpan table = doc.Tables[i];
            if (table.IsArrayTable && string.Equals(table.HeaderPath, tableName, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(table);
            }
        }

        return matches;
    }

    private static TomlTableSpan? ResolveTargetEntryTable(
        TomlDocument doc,
        IReadOnlyList<TomlTableSpan> entryTables,
        string tableName,
        string? assetId,
        int? tableIndex,
        int? entryIndex)
    {
        bool hasAssetId = !string.IsNullOrWhiteSpace(assetId);

        if (tableIndex.HasValue && tableIndex.Value >= 0 && tableIndex.Value < doc.Tables.Count)
        {
            TomlTableSpan candidate = doc.Tables[tableIndex.Value];
            if (candidate.IsArrayTable &&
                string.Equals(candidate.HeaderPath, tableName, StringComparison.OrdinalIgnoreCase) &&
                (!hasAssetId || EntryMatchesId(doc, candidate.Index, assetId!)))
            {
                return candidate;
            }
        }

        if (entryIndex.HasValue && entryIndex.Value >= 0 && entryIndex.Value < entryTables.Count)
        {
            TomlTableSpan candidate = entryTables[entryIndex.Value];
            if (!hasAssetId || EntryMatchesId(doc, candidate.Index, assetId!))
            {
                return candidate;
            }
        }

        if (hasAssetId)
        {
            for (int i = 0; i < entryTables.Count; i++)
            {
                if (EntryMatchesId(doc, entryTables[i].Index, assetId!))
                {
                    return entryTables[i];
                }
            }
        }

        return null;
    }

    private static bool EntryMatchesId(TomlDocument doc, int tableIndex, string assetId)
    {
        Result<TomlFieldSpan> idField = doc.GetFieldInTable(tableIndex, "ID");
        if (!idField.Success)
        {
            idField = doc.GetFieldInTable(tableIndex, "id");
        }

        return idField.Success &&
               idField.Value.TypedValue is string currentId &&
               string.Equals(currentId, assetId, StringComparison.Ordinal);
    }

    private static int FindLastOwnedTableIndex(TomlDocument doc, int startTableIndex, string tableName)
    {
        string childPrefix = string.Concat(tableName, ".");
        int lastOwned = startTableIndex;

        for (int i = startTableIndex + 1; i < doc.Tables.Count; i++)
        {
            TomlTableSpan next = doc.Tables[i];
            if (next.IsArrayTable && string.Equals(next.HeaderPath, tableName, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (next.HeaderPath.StartsWith(childPrefix, StringComparison.OrdinalIgnoreCase))
            {
                lastOwned = i;
                continue;
            }

            break;
        }

        return lastOwned;
    }

    private static string RemoveSpanPreservingSiblingSpacing(
        string text,
        bool hadTrailingNewline,
        int spanStart,
        int spanEnd)
    {
        int removeStart = Math.Clamp(spanStart, 0, text.Length);
        int removeEnd = Math.Clamp(spanEnd, removeStart, text.Length);

        //  Consume blank lines directly following the removed section, never touching comments.
        removeEnd = AdvanceOverBlankLines(text, removeEnd);

        //  When removing the final section of a file, also trim orphaned blank lines directly before it.
        if (removeEnd >= text.Length)
        {
            removeStart = RewindOverBlankLines(text, removeStart);
            if (!hadTrailingNewline && removeStart > 0 && text[removeStart - 1] == '\n')
            {
                removeStart--;
                if (removeStart > 0 && text[removeStart - 1] == '\r')
                {
                    removeStart--;
                }
            }
        }

        return string.Concat(text.AsSpan(0, removeStart), text.AsSpan(removeEnd));
    }

    private static string CollapseOrphanBlankLineAt(string text, int offset, bool hadTrailingNewline)
    {
        int cursor = Math.Clamp(offset, 0, text.Length);
        int afterBlanks = AdvanceOverBlankLines(text, cursor);

        if (afterBlanks >= text.Length)
        {
            int trimmedStart = RewindOverBlankLines(text, cursor);
            if (!hadTrailingNewline && trimmedStart > 0 && text[trimmedStart - 1] == '\n')
            {
                trimmedStart--;
                if (trimmedStart > 0 && text[trimmedStart - 1] == '\r')
                {
                    trimmedStart--;
                }
            }

            return text.Substring(0, trimmedStart);
        }

        if (afterBlanks > cursor)
        {
            bool precededByBlankOrHeader = IsPrecededByBlankLineOrSectionHeader(text, cursor);
            if (precededByBlankOrHeader)
            {
                return string.Concat(text.AsSpan(0, cursor), text.AsSpan(afterBlanks));
            }
        }

        return text;
    }

    private static bool IsPrecededByBlankLineOrSectionHeader(string text, int lineStart)
    {
        if (lineStart <= 0)
        {
            return true;
        }

        int prevLineEnd = lineStart;
        if (text[prevLineEnd - 1] == '\n')
        {
            prevLineEnd--;
            if (prevLineEnd > 0 && text[prevLineEnd - 1] == '\r')
            {
                prevLineEnd--;
            }
        }

        int prevLineStart = prevLineEnd;
        while (prevLineStart > 0 && text[prevLineStart - 1] != '\n')
        {
            prevLineStart--;
        }

        ReadOnlySpan<char> prevLine = text.AsSpan(prevLineStart, prevLineEnd - prevLineStart).Trim();
        return prevLine.Length == 0 || (prevLine[0] == '[' && prevLine[prevLine.Length - 1] == ']');
    }

    private static int AdvanceOverBlankLines(string text, int offset)
    {
        int cursor = offset;
        while (cursor < text.Length)
        {
            int lineStart = cursor;
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

            if (cursor >= text.Length)
            {
                return text.Length;
            }

            return lineStart;
        }

        return cursor;
    }

    private static int RewindOverBlankLines(string text, int offset)
    {
        int cursor = offset;
        while (cursor > 0)
        {
            int lineEnd = cursor;
            if (text[lineEnd - 1] != '\n')
            {
                break;
            }

            int scan = lineEnd - 1;
            if (scan > 0 && text[scan - 1] == '\r')
            {
                scan--;
            }

            int lineStart = scan;
            while (lineStart > 0 && text[lineStart - 1] != '\n')
            {
                lineStart--;
            }

            bool isBlank = true;
            for (int i = lineStart; i < scan; i++)
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
}
