using System.Diagnostics;
using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Storage;
using WaywardBeyond.AssetTools.Workspace;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;

namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Captures, duplicates, and pastes multi-entry TOML, standalone material, and binary blueprint assets.
/// </summary>
public sealed class AssetCloner
{
    private static readonly UTF8Encoding UTF8_NO_BOM = new(encoderShouldEmitUTF8Identifier: false);
    private readonly AtomicFileSaver _atomicSaver;

    /// <summary>
    ///     Creates an asset cloner using <paramref name="atomicSaver"/> for atomic disk writes.
    /// </summary>
    public AssetCloner(AtomicFileSaver? atomicSaver = null)
    {
        _atomicSaver = atomicSaver ?? new AtomicFileSaver();
    }

    /// <summary>
    ///     Captures a self-contained clipboard payload for the specified asset without modifying disk state.
    /// </summary>
    public Result<AssetClipboardPayload> CaptureClipboardPayload(
        WorkspaceIndex workspace,
        AssetKind kind,
        string assetId,
        string absoluteFilePath,
        int tableIndex)
    {
        if (workspace == null)
        {
            return Result<AssetClipboardPayload>.FromFailure("Workspace is required.");
        }

        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Result<AssetClipboardPayload>.FromFailure("Asset identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(absoluteFilePath))
        {
            return Result<AssetClipboardPayload>.FromFailure("Asset file path is required.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(absoluteFilePath);
        }
        catch (Exception ex)
        {
            return Result<AssetClipboardPayload>.FromFailure(ex);
        }

        if (!File.Exists(fullPath))
        {
            return Result<AssetClipboardPayload>.FromFailure($"File not found: '{fullPath}'.");
        }

        WorkspaceRoot? owningRoot = FindOwningRoot(workspace, fullPath);
        if (!owningRoot.HasValue)
        {
            return Result<AssetClipboardPayload>.FromFailure($"File '{fullPath}' is outside enabled workspace roots.");
        }

        string rootId = owningRoot.Value.Id;
        if (kind == AssetKind.Blueprint)
        {
            return CaptureBlueprintPayload(owningRoot.Value, assetId, fullPath);
        }

        if (kind == AssetKind.Material)
        {
            return CaptureMaterialPayload(owningRoot.Value, assetId, fullPath);
        }

        string? tableName = GetArrayTableName(kind);
        if (tableName == null)
        {
            return Result<AssetClipboardPayload>.FromFailure($"Cloning is not supported for asset kind '{kind}'.");
        }

        return CaptureMultiEntryTomlPayload(kind, tableName, rootId, assetId.Trim(), fullPath, tableIndex);
    }

    /// <summary>
    ///     Captures and immediately pastes a duplicate of the specified asset with the next unique identifier.
    /// </summary>
    public Result<AssetCloneResult> DuplicateAsset(
        WorkspaceIndex workspace,
        AssetKind kind,
        string assetId,
        string absoluteFilePath,
        int tableIndex)
    {
        Result<AssetClipboardPayload> captured = CaptureClipboardPayload(workspace, kind, assetId, absoluteFilePath, tableIndex);
        if (!captured.Success)
        {
            return Result<AssetCloneResult>.FromFailure(captured.Message);
        }

        string? targetFile = kind == AssetKind.Material || kind == AssetKind.Blueprint
            ? null
            : captured.Value.SourceFilePath;
        return PasteAsset(workspace, captured.Value, targetFilePath: targetFile, targetRootId: captured.Value.RootId);
    }

    /// <summary>
    ///     Pastes <paramref name="payload"/> into a writable target root or file using a newly allocated identifier.
    /// </summary>
    public Result<AssetCloneResult> PasteAsset(
        WorkspaceIndex workspace,
        AssetClipboardPayload payload,
        string? targetFilePath = null,
        string? targetRootId = null)
    {
        if (workspace == null)
        {
            return Result<AssetCloneResult>.FromFailure("Workspace is required.");
        }

        if (string.IsNullOrWhiteSpace(payload.SourceAssetId))
        {
            return Result<AssetCloneResult>.FromFailure("Clipboard payload is missing a source asset identifier.");
        }

        Result<WorkspaceRoot> targetRootResult = ResolveWritableTargetRoot(workspace, targetFilePath, targetRootId);
        if (!targetRootResult.Success)
        {
            return Result<AssetCloneResult>.FromFailure(targetRootResult.Message);
        }

        WorkspaceRoot targetRoot = targetRootResult.Value;
        if (payload.AssetKind == AssetKind.Blueprint)
        {
            return PasteBlueprint(workspace, payload, targetRoot);
        }

        if (payload.AssetKind == AssetKind.Material)
        {
            return PasteMaterial(workspace, payload, targetRoot);
        }

        string? tableName = GetArrayTableName(payload.AssetKind);
        if (tableName == null)
        {
            return Result<AssetCloneResult>.FromFailure($"Pasting is not supported for asset kind '{payload.AssetKind}'.");
        }

        return PasteMultiEntryToml(workspace, payload, tableName, targetRoot, targetFilePath);
    }

    private static Result<AssetClipboardPayload> CaptureBlueprintPayload(
        in WorkspaceRoot owningRoot,
        string assetId,
        string fullPath)
    {
        string normalizedInputId = assetId.Trim().Replace('\\', '/');
        string? derivedId = TryDeriveRelativeId(owningRoot.AssetsDirectory, "blueprints", ".wbp", fullPath);
        if (derivedId == null || !string.Equals(derivedId, normalizedInputId, StringComparison.OrdinalIgnoreCase))
        {
            return Result<AssetClipboardPayload>.FromFailure(
                $"Blueprint '{assetId}' was not found at '{fullPath}'.");
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            return Result<AssetClipboardPayload>.FromSuccess(new AssetClipboardPayload(
                AssetKind.Blueprint,
                derivedId,
                owningRoot.Id,
                fullPath,
                SourceTableIndex: -1,
                RawTomlText: null,
                RawBytes: bytes));
        }
        catch (Exception ex)
        {
            return Result<AssetClipboardPayload>.FromFailure(ex);
        }
    }

    private static Result<AssetClipboardPayload> CaptureMaterialPayload(
        in WorkspaceRoot owningRoot,
        string assetId,
        string fullPath)
    {
        string normalizedInputId = assetId.Trim().Replace('\\', '/');
        string? derivedId = TryDeriveRelativeId(owningRoot.AssetsDirectory, "materials", ".toml", fullPath);
        if (derivedId == null || !string.Equals(derivedId, normalizedInputId, StringComparison.OrdinalIgnoreCase))
        {
            return Result<AssetClipboardPayload>.FromFailure(
                $"Material '{assetId}' was not found at '{fullPath}'.");
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            TomlDocument doc = TomlDocument.Parse(bytes, fullPath);
            if (doc.HasSyntaxErrors)
            {
                return Result<AssetClipboardPayload>.FromFailure(
                    $"Cannot copy material '{derivedId}' because '{fullPath}' contains TOML syntax errors.");
            }

            return Result<AssetClipboardPayload>.FromSuccess(new AssetClipboardPayload(
                AssetKind.Material,
                derivedId,
                owningRoot.Id,
                fullPath,
                SourceTableIndex: -1,
                RawTomlText: doc.Text,
                RawBytes: bytes));
        }
        catch (Exception ex)
        {
            return Result<AssetClipboardPayload>.FromFailure(ex);
        }
    }

    private static Result<AssetClipboardPayload> CaptureMultiEntryTomlPayload(
        AssetKind kind,
        string tableName,
        string rootId,
        string assetId,
        string fullPath,
        int tableIndex)
    {
        Result<TomlDocument> loaded = TomlDocument.Load(fullPath);
        if (!loaded.Success)
        {
            return Result<AssetClipboardPayload>.FromFailure(loaded.Message);
        }

        TomlDocument doc = loaded.Value;
        if (doc.HasSyntaxErrors)
        {
            return Result<AssetClipboardPayload>.FromFailure(
                $"Cannot copy entry from file with TOML syntax errors: '{fullPath}'.");
        }

        TomlTableSpan? targetTable = ResolveTargetEntryTable(doc, tableName, assetId, tableIndex);
        if (!targetTable.HasValue)
        {
            return Result<AssetClipboardPayload>.FromFailure(
                $"Could not locate {kind} '{assetId}' in '{fullPath}'.");
        }

        int startTableIdx = targetTable.Value.Index;
        int lastOwnedIdx = FindLastOwnedTableIndex(doc, startTableIdx, tableName);
        int blockStart = doc.Tables[startTableIdx].HeaderLineSpan.Start;
        int blockEnd = ComputeEntryBlockEnd(doc, lastOwnedIdx, tableName);

        string rawEntry = doc.Text.Substring(blockStart, Math.Max(0, blockEnd - blockStart))
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .TrimEnd('\n');

        return Result<AssetClipboardPayload>.FromSuccess(new AssetClipboardPayload(
            kind,
            assetId,
            rootId,
            fullPath,
            startTableIdx,
            RawTomlText: rawEntry,
            RawBytes: null));
    }

    private Result<AssetCloneResult> PasteMultiEntryToml(
        WorkspaceIndex workspace,
        in AssetClipboardPayload payload,
        string tableName,
        in WorkspaceRoot targetRoot,
        string? targetFilePath)
    {
        if (string.IsNullOrWhiteSpace(payload.RawTomlText))
        {
            return Result<AssetCloneResult>.FromFailure("Clipboard payload does not contain TOML entry text.");
        }

        Result<string> targetPathResult = ResolveMultiEntryTargetPath(payload, targetRoot, targetFilePath);
        if (!targetPathResult.Success)
        {
            return Result<AssetCloneResult>.FromFailure(targetPathResult.Message);
        }

        string fullTargetPath = targetPathResult.Value;
        bool fileExists = File.Exists(fullTargetPath);
        TomlDocument? existingDoc = null;
        string? baseHash = null;
        HashSet<string> existingFileIds = new(StringComparer.OrdinalIgnoreCase);

        if (fileExists)
        {
            Result<TomlDocument> loaded = TomlDocument.Load(fullTargetPath);
            if (!loaded.Success)
            {
                return Result<AssetCloneResult>.FromFailure(loaded.Message);
            }

            existingDoc = loaded.Value;
            if (existingDoc.HasSyntaxErrors)
            {
                return Result<AssetCloneResult>.FromFailure(
                    $"Cannot paste into file with TOML syntax errors: '{fullTargetPath}'.");
            }

            baseHash = ContentHash.ComputeSha256Hex(existingDoc.ToBytes());
            CollectFileIds(existingDoc, tableName, existingFileIds);
        }

        Result<string> idAlloc = AssetIdAllocator.AllocateNextId(
            workspace,
            payload.AssetKind,
            payload.SourceAssetId,
            existingFileIds);
        if (!idAlloc.Success)
        {
            return Result<AssetCloneResult>.FromFailure(idAlloc.Message);
        }

        string newId = idAlloc.Value;
        Result<string> rewrittenEntryResult = RewriteEntryId(payload.RawTomlText, tableName, newId);
        if (!rewrittenEntryResult.Success)
        {
            return Result<AssetCloneResult>.FromFailure(rewrittenEntryResult.Message);
        }

        string entryBlock = rewrittenEntryResult.Value;
        string updatedText = AppendEntryBlock(existingDoc?.Text ?? string.Empty, entryBlock);
        TomlDocument verified = TomlDocument.Parse(updatedText, fullTargetPath);
        if (verified.HasSyntaxErrors)
        {
            string reason = verified.Diagnostics.Count > 0
                ? verified.Diagnostics[0].Message
                : "Pasted TOML produced syntax errors.";
            return Result<AssetCloneResult>.FromFailure($"Pasting entry produced invalid TOML: {reason}");
        }

        IReadOnlyList<TomlTableSpan> entryTables = verified.GetTables(tableName);
        if (entryTables.Count == 0)
        {
            return Result<AssetCloneResult>.FromFailure($"Pasted entry did not produce a [[{tableName}]] table.");
        }

        TomlTableSpan newTable = entryTables[entryTables.Count - 1];
        int newTableIndex = newTable.Index;
        int newEntryIndex = entryTables.Count - 1;

        byte[] outputBytes;
        if (existingDoc != null)
        {
            existingDoc.SetRawText(updatedText);
            outputBytes = existingDoc.ToBytes();
        }
        else
        {
            outputBytes = UTF8_NO_BOM.GetBytes(updatedText);
        }

        var saveReq = new FileSaveRequest(
            fullTargetPath,
            outputBytes,
            ExpectedBaseSha256: baseHash,
            ExpectFileNotToExist: !fileExists);
        AtomicSaveResult saved = _atomicSaver.Save(in saveReq);
        if (!saved.Success)
        {
            return Result<AssetCloneResult>.FromFailure(saved.ErrorMessage ?? "Failed saving pasted TOML file.");
        }

        return Result<AssetCloneResult>.FromSuccess(new AssetCloneResult(
            payload.AssetKind,
            payload.SourceAssetId,
            newId,
            targetRoot.Id,
            fullTargetPath,
            newTableIndex,
            newEntryIndex,
            CreatedNewFile: !fileExists,
            UpdatedText: updatedText));
    }

    private Result<AssetCloneResult> PasteMaterial(
        WorkspaceIndex workspace,
        in AssetClipboardPayload payload,
        in WorkspaceRoot targetRoot)
    {
        if (payload.RawTomlText == null)
        {
            return Result<AssetCloneResult>.FromFailure("Clipboard payload does not contain material TOML text.");
        }

        string materialsDir = Path.GetFullPath(Path.Combine(targetRoot.AssetsDirectory, "materials"));
        List<string> extraTaken = [];
        Result<string> idAlloc = AllocateUniqueFileBackedId(
            workspace,
            AssetKind.Material,
            payload.SourceAssetId,
            materialsDir,
            ".toml",
            extraTaken);
        if (!idAlloc.Success)
        {
            return Result<AssetCloneResult>.FromFailure(idAlloc.Message);
        }

        string newId = idAlloc.Value;
        string destinationPath = Path.GetFullPath(
            Path.Combine(materialsDir, newId.Replace('/', Path.DirectorySeparatorChar) + ".toml"));

        TomlDocument matDoc = TomlDocument.Parse(payload.RawTomlText, destinationPath);
        if (matDoc.HasSyntaxErrors)
        {
            return Result<AssetCloneResult>.FromFailure("Material clipboard payload contains TOML syntax errors.");
        }

        Result<TomlFieldSpan> idField = matDoc.GetRootField("ID");
        if (idField.Success && idField.Value.ValueKind == TomlValueKind.String)
        {
            Result setId = matDoc.SetString(idField.Value, newId);
            if (!setId.Success)
            {
                return Result<AssetCloneResult>.FromFailure(setId.Message);
            }
        }

        string? parentDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        byte[] bytes = UTF8_NO_BOM.GetBytes(matDoc.Text);
        var saveReq = new FileSaveRequest(destinationPath, bytes, ExpectFileNotToExist: true);
        AtomicSaveResult saved = _atomicSaver.Save(in saveReq);
        if (!saved.Success)
        {
            return Result<AssetCloneResult>.FromFailure(saved.ErrorMessage ?? "Failed saving cloned material.");
        }

        return Result<AssetCloneResult>.FromSuccess(new AssetCloneResult(
            AssetKind.Material,
            payload.SourceAssetId,
            newId,
            targetRoot.Id,
            destinationPath,
            NewTableIndex: -1,
            NewEntryIndex: 0,
            CreatedNewFile: true,
            UpdatedText: matDoc.Text));
    }

    private Result<AssetCloneResult> PasteBlueprint(
        WorkspaceIndex workspace,
        in AssetClipboardPayload payload,
        in WorkspaceRoot targetRoot)
    {
        if (payload.RawBytes == null)
        {
            return Result<AssetCloneResult>.FromFailure("Clipboard payload does not contain blueprint bytes.");
        }

        string blueprintsDir = Path.GetFullPath(Path.Combine(targetRoot.AssetsDirectory, "blueprints"));
        List<string> extraTaken = [];
        Result<string> idAlloc = AllocateUniqueFileBackedId(
            workspace,
            AssetKind.Blueprint,
            payload.SourceAssetId,
            blueprintsDir,
            ".wbp",
            extraTaken);
        if (!idAlloc.Success)
        {
            return Result<AssetCloneResult>.FromFailure(idAlloc.Message);
        }

        string newId = idAlloc.Value;
        string destinationPath = Path.GetFullPath(
            Path.Combine(blueprintsDir, newId.Replace('/', Path.DirectorySeparatorChar) + ".wbp"));

        string? parentDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        byte[] outputBytes = BuildBlueprintCloneBytes(payload.RawBytes, payload.SourceAssetId, newId);
        var saveReq = new FileSaveRequest(destinationPath, outputBytes, ExpectFileNotToExist: true);
        AtomicSaveResult saved = _atomicSaver.Save(in saveReq);
        if (!saved.Success)
        {
            return Result<AssetCloneResult>.FromFailure(saved.ErrorMessage ?? "Failed saving cloned blueprint.");
        }

        return Result<AssetCloneResult>.FromSuccess(new AssetCloneResult(
            AssetKind.Blueprint,
            payload.SourceAssetId,
            newId,
            targetRoot.Id,
            destinationPath,
            NewTableIndex: -1,
            NewEntryIndex: 0,
            CreatedNewFile: true,
            UpdatedText: null));
    }

    private static Result<string> AllocateUniqueFileBackedId(
        WorkspaceIndex workspace,
        AssetKind kind,
        string baseId,
        string targetSubDir,
        string extension,
        List<string> extraTaken)
    {
        while (true)
        {
            Result<string> allocated = AssetIdAllocator.AllocateNextId(workspace, kind, baseId, extraTaken);
            if (!allocated.Success)
            {
                return allocated;
            }

            string candidatePath = Path.GetFullPath(
                Path.Combine(targetSubDir, allocated.Value.Replace('/', Path.DirectorySeparatorChar) + extension));
            if (!File.Exists(candidatePath))
            {
                return allocated;
            }

            extraTaken.Add(allocated.Value);
        }
    }

    private static byte[] BuildBlueprintCloneBytes(byte[] sourceBytes, string sourceId, string newId)
    {
        try
        {
            BlueprintData data = BlueprintData.Deserialize(sourceBytes);
            if (data.SizeX <= 0 || data.SizeY <= 0 || data.SizeZ <= 0 || data.Chunks == null)
            {
                return sourceBytes;
            }

            string updatedName = ComputeClonedBlueprintDisplayName(data.Name, sourceId, newId);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var updated = new BlueprintData(
                data.Version,
                updatedName,
                now,
                now,
                data.BrickCount,
                data.SizeX,
                data.SizeY,
                data.SizeZ,
                data.Chunks,
                data.BrickPalette,
                data.PaintPalette);
            return updated.Serialize();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Blueprint '{newId}' copied as raw payload after deserialize fallback: {ex.Message}");
            return sourceBytes;
        }
    }

    private static string ComputeClonedBlueprintDisplayName(string? currentName, string sourceId, string newId)
    {
        string trimmedName = (currentName ?? string.Empty).Trim();
        string newLeaf = Path.GetFileName(newId.Replace('\\', '/'));
        string oldLeaf = Path.GetFileName(sourceId.Replace('\\', '/'));

        if (trimmedName.Length == 0 ||
            string.Equals(trimmedName, sourceId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmedName, oldLeaf, StringComparison.OrdinalIgnoreCase))
        {
            return ClampBlueprintName(newLeaf);
        }

        string suffix = string.Empty;
        int underscoreIdx = newLeaf.LastIndexOf('_');
        if (underscoreIdx > 0)
        {
            suffix = newLeaf.Substring(underscoreIdx);
        }

        if (suffix.Length == 0)
        {
            return ClampBlueprintName(trimmedName);
        }

        string nameStem = AssetIdAllocator.ExtractBaseStem(trimmedName);
        return ClampBlueprintName(string.Concat(nameStem, suffix));
    }

    private static string ClampBlueprintName(string name)
    {
        return name.Length <= FileBlueprintStorage.MAX_NAME_LENGTH
            ? name
            : name.Substring(0, FileBlueprintStorage.MAX_NAME_LENGTH);
    }

    private static Result<string> RewriteEntryId(string rawEntryText, string tableName, string newId)
    {
        TomlDocument entryDoc = TomlDocument.Parse(rawEntryText);
        if (entryDoc.HasSyntaxErrors)
        {
            return Result<string>.FromFailure("Clipboard TOML entry has syntax errors.");
        }

        if (entryDoc.Tables.Count == 0 ||
            !entryDoc.Tables[0].IsArrayTable ||
            !string.Equals(entryDoc.Tables[0].HeaderPath, tableName, StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.FromFailure($"Clipboard entry does not begin with [[{tableName}]].");
        }

        string defaultIdKey = string.Equals(tableName, "recipes", StringComparison.OrdinalIgnoreCase) ? "id" : "ID";
        Result<TomlFieldSpan> idField = entryDoc.GetFieldInTable(0, defaultIdKey);
        if (!idField.Success)
        {
            idField = entryDoc.GetFieldInTable(0, defaultIdKey == "id" ? "ID" : "id");
        }

        Result editResult = idField.Success
            ? entryDoc.SetString(idField.Value, newId)
            : entryDoc.AddField(entryDoc.Tables[0], defaultIdKey, TomlValueFormatter.FormatBasicString(newId));

        if (!editResult.Success)
        {
            return Result<string>.FromFailure(editResult.Message);
        }

        string normalized = entryDoc.Text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .TrimEnd('\n');
        return Result<string>.FromSuccess(normalized);
    }

    private static string AppendEntryBlock(string existingText, string entryBlock)
    {
        if (existingText.Length == 0)
        {
            return string.Concat(entryBlock, "\n");
        }

        string separator;
        if (existingText.EndsWith("\r\n\r\n", StringComparison.Ordinal) ||
            existingText.EndsWith("\n\n", StringComparison.Ordinal))
        {
            separator = string.Empty;
        }
        else if (existingText.EndsWith('\n'))
        {
            separator = "\n";
        }
        else
        {
            separator = "\n\n";
        }

        return string.Concat(existingText, separator, entryBlock, "\n");
    }

    private static Result<WorkspaceRoot> ResolveWritableTargetRoot(
        WorkspaceIndex workspace,
        string? targetFilePath,
        string? targetRootId)
    {
        WorkspaceRoot? explicitRoot = null;
        if (!string.IsNullOrWhiteSpace(targetRootId))
        {
            for (int i = 0; i < workspace.Roots.Count; i++)
            {
                if (string.Equals(workspace.Roots[i].Id, targetRootId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    explicitRoot = workspace.Roots[i];
                    break;
                }
            }

            if (!explicitRoot.HasValue)
            {
                return Result<WorkspaceRoot>.FromFailure($"Target workspace root '{targetRootId}' was not found.");
            }

            if (!explicitRoot.Value.IsEnabled)
            {
                return Result<WorkspaceRoot>.FromFailure($"Target workspace root '{explicitRoot.Value.Id}' is disabled.");
            }

            if (!explicitRoot.Value.IsWritable)
            {
                return Result<WorkspaceRoot>.FromFailure(
                    $"Refusing to paste into read-only root '{explicitRoot.Value.Id}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(targetFilePath))
        {
            string fullTarget;
            try
            {
                fullTarget = Path.GetFullPath(targetFilePath);
            }
            catch (Exception ex)
            {
                return Result<WorkspaceRoot>.FromFailure(ex);
            }

            WorkspaceRoot? pathRoot = FindOwningRoot(workspace, fullTarget);
            if (!pathRoot.HasValue)
            {
                return Result<WorkspaceRoot>.FromFailure(
                    $"Refusing to paste into '{fullTarget}': path is outside enabled workspace roots.");
            }

            if (!pathRoot.Value.IsWritable)
            {
                return Result<WorkspaceRoot>.FromFailure(
                    $"Refusing to paste into '{fullTarget}': file belongs to read-only root '{pathRoot.Value.Id}'.");
            }

            if (explicitRoot.HasValue &&
                !string.Equals(explicitRoot.Value.Id, pathRoot.Value.Id, StringComparison.OrdinalIgnoreCase))
            {
                return Result<WorkspaceRoot>.FromFailure(
                    $"Target path '{fullTarget}' belongs to root '{pathRoot.Value.Id}', not '{explicitRoot.Value.Id}'.");
            }

            return Result<WorkspaceRoot>.FromSuccess(pathRoot.Value);
        }

        if (explicitRoot.HasValue)
        {
            return Result<WorkspaceRoot>.FromSuccess(explicitRoot.Value);
        }

        return workspace.GetPrimaryWritableRoot();
    }

    private static Result<string> ResolveMultiEntryTargetPath(
        in AssetClipboardPayload payload,
        in WorkspaceRoot targetRoot,
        string? targetFilePath)
    {
        if (!string.IsNullOrWhiteSpace(targetFilePath))
        {
            return Result<string>.FromSuccess(Path.GetFullPath(targetFilePath));
        }

        if (!string.IsNullOrWhiteSpace(payload.SourceFilePath))
        {
            string fullSource = Path.GetFullPath(payload.SourceFilePath);
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string rootPrefix = Path.GetFullPath(targetRoot.RootDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string assetsPrefix = Path.GetFullPath(targetRoot.AssetsDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (fullSource.StartsWith(rootPrefix, comparison) || fullSource.StartsWith(assetsPrefix, comparison))
            {
                return Result<string>.FromSuccess(fullSource);
            }
        }

        string? kindSubDir = GetKindDirectory(payload.AssetKind);
        if (kindSubDir == null)
        {
            return Result<string>.FromFailure($"Unsupported kind '{payload.AssetKind}'.");
        }

        string fileName = !string.IsNullOrWhiteSpace(payload.SourceFilePath)
            ? Path.GetFileName(payload.SourceFilePath)
            : $"{kindSubDir}.toml";
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
        {
            fileName = $"{kindSubDir}.toml";
        }

        return Result<string>.FromSuccess(Path.GetFullPath(Path.Combine(targetRoot.AssetsDirectory, kindSubDir, fileName)));
    }

    private static WorkspaceRoot? FindOwningRoot(WorkspaceIndex workspace, string fullPath)
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

        return bestRoot;
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

    private static string? TryDeriveRelativeId(string assetsDir, string subDir, string extension, string fullPath)
    {
        string folder = Path.GetFullPath(Path.Combine(assetsDir, subDir))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string prefix = folder + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!fullPath.StartsWith(prefix, comparison))
        {
            return null;
        }

        string relative = Path.GetRelativePath(folder, fullPath).Replace('\\', '/');
        if (!relative.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return relative.Substring(0, relative.Length - extension.Length);
    }

    private static TomlTableSpan? ResolveTargetEntryTable(
        TomlDocument doc,
        string tableName,
        string assetId,
        int tableIndex)
    {
        if (tableIndex >= 0 && tableIndex < doc.Tables.Count)
        {
            TomlTableSpan candidate = doc.Tables[tableIndex];
            if (candidate.IsArrayTable &&
                string.Equals(candidate.HeaderPath, tableName, StringComparison.OrdinalIgnoreCase) &&
                EntryMatchesId(doc, candidate.Index, assetId))
            {
                return candidate;
            }
        }

        IReadOnlyList<TomlTableSpan> entryTables = CollectArrayTablesCaseInsensitive(doc, tableName);
        for (int i = 0; i < entryTables.Count; i++)
        {
            if (EntryMatchesId(doc, entryTables[i].Index, assetId))
            {
                return entryTables[i];
            }
        }

        return null;
    }

    private static List<TomlTableSpan> CollectArrayTablesCaseInsensitive(TomlDocument doc, string tableName)
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

    private static int ComputeEntryBlockEnd(TomlDocument doc, int lastOwnedIdx, string tableName)
    {
        int contentEnd = doc.Tables[lastOwnedIdx].ContentSpan.End;
        int limit = lastOwnedIdx + 1 < doc.Tables.Count
            ? doc.Tables[lastOwnedIdx + 1].HeaderLineSpan.Start
            : doc.Text.Length;

        if (contentEnd >= limit)
        {
            return contentEnd;
        }

        //  Include trailing comments that immediately follow the last table without a preceding blank line
        //  when separated from the following sibling section by at least one blank line (or EOF).
        int cursor = contentEnd;
        int lastCommentEnd = contentEnd;
        bool sawBlankAfterComments = limit == doc.Text.Length;

        while (cursor < limit)
        {
            int lineStart = cursor;
            int lineEnd = cursor;
            while (lineEnd < limit && doc.Text[lineEnd] != '\n')
            {
                lineEnd++;
            }

            if (lineEnd < limit && doc.Text[lineEnd] == '\n')
            {
                lineEnd++;
            }

            ReadOnlySpan<char> trimmed = doc.Text.AsSpan(lineStart, lineEnd - lineStart).Trim();
            if (trimmed.Length == 0)
            {
                if (lastCommentEnd > contentEnd)
                {
                    sawBlankAfterComments = true;
                }

                break;
            }

            if (trimmed[0] == '#')
            {
                lastCommentEnd = lineEnd;
                cursor = lineEnd;
                continue;
            }

            break;
        }

        return sawBlankAfterComments && lastCommentEnd > contentEnd ? lastCommentEnd : contentEnd;
    }

    private static void CollectFileIds(TomlDocument doc, string tableName, HashSet<string> ids)
    {
        IReadOnlyList<TomlTableSpan> entryTables = CollectArrayTablesCaseInsensitive(doc, tableName);
        for (int i = 0; i < entryTables.Count; i++)
        {
            Result<TomlFieldSpan> idField = doc.GetFieldInTable(entryTables[i].Index, "ID");
            if (!idField.Success)
            {
                idField = doc.GetFieldInTable(entryTables[i].Index, "id");
            }

            if (idField.Success && idField.Value.TypedValue is string id && !string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }
    }

    private static string? GetArrayTableName(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.Brick => "Bricks",
            AssetKind.Item => "Items",
            AssetKind.Paint => "Paints",
            AssetKind.Skill => "Skills",
            AssetKind.Body => "Bodies",
            AssetKind.Recipe => "Recipes",
            _ => null
        };
    }

    private static string? GetKindDirectory(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.Brick => "bricks",
            AssetKind.Item => "items",
            AssetKind.Paint => "paints",
            AssetKind.Skill => "skills",
            AssetKind.Body => "bodies",
            AssetKind.Recipe => "recipes",
            _ => null
        };
    }
}
