using System.Diagnostics;
using System.Globalization;
using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Editing;
using WaywardBeyond.AssetTools.Storage;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Validation;

/// <summary>
///     Bulk actions available from the validation panel: fix-case, single quick fix, bulk safe fixes, format-file, revalidate, and copy-all.
/// </summary>
public static class ValidationBulkActions
{
    private static readonly UTF8Encoding UTF8_NO_BOM = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding UTF8_WITH_BOM = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    ///     Rewrites all case-mismatched paths in writable files using exact disk casing and returns the fix count.
    /// </summary>
    public static Result<int> FixCase(
        WorkspaceIndex workspace,
        ValidationReport report,
        MultiFileSaver? multiSaver = null)
    {
        if (workspace == null || report == null)
        {
            return Result<int>.FromFailure("Workspace and validation report are required.");
        }

        Dictionary<string, List<(TomlTextSpan Span, string NewValue)>> editsByFile = new(StringComparer.Ordinal);
        for (int i = 0; i < report.Findings.Count; i++)
        {
            ValidationFinding finding = report.Findings[i];
            if (!string.Equals(finding.Code, "PATH001", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(finding.SuggestedFixValue) ||
                !finding.FieldSpan.HasValue ||
                !IsFileWritable(workspace, finding.FilePath))
            {
                continue;
            }

            if (!editsByFile.TryGetValue(finding.FilePath, out List<(TomlTextSpan, string)>? list))
            {
                list = [];
                editsByFile[finding.FilePath] = list;
            }

            list.Add((finding.FieldSpan.Value, finding.SuggestedFixValue));
        }

        if (editsByFile.Count == 0)
        {
            return Result<int>.FromSuccess(0);
        }

        List<FileSaveRequest> saveRequests = [];
        int fixedCount = 0;

        foreach (KeyValuePair<string, List<(TomlTextSpan Span, string NewValue)>> entry in editsByFile)
        {
            Result<TomlDocument> loaded = TomlDocument.Load(entry.Key);
            if (!loaded.Success)
            {
                return Result<int>.FromFailure(loaded.Message);
            }

            TomlDocument doc = loaded.Value;
            string baseHash = ContentHash.ComputeSha256Hex(doc.ToBytes());

            List<(TomlTextSpan Span, string NewValue)> ordered = [.. entry.Value];
            ordered.Sort(static (left, right) => right.Span.Start.CompareTo(left.Span.Start));

            for (int i = 0; i < ordered.Count; i++)
            {
                TomlTextSpan span = ordered[i].Span;
                string? replacement = ComputeReplacementToken(doc, span, ordered[i].NewValue);
                if (replacement == null)
                {
                    return Result<int>.FromFailure("Replacement span is out of bounds or invalid.");
                }

                Result<int> replaced = doc.ReplaceValueAcrossSpans([span], replacement);
                if (!replaced.Success)
                {
                    return Result<int>.FromFailure(replaced.Message);
                }

                fixedCount += replaced.Value;
            }

            saveRequests.Add(new FileSaveRequest(entry.Key, doc.ToBytes(), ExpectedBaseSha256: baseHash));
        }

        if (multiSaver != null)
        {
            MultiFileSaveResult batchResult = multiSaver.SaveAll(saveRequests);
            return batchResult.Success
                ? Result<int>.FromSuccess(fixedCount)
                : Result<int>.FromFailure(batchResult.ErrorMessage);
        }

        var singleSaver = new AtomicFileSaver();
        for (int i = 0; i < saveRequests.Count; i++)
        {
            FileSaveRequest req = saveRequests[i];
            AtomicSaveResult saved = singleSaver.Save(in req);
            if (!saved.Success)
            {
                return Result<int>.FromFailure(saved.ErrorMessage);
            }
        }

        return Result<int>.FromSuccess(fixedCount);
    }

    /// <summary>
    ///     Applies a single automatic quick fix for <paramref name="finding"/> atomically, skipping read-only roots cleanly.
    /// </summary>
    public static Result<bool> ApplySingleFix(
        WorkspaceIndex workspace,
        ValidationFinding finding,
        AtomicFileSaver? saver = null)
    {
        if (workspace == null)
        {
            return Result<bool>.FromFailure("Workspace is required.");
        }

        if (finding.FixKind == ValidationFixKind.None)
        {
            return Result<bool>.FromSuccess(false);
        }

        if (string.IsNullOrWhiteSpace(finding.FilePath))
        {
            return Result<bool>.FromFailure("Finding file path is required.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(finding.FilePath);
        }
        catch (Exception ex)
        {
            return Result<bool>.FromFailure(ex);
        }

        if (!IsFileWritable(workspace, fullPath))
        {
            return Result<bool>.FromSuccess(false);
        }

        AtomicFileSaver atomicSaver = saver ?? new AtomicFileSaver();
        return finding.FixKind switch
        {
            ValidationFixKind.StripUtf8Bom => ApplyStripBom(fullPath, atomicSaver),
            ValidationFixKind.NormalizeLineEndings => ApplyNormalizeLineEndings(fullPath, atomicSaver),
            ValidationFixKind.ReplaceValue => ApplySingleReplaceValue(fullPath, finding, atomicSaver),
            ValidationFixKind.RemoveArrayElement => ApplySingleRemoveArrayElement(fullPath, finding, atomicSaver),
            ValidationFixKind.RemoveField => ApplySingleRemoveField(fullPath, finding, atomicSaver),
            ValidationFixKind.InsertDefaultSkillLevels => ApplyInsertDefaultSkillLevels(fullPath, finding, atomicSaver),
            ValidationFixKind.CreateLocalizationKey => ApplyCreateLocalizationKey(workspace, fullPath, finding, atomicSaver),
            _ => Result<bool>.FromSuccess(false)
        };
    }

    /// <summary>
    ///     Applies all writable, deterministic, non-destructive fixes marked <see cref="ValidationFinding.IsSafeBulkFix"/>.
    /// </summary>
    public static Result<BulkFixResult> FixAllSafeIssues(
        WorkspaceIndex workspace,
        ValidationReport report,
        AtomicFileSaver? saver = null)
    {
        if (workspace == null || report == null)
        {
            return Result<BulkFixResult>.FromFailure("Workspace and validation report are required.");
        }

        return FixAllSafeIssues(workspace, report.Findings, saver);
    }

    /// <summary>
    ///     Applies all writable, deterministic, non-destructive fixes marked <see cref="ValidationFinding.IsSafeBulkFix"/>.
    /// </summary>
    public static Result<BulkFixResult> FixAllSafeIssues(
        WorkspaceIndex workspace,
        IReadOnlyList<ValidationFinding> findings,
        AtomicFileSaver? saver = null)
    {
        if (workspace == null || findings == null)
        {
            return Result<BulkFixResult>.FromFailure("Workspace and findings are required.");
        }

        StringComparer pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        var groupedByFile = new Dictionary<string, List<ValidationFinding>>(pathComparer);
        int skippedReadOnly = 0;
        int skippedCount = 0;

        for (int i = 0; i < findings.Count; i++)
        {
            ValidationFinding finding = findings[i];
            if (!finding.IsSafeBulkFix || finding.FixKind == ValidationFixKind.None)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(finding.FilePath))
            {
                skippedCount++;
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(finding.FilePath);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Skipping invalid validation path '{finding.FilePath}': {ex.Message}");
                skippedCount++;
                continue;
            }

            if (!IsFileWritable(workspace, fullPath))
            {
                skippedReadOnly++;
                continue;
            }

            if (!groupedByFile.TryGetValue(fullPath, out List<ValidationFinding>? list))
            {
                list = [];
                groupedByFile[fullPath] = list;
            }

            list.Add(finding);
        }

        if (groupedByFile.Count == 0)
        {
            return Result<BulkFixResult>.FromSuccess(new BulkFixResult(0, 0, skippedReadOnly, [], skippedCount));
        }

        AtomicFileSaver atomicSaver = saver ?? new AtomicFileSaver();
        List<string> modifiedFiles = [];
        int totalApplied = 0;

        foreach (KeyValuePair<string, List<ValidationFinding>> fileEntry in groupedByFile)
        {
            Result<(int Applied, int Skipped, bool Modified)> fileResult = ApplySafeFixesForFile(
                fileEntry.Key,
                fileEntry.Value,
                atomicSaver);
            if (!fileResult.Success)
            {
                return Result<BulkFixResult>.FromFailure(fileResult.Message);
            }

            totalApplied += fileResult.Value.Applied;
            skippedCount += fileResult.Value.Skipped;
            if (fileResult.Value.Modified)
            {
                modifiedFiles.Add(fileEntry.Key);
            }
        }

        return Result<BulkFixResult>.FromSuccess(new BulkFixResult(
            totalApplied,
            modifiedFiles.Count,
            skippedReadOnly,
            modifiedFiles,
            skippedCount));
    }

    /// <summary>
    ///     Applies house TOML formatting to each file path and atomically writes modified files.
    /// </summary>
    public static Result<int> FormatFiles(IReadOnlyList<string> filePaths, AtomicFileSaver? saver = null)
    {
        if (filePaths == null)
        {
            return Result<int>.FromFailure("File list is required.");
        }

        AtomicFileSaver atomicSaver = saver ?? new AtomicFileSaver();
        int formattedCount = 0;

        for (int i = 0; i < filePaths.Count; i++)
        {
            string filePath = filePaths[i];
            Result<TomlDocument> loaded = TomlDocument.Load(filePath);
            if (!loaded.Success)
            {
                return Result<int>.FromFailure(loaded.Message);
            }

            TomlDocument doc = loaded.Value;
            if (doc.HasSyntaxErrors)
            {
                continue;
            }

            Result<string> formatted = TomlFormatter.Format(doc);
            if (!formatted.Success)
            {
                return Result<int>.FromFailure(formatted.Message);
            }

            byte[] originalBytes = doc.ToBytes();
            byte[] formattedBytes = Encoding.UTF8.GetBytes(formatted.Value);
            if (originalBytes.AsSpan().SequenceEqual(formattedBytes))
            {
                continue;
            }

            var request = new FileSaveRequest(filePath, formattedBytes, ContentHash.ComputeSha256Hex(originalBytes));
            AtomicSaveResult saved = atomicSaver.Save(in request);
            if (!saved.Success)
            {
                return Result<int>.FromFailure(saved.ErrorMessage);
            }

            formattedCount++;
        }

        return Result<int>.FromSuccess(formattedCount);
    }

    /// <summary>
    ///     Reloads <paramref name="current"/> from disk and re-runs full workspace validation.
    /// </summary>
    public static Result<(WorkspaceIndex Workspace, ValidationReport Report)> Revalidate(
        WorkspaceLoader loader,
        WorkspaceValidator validator,
        WorkspaceIndex current)
    {
        if (loader == null || validator == null || current == null)
        {
            return Result<(WorkspaceIndex, ValidationReport)>.FromFailure("Loader, validator, and workspace are required.");
        }

        Result<WorkspaceIndex> reloaded = current.Mode == WorkspaceMode.Repo
            ? loader.LoadRepo(current.RootPath)
            : loader.LoadPack(current.RootPath, current.ReferenceInstallPath);

        if (!reloaded.Success)
        {
            return Result<(WorkspaceIndex, ValidationReport)>.FromFailure(reloaded.Message);
        }

        ValidationReport report = validator.Validate(reloaded.Value);
        return Result<(WorkspaceIndex, ValidationReport)>.FromSuccess((reloaded.Value, report));
    }

    /// <summary>
    ///     Formats the complete validation report as plain text for clipboard export.
    /// </summary>
    public static string CopyAll(ValidationReport report)
    {
        if (report == null)
        {
            return string.Empty;
        }

        return report.FormatPlainText();
    }

    private static Result<bool> ApplyStripBom(string fullPath, AtomicFileSaver atomicSaver)
    {
        if (!File.Exists(fullPath))
        {
            return Result<bool>.FromFailure($"File not found: '{fullPath}'.");
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            if (!HasUtf8Bom(bytes))
            {
                return Result<bool>.FromSuccess(false);
            }

            string baseHash = ContentHash.ComputeSha256Hex(bytes);
            byte[] withoutBom = bytes.AsSpan(3).ToArray();
            var request = new FileSaveRequest(fullPath, withoutBom, ExpectedBaseSha256: baseHash);
            AtomicSaveResult saved = atomicSaver.Save(in request);
            return saved.Success
                ? Result<bool>.FromSuccess(true)
                : Result<bool>.FromFailure(saved.ErrorMessage ?? "Failed stripping UTF-8 BOM.");
        }
        catch (Exception ex)
        {
            return Result<bool>.FromFailure(ex);
        }
    }

    private static Result<bool> ApplyNormalizeLineEndings(string fullPath, AtomicFileSaver atomicSaver)
    {
        if (!File.Exists(fullPath))
        {
            return Result<bool>.FromFailure($"File not found: '{fullPath}'.");
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            bool hasBom = HasUtf8Bom(bytes);
            string baseHash = ContentHash.ComputeSha256Hex(bytes);
            string text = UTF8_NO_BOM.GetString(hasBom ? bytes.AsSpan(3) : bytes.AsSpan());
            string normalized = NormalizeToLf(text);
            if (string.Equals(text, normalized, StringComparison.Ordinal))
            {
                return Result<bool>.FromSuccess(false);
            }

            byte[] output = EncodeUtf8(normalized, includeBom: hasBom);
            var request = new FileSaveRequest(fullPath, output, ExpectedBaseSha256: baseHash);
            AtomicSaveResult saved = atomicSaver.Save(in request);
            return saved.Success
                ? Result<bool>.FromSuccess(true)
                : Result<bool>.FromFailure(saved.ErrorMessage ?? "Failed normalizing line endings.");
        }
        catch (Exception ex)
        {
            return Result<bool>.FromFailure(ex);
        }
    }

    private static Result<bool> ApplySingleReplaceValue(
        string fullPath,
        in ValidationFinding finding,
        AtomicFileSaver atomicSaver)
    {
        if (!finding.FieldSpan.HasValue || string.IsNullOrEmpty(finding.SuggestedFixValue))
        {
            return Result<bool>.FromFailure($"Finding '{finding.Code}' is missing a span or suggested replacement value.");
        }

        Result<TomlDocument> loaded = TomlDocument.Load(fullPath);
        if (!loaded.Success)
        {
            return Result<bool>.FromFailure(loaded.Message);
        }

        TomlDocument doc = loaded.Value;
        if (doc.HasSyntaxErrors)
        {
            return Result<bool>.FromFailure($"Cannot apply fix to '{fullPath}' because it has TOML syntax errors.");
        }

        string baseHash = ContentHash.ComputeSha256Hex(doc.ToBytes());
        TomlTextSpan span = finding.FieldSpan.Value;

        if (TryFindFieldByKeySpan(doc, span, out TomlFieldSpan matchedKeyField))
        {
            string formattedKey = TomlValueFormatter.FormatKeySegment(finding.SuggestedFixValue);
            if (string.Equals(matchedKeyField.RawKey, formattedKey, StringComparison.Ordinal))
            {
                return Result<bool>.FromSuccess(false);
            }

            Result renamed = doc.RenameKey(in matchedKeyField, finding.SuggestedFixValue);
            if (!renamed.Success)
            {
                return Result<bool>.FromFailure(renamed.Message);
            }

            return SaveDocument(fullPath, doc, baseHash, atomicSaver);
        }

        string? replacement = ComputeReplacementToken(doc, span, finding.SuggestedFixValue);
        if (replacement == null)
        {
            return Result<bool>.FromFailure($"Target span in '{fullPath}' is invalid or out of bounds.");
        }

        if (string.Equals(span.Slice(doc.Text), replacement, StringComparison.Ordinal))
        {
            return Result<bool>.FromSuccess(false);
        }

        Result<int> replaced = doc.ReplaceValueAcrossSpans([span], replacement);
        if (!replaced.Success)
        {
            return Result<bool>.FromFailure(replaced.Message);
        }

        return replaced.Value > 0
            ? SaveDocument(fullPath, doc, baseHash, atomicSaver)
            : Result<bool>.FromSuccess(false);
    }

    private static Result<bool> ApplySingleRemoveArrayElement(
        string fullPath,
        in ValidationFinding finding,
        AtomicFileSaver atomicSaver)
    {
        if (!finding.FieldSpan.HasValue)
        {
            return Result<bool>.FromFailure($"Finding '{finding.Code}' is missing a target array element span.");
        }

        Result<TomlDocument> loaded = TomlDocument.Load(fullPath);
        if (!loaded.Success)
        {
            return Result<bool>.FromFailure(loaded.Message);
        }

        TomlDocument doc = loaded.Value;
        if (doc.HasSyntaxErrors)
        {
            return Result<bool>.FromFailure($"Cannot remove array element from '{fullPath}' due to TOML syntax errors.");
        }

        string baseHash = ContentHash.ComputeSha256Hex(doc.ToBytes());
        if (!TryFindArrayElement(doc, finding.FieldSpan.Value, out TomlFieldSpan ownerField, out int elemIdx))
        {
            return Result<bool>.FromFailure($"Target array element could not be located in '{fullPath}'.");
        }

        Result removed = doc.RemoveArrayItem(in ownerField, elemIdx);
        if (!removed.Success)
        {
            return Result<bool>.FromFailure(removed.Message);
        }

        return SaveDocument(fullPath, doc, baseHash, atomicSaver);
    }

    private static Result<bool> ApplySingleRemoveField(
        string fullPath,
        in ValidationFinding finding,
        AtomicFileSaver atomicSaver)
    {
        if (!finding.FieldSpan.HasValue)
        {
            return Result<bool>.FromFailure($"Finding '{finding.Code}' is missing a target field span.");
        }

        Result<TomlDocument> loaded = TomlDocument.Load(fullPath);
        if (!loaded.Success)
        {
            return Result<bool>.FromFailure(loaded.Message);
        }

        TomlDocument doc = loaded.Value;
        if (doc.HasSyntaxErrors)
        {
            return Result<bool>.FromFailure($"Cannot remove field from '{fullPath}' due to TOML syntax errors.");
        }

        string baseHash = ContentHash.ComputeSha256Hex(doc.ToBytes());
        if (!TryFindFieldByAnySpan(doc, finding.FieldSpan.Value, out TomlFieldSpan field))
        {
            return Result<bool>.FromFailure($"Target field could not be located in '{fullPath}'.");
        }

        Result removed = doc.RemoveField(in field);
        if (!removed.Success)
        {
            return Result<bool>.FromFailure(removed.Message);
        }

        return SaveDocument(fullPath, doc, baseHash, atomicSaver);
    }

    private static Result<bool> ApplyInsertDefaultSkillLevels(
        string fullPath,
        in ValidationFinding finding,
        AtomicFileSaver atomicSaver)
    {
        Result<TomlDocument> loaded = TomlDocument.Load(fullPath);
        if (!loaded.Success)
        {
            return Result<bool>.FromFailure(loaded.Message);
        }

        TomlDocument doc = loaded.Value;
        if (doc.HasSyntaxErrors)
        {
            return Result<bool>.FromFailure($"Cannot insert skill levels into '{fullPath}' due to TOML syntax errors.");
        }

        TomlTableSpan? targetSkillTable = FindTargetSkillTable(doc, finding);
        if (!targetSkillTable.HasValue)
        {
            return Result<bool>.FromFailure($"Could not locate [[Skills]] entry at line {finding.Line} in '{fullPath}'.");
        }

        string baseHash = ContentHash.ComputeSha256Hex(doc.ToBytes());
        int skillTableIdx = targetSkillTable.Value.Index;
        int lastOwnedIdx = skillTableIdx;
        TomlTableSpan? existingLevelsTable = null;

        for (int i = skillTableIdx + 1; i < doc.Tables.Count; i++)
        {
            TomlTableSpan sub = doc.Tables[i];
            if (sub.IsArrayTable && string.Equals(sub.HeaderPath, "Skills", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!sub.HeaderPath.StartsWith("Skills.", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            lastOwnedIdx = i;
            if (!sub.IsArrayTable && string.Equals(sub.HeaderPath, "Skills.Levels", StringComparison.OrdinalIgnoreCase))
            {
                existingLevelsTable = sub;
            }
        }

        if (existingLevelsTable.HasValue)
        {
            if (existingLevelsTable.Value.Fields.Count > 0)
            {
                return Result<bool>.FromSuccess(false);
            }

            Result added = doc.AddField(existingLevelsTable.Value, "1", "100");
            if (!added.Success)
            {
                return Result<bool>.FromFailure(added.Message);
            }

            return SaveDocument(fullPath, doc, baseHash, atomicSaver);
        }

        int insertAt = doc.Tables[lastOwnedIdx].ContentSpan.End;
        string nl = doc.LineEnding;
        var insertion = new StringBuilder();

        if (insertAt > 0 && doc.Text[insertAt - 1] != '\n')
        {
            insertion.Append(nl);
        }

        insertion.Append(nl);
        insertion.Append("[Skills.Levels]");
        insertion.Append(nl);
        insertion.Append("1 = 100");
        insertion.Append(nl);

        if (insertAt < doc.Text.Length && doc.Text[insertAt] != '\r' && doc.Text[insertAt] != '\n')
        {
            insertion.Append(nl);
        }

        string candidate = string.Concat(doc.Text.AsSpan(0, insertAt), insertion.ToString(), doc.Text.AsSpan(insertAt));
        TomlDocument verified = TomlDocument.Parse(candidate, fullPath);
        if (verified.HasSyntaxErrors)
        {
            string reason = verified.Diagnostics.Count > 0
                ? verified.Diagnostics[0].Message
                : "Inserting [Skills.Levels] produced invalid TOML.";
            return Result<bool>.FromFailure(reason);
        }

        doc.SetRawText(candidate);
        return SaveDocument(fullPath, doc, baseHash, atomicSaver);
    }

    private static Result<bool> ApplyCreateLocalizationKey(
        WorkspaceIndex workspace,
        string sourceFilePath,
        in ValidationFinding finding,
        AtomicFileSaver atomicSaver)
    {
        string? key = !string.IsNullOrWhiteSpace(finding.SuggestedFixValue)
            ? finding.SuggestedFixValue.Trim()
            : ExtractFirstSingleQuotedToken(finding.Message);
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<bool>.FromFailure("Missing localization key name.");
        }

        WorkspaceRoot? owningRoot = FindOwningRoot(workspace, sourceFilePath);
        if (!owningRoot.HasValue || !owningRoot.Value.IsWritable)
        {
            return Result<bool>.FromSuccess(false);
        }

        if (workspace.HasLocalizationKey(key, locale: "en"))
        {
            return Result<bool>.FromSuccess(false);
        }

        string targetLangFile = ResolveTargetEnglishLocalizationFile(workspace, owningRoot.Value, sourceFilePath);
        bool exists = File.Exists(targetLangFile);
        string? baseHash = null;
        if (exists)
        {
            Result<string?> hashRes = ContentHash.ComputeFileSha256Hex(targetLangFile);
            if (!hashRes.Success)
            {
                return Result<bool>.FromFailure(hashRes.Message);
            }

            baseHash = hashRes.Value;
        }

        string initialText = BuildDefaultLocalizationText(key);
        Result<TomlDocument> updatedDoc = new AssetScaffolder().CreateLocalizationKey(
            targetLangFile,
            "en",
            key,
            initialText);
        if (!updatedDoc.Success)
        {
            return Result<bool>.FromFailure(updatedDoc.Message);
        }

        var saveRequest = new FileSaveRequest(
            targetLangFile,
            updatedDoc.Value.ToBytes(),
            ExpectedBaseSha256: baseHash,
            ExpectFileNotToExist: !exists);
        AtomicSaveResult saved = atomicSaver.Save(in saveRequest);
        return saved.Success
            ? Result<bool>.FromSuccess(true)
            : Result<bool>.FromFailure(saved.ErrorMessage ?? "Failed creating localization key.");
    }

    private static Result<(int Applied, int Skipped, bool Modified)> ApplySafeFixesForFile(
        string fullPath,
        List<ValidationFinding> findings,
        AtomicFileSaver atomicSaver)
    {
        if (!File.Exists(fullPath))
        {
            return Result<(int, int, bool)>.FromFailure($"File not found: '{fullPath}'.");
        }

        byte[] originalBytes;
        try
        {
            originalBytes = File.ReadAllBytes(fullPath);
        }
        catch (Exception ex)
        {
            return Result<(int, int, bool)>.FromFailure(ex);
        }

        string baseHash = ContentHash.ComputeSha256Hex(originalBytes);
        TomlDocument doc = TomlDocument.Parse(originalBytes, fullPath);
        bool wantsStripBom = false;
        bool wantsNormalizeCrlf = false;
        List<SpanFixCandidate> candidates = [];
        int skipped = 0;

        for (int i = 0; i < findings.Count; i++)
        {
            ValidationFinding f = findings[i];
            switch (f.FixKind)
            {
                case ValidationFixKind.StripUtf8Bom:
                    wantsStripBom = true;
                    break;

                case ValidationFixKind.NormalizeLineEndings:
                    wantsNormalizeCrlf = true;
                    break;

                case ValidationFixKind.ReplaceValue:
                case ValidationFixKind.RemoveArrayElement:
                case ValidationFixKind.RemoveField:
                    if (!f.FieldSpan.HasValue)
                    {
                        skipped++;
                        continue;
                    }

                    if (f.FixKind == ValidationFixKind.ReplaceValue && string.IsNullOrEmpty(f.SuggestedFixValue))
                    {
                        skipped++;
                        continue;
                    }

                    candidates.Add(new SpanFixCandidate(f, f.FieldSpan.Value));
                    break;

                default:
                    skipped++;
                    break;
            }
        }

        int applied = 0;
        if (candidates.Count > 0)
        {
            if (doc.HasSyntaxErrors)
            {
                skipped += candidates.Count;
            }
            else
            {
                List<SpanFixCandidate> resolved = SelectNonOverlappingDescending(candidates, ref skipped);
                for (int i = 0; i < resolved.Count; i++)
                {
                    SpanFixCandidate op = resolved[i];
                    switch (op.Finding.FixKind)
                    {
                        case ValidationFixKind.RemoveField:
                            if (!TryFindFieldByAnySpan(doc, op.Span, out TomlFieldSpan removeField))
                            {
                                skipped++;
                                continue;
                            }

                            Result removedField = RemoveFieldWithoutRealignment(doc, in removeField);
                            if (!removedField.Success)
                            {
                                return Result<(int, int, bool)>.FromFailure(removedField.Message);
                            }

                            applied++;
                            break;

                        case ValidationFixKind.RemoveArrayElement:
                            if (!TryFindArrayElement(doc, op.Span, out TomlFieldSpan ownerArray, out int elemIdx))
                            {
                                skipped++;
                                continue;
                            }

                            Result removedElem = doc.RemoveArrayItem(in ownerArray, elemIdx);
                            if (!removedElem.Success)
                            {
                                return Result<(int, int, bool)>.FromFailure(removedElem.Message);
                            }

                            applied++;
                            break;

                        case ValidationFixKind.ReplaceValue:
                            string? replacement = ComputeReplacementToken(doc, op.Span, op.Finding.SuggestedFixValue!);
                            if (replacement == null)
                            {
                                skipped++;
                                continue;
                            }

                            if (string.Equals(op.Span.Slice(doc.Text), replacement, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            Result<int> replaced = doc.ReplaceValueAcrossSpans([op.Span], replacement);
                            if (!replaced.Success)
                            {
                                return Result<(int, int, bool)>.FromFailure(replaced.Message);
                            }

                            applied += replaced.Value;
                            break;
                    }
                }
            }
        }

        string finalText = doc.Text;
        if (wantsNormalizeCrlf)
        {
            string normalized = NormalizeToLf(finalText);
            if (!string.Equals(normalized, finalText, StringComparison.Ordinal))
            {
                finalText = normalized;
                applied++;
            }
        }

        bool includeBom = doc.HasUtf8Bom && !wantsStripBom;
        if (wantsStripBom && doc.HasUtf8Bom)
        {
            applied++;
        }

        byte[] finalBytes = EncodeUtf8(finalText, includeBom);
        if (originalBytes.AsSpan().SequenceEqual(finalBytes))
        {
            return Result<(int, int, bool)>.FromSuccess((applied, skipped, false));
        }

        if (!doc.HasSyntaxErrors)
        {
            TomlDocument verified = TomlDocument.Parse(finalText, fullPath);
            if (verified.HasSyntaxErrors)
            {
                string reason = verified.Diagnostics.Count > 0
                    ? verified.Diagnostics[0].Message
                    : "Bulk safe fixes produced invalid TOML.";
                return Result<(int, int, bool)>.FromFailure(reason);
            }
        }

        var saveRequest = new FileSaveRequest(fullPath, finalBytes, ExpectedBaseSha256: baseHash);
        AtomicSaveResult saved = atomicSaver.Save(in saveRequest);
        if (!saved.Success)
        {
            return Result<(int, int, bool)>.FromFailure(saved.ErrorMessage ?? $"Failed saving '{fullPath}'.");
        }

        return Result<(int, int, bool)>.FromSuccess((applied, skipped, true));
    }

    private static List<SpanFixCandidate> SelectNonOverlappingDescending(
        List<SpanFixCandidate> candidates,
        ref int skipped)
    {
        //  Sort by Span.Start descending, then highest deletion priority first so removals supersede replacements on the same span.
        candidates.Sort(static (left, right) =>
        {
            int startCompare = right.Span.Start.CompareTo(left.Span.Start);
            if (startCompare != 0)
            {
                return startCompare;
            }

            int endCompare = right.Span.End.CompareTo(left.Span.End);
            if (endCompare != 0)
            {
                return endCompare;
            }

            return GetFixPriority(right.Finding.FixKind).CompareTo(GetFixPriority(left.Finding.FixKind));
        });

        List<SpanFixCandidate> accepted = [];
        for (int i = 0; i < candidates.Count; i++)
        {
            SpanFixCandidate candidate = candidates[i];
            bool overlaps = false;
            for (int j = 0; j < accepted.Count; j++)
            {
                TomlTextSpan existing = accepted[j].Span;
                if (candidate.Span.Start < existing.End && existing.Start < candidate.Span.End)
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
            {
                skipped++;
                continue;
            }

            accepted.Add(candidate);
        }

        return accepted;
    }

    private static int GetFixPriority(ValidationFixKind kind)
    {
        return kind switch
        {
            ValidationFixKind.RemoveField => 3,
            ValidationFixKind.RemoveArrayElement => 2,
            ValidationFixKind.ReplaceValue => 1,
            _ => 0
        };
    }

    private static Result RemoveFieldWithoutRealignment(TomlDocument doc, in TomlFieldSpan field)
    {
        if (field.LineSpan.Start < 0 || field.LineSpan.End > doc.Text.Length)
        {
            return Result.FromFailure("Field line span is out of bounds.");
        }

        int removeStart = field.LineSpan.Start;
        int removeEnd = field.LineSpan.End;
        bool lineHadNewline = removeEnd > removeStart && doc.Text[removeEnd - 1] == '\n';
        if (!lineHadNewline && removeStart > 0 && doc.Text[removeStart - 1] == '\n')
        {
            removeStart--;
            if (removeStart > 0 && doc.Text[removeStart - 1] == '\r')
            {
                removeStart--;
            }
        }

        string candidate = string.Concat(doc.Text.AsSpan(0, removeStart), doc.Text.AsSpan(removeEnd));
        TomlDocument verified = TomlDocument.Parse(candidate, doc.SourcePath);
        if (verified.HasSyntaxErrors)
        {
            string reason = verified.Diagnostics.Count > 0
                ? verified.Diagnostics[0].Message
                : "Removing field produced invalid TOML.";
            return Result.FromFailure(reason);
        }

        doc.SetRawText(candidate);
        return Result.FromSuccess();
    }

    private static string? ComputeReplacementToken(TomlDocument doc, in TomlTextSpan span, string suggestedValue)
    {
        if (span.Start < 0 || span.Length < 0 || span.End > doc.Text.Length)
        {
            return null;
        }

        string existingRaw = span.Slice(doc.Text);

        for (int i = 0; i < doc.Tables.Count; i++)
        {
            TomlTableSpan table = doc.Tables[i];
            if (table.HeaderSpan == span)
            {
                List<string> segments = [.. table.HeaderSegments];
                if (segments.Count == 0)
                {
                    segments.Add(suggestedValue);
                }
                else
                {
                    segments[segments.Count - 1] = suggestedValue;
                }

                string formattedPath = TomlValueFormatter.FormatDottedKey(segments);
                return table.IsArrayTable
                    ? string.Concat("[[", formattedPath, "]]")
                    : string.Concat("[", formattedPath, "]");
            }
        }

        if (TryFindFieldByKeySpan(doc, span, out _))
        {
            return TomlValueFormatter.FormatKeySegment(suggestedValue);
        }

        if (TryFindFieldByValueSpan(doc, span, out TomlFieldSpan valueField) &&
            valueField.ValueKind != TomlValueKind.String)
        {
            if (valueField.ValueKind == TomlValueKind.Integer &&
                long.TryParse(suggestedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return suggestedValue;
            }

            if (valueField.ValueKind == TomlValueKind.Float &&
                double.TryParse(suggestedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return suggestedValue;
            }

            if (valueField.ValueKind == TomlValueKind.Boolean &&
                (string.Equals(suggestedValue, "true", StringComparison.Ordinal) ||
                 string.Equals(suggestedValue, "false", StringComparison.Ordinal)))
            {
                return suggestedValue;
            }
        }

        return TomlValueFormatter.FormatString(suggestedValue, existingRaw);
    }

    private static bool TryFindFieldByKeySpan(TomlDocument doc, in TomlTextSpan span, out TomlFieldSpan matched)
    {
        for (int i = 0; i < doc.AllFields.Count; i++)
        {
            if (doc.AllFields[i].KeySpan == span)
            {
                matched = doc.AllFields[i];
                return true;
            }
        }

        matched = default;
        return false;
    }

    private static bool TryFindFieldByValueSpan(TomlDocument doc, in TomlTextSpan span, out TomlFieldSpan matched)
    {
        for (int i = 0; i < doc.AllFields.Count; i++)
        {
            if (doc.AllFields[i].ValueSpan == span)
            {
                matched = doc.AllFields[i];
                return true;
            }
        }

        matched = default;
        return false;
    }

    private static bool TryFindFieldByAnySpan(TomlDocument doc, in TomlTextSpan span, out TomlFieldSpan matched)
    {
        for (int i = 0; i < doc.AllFields.Count; i++)
        {
            TomlFieldSpan f = doc.AllFields[i];
            if (f.LineSpan == span || f.ValueSpan == span || f.KeySpan == span)
            {
                matched = f;
                return true;
            }
        }

        matched = default;
        return false;
    }

    private static bool TryFindArrayElement(
        TomlDocument doc,
        in TomlTextSpan elementSpan,
        out TomlFieldSpan ownerField,
        out int elementIndex)
    {
        for (int i = 0; i < doc.AllFields.Count; i++)
        {
            TomlFieldSpan field = doc.AllFields[i];
            if (field.ValueKind != TomlValueKind.Array)
            {
                continue;
            }

            for (int j = 0; j < field.ArrayElements.Count; j++)
            {
                if (field.ArrayElements[j].ValueSpan == elementSpan)
                {
                    ownerField = field;
                    elementIndex = j;
                    return true;
                }
            }
        }

        ownerField = default;
        elementIndex = -1;
        return false;
    }

    private static TomlTableSpan? FindTargetSkillTable(TomlDocument doc, in ValidationFinding finding)
    {
        if (finding.FieldSpan.HasValue)
        {
            TomlTextSpan span = finding.FieldSpan.Value;
            for (int i = 0; i < doc.Tables.Count; i++)
            {
                TomlTableSpan table = doc.Tables[i];
                if (table.IsArrayTable &&
                    string.Equals(table.HeaderPath, "Skills", StringComparison.OrdinalIgnoreCase) &&
                    (table.HeaderLineSpan == span || table.HeaderSpan == span))
                {
                    return table;
                }
            }
        }

        for (int i = 0; i < doc.Tables.Count; i++)
        {
            TomlTableSpan table = doc.Tables[i];
            if (table.IsArrayTable &&
                string.Equals(table.HeaderPath, "Skills", StringComparison.OrdinalIgnoreCase) &&
                table.HeaderLineSpan.Line == finding.Line)
            {
                return table;
            }
        }

        return null;
    }

    private static Result<bool> SaveDocument(
        string fullPath,
        TomlDocument doc,
        string baseHash,
        AtomicFileSaver atomicSaver)
    {
        var request = new FileSaveRequest(fullPath, doc.ToBytes(), ExpectedBaseSha256: baseHash);
        AtomicSaveResult saved = atomicSaver.Save(in request);
        return saved.Success
            ? Result<bool>.FromSuccess(true)
            : Result<bool>.FromFailure(saved.ErrorMessage ?? $"Failed saving '{fullPath}'.");
    }

    private static string ResolveTargetEnglishLocalizationFile(
        WorkspaceIndex workspace,
        in WorkspaceRoot root,
        string sourceFilePath)
    {
        string preferredFileName = InferPreferredLangFileName(sourceFilePath);
        string preferredPath = Path.GetFullPath(Path.Combine(root.AssetsDirectory, "lang", "en", preferredFileName));
        if (File.Exists(preferredPath))
        {
            return preferredPath;
        }

        string enDir = Path.GetFullPath(Path.Combine(root.AssetsDirectory, "lang", "en"));
        if (Directory.Exists(enDir))
        {
            string? firstExisting = null;
            string[] existingFiles = Directory.GetFiles(enDir, "*.toml", SearchOption.AllDirectories);
            Array.Sort(existingFiles, StringComparer.OrdinalIgnoreCase);
            if (existingFiles.Length > 0)
            {
                firstExisting = Path.GetFullPath(existingFiles[0]);
            }

            if (firstExisting != null)
            {
                return firstExisting;
            }
        }

        for (int i = 0; i < workspace.LocalizationEntries.Count; i++)
        {
            IndexedLocalizationEntry entry = workspace.LocalizationEntries[i];
            if (entry.IsWritable &&
                string.Equals(entry.RootId, root.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Locale, "en", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(entry.FilePath);
            }
        }

        return preferredPath;
    }

    private static string InferPreferredLangFileName(string sourceFilePath)
    {
        string normalized = sourceFilePath.Replace('\\', '/');
        if (normalized.Contains("/items/", StringComparison.OrdinalIgnoreCase))
        {
            return "item.toml";
        }

        if (normalized.Contains("/paints/", StringComparison.OrdinalIgnoreCase))
        {
            return "paint.toml";
        }

        if (normalized.Contains("/skills/", StringComparison.OrdinalIgnoreCase))
        {
            return "skill.toml";
        }

        if (normalized.Contains("/bricks/", StringComparison.OrdinalIgnoreCase))
        {
            return "brick.toml";
        }

        return "strings.toml";
    }

    private static string BuildDefaultLocalizationText(string key)
    {
        string[] parts = key.Split('.', StringSplitOptions.RemoveEmptyEntries);
        string candidate = parts.Length > 0 ? parts[parts.Length - 1] : key;
        if (parts.Length >= 2 &&
            (string.Equals(candidate, "name", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(candidate, "description", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(candidate, "title", StringComparison.OrdinalIgnoreCase)))
        {
            candidate = parts[parts.Length - 2];
        }

        string spaced = candidate.Replace('_', ' ').Replace('-', ' ').Trim();
        if (spaced.Length == 0)
        {
            return key;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
    }

    private static string? ExtractFirstSingleQuotedToken(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return null;
        }

        int start = message.IndexOf('\'');
        if (start < 0 || start + 1 >= message.Length)
        {
            return null;
        }

        int end = message.IndexOf('\'', start + 1);
        if (end <= start + 1)
        {
            return null;
        }

        string token = message.Substring(start + 1, end - start - 1).Trim();
        return token.Length > 0 ? token : null;
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

    private static bool IsFileWritable(WorkspaceIndex workspace, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Invalid path '{filePath}' treated as non-writable: {ex.Message}");
            return false;
        }

        WorkspaceRoot? owningRoot = FindOwningRoot(workspace, full);
        return owningRoot.HasValue && owningRoot.Value.IsWritable;
    }

    private static bool HasUtf8Bom(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }

    private static string NormalizeToLf(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
                   .Replace("\r", "\n", StringComparison.Ordinal);
    }

    private static byte[] EncodeUtf8(string text, bool includeBom)
    {
        if (!includeBom)
        {
            return UTF8_NO_BOM.GetBytes(text);
        }

        byte[] preamble = UTF8_WITH_BOM.GetPreamble();
        int bodyByteCount = UTF8_NO_BOM.GetByteCount(text);
        var buffer = new byte[preamble.Length + bodyByteCount];
        Buffer.BlockCopy(preamble, 0, buffer, 0, preamble.Length);
        UTF8_NO_BOM.GetBytes(text, 0, text.Length, buffer, preamble.Length);
        return buffer;
    }

    private readonly record struct SpanFixCandidate(ValidationFinding Finding, TomlTextSpan Span);
}
