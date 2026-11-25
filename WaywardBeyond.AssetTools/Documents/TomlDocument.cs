using System.Text;
using Swordfish.Library.Util;

namespace WaywardBeyond.AssetTools.Documents;

/// <summary>
///     Lossless TOML document backed by source text and Tomlyn CST spans for surgical edits.
/// </summary>
public sealed class TomlDocument
{
    private static readonly UTF8Encoding UTF8_NO_BOM = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding UTF8_WITH_BOM = new(encoderShouldEmitUTF8Identifier: true);

    private string _text;
    private readonly string? _sourcePath;
    private TomlDocumentParser.ParseOutput _parsed;

    private TomlDocument(string text, bool hasUtf8Bom, string? sourcePath, TomlDocumentParser.ParseOutput parsed)
    {
        _text = text;
        HasUtf8Bom = hasUtf8Bom;
        _sourcePath = sourcePath;
        _parsed = parsed;
    }

    /// <summary>
    ///     Gets the current full TOML text.
    /// </summary>
    public string Text => _text;

    /// <summary>
    ///     Gets the optional source file path associated with this document.
    /// </summary>
    public string? SourcePath => _sourcePath;

    /// <summary>
    ///     Gets a value indicating whether the source originally carried a UTF-8 byte order mark.
    /// </summary>
    public bool HasUtf8Bom { get; }

    /// <summary>
    ///     Gets the detected dominant line ending (<c>"\n"</c> or <c>"\r\n"</c>).
    /// </summary>
    public string LineEnding => _parsed.LineEnding;

    /// <summary>
    ///     Gets a value indicating whether the text currently ends with a newline.
    /// </summary>
    public bool HasTrailingNewline => _parsed.HasTrailingNewline;

    /// <summary>
    ///     Gets a value indicating whether parsing produced one or more syntax or validation errors.
    /// </summary>
    public bool HasSyntaxErrors => _parsed.HasSyntaxErrors;

    /// <summary>
    ///     Gets all diagnostics reported while parsing the document.
    /// </summary>
    public IReadOnlyList<TomlDiagnostic> Diagnostics => _parsed.Diagnostics;

    /// <summary>
    ///     Gets all key-value fields declared before the first table header.
    /// </summary>
    public IReadOnlyList<TomlFieldSpan> RootFields => _parsed.RootFields;

    /// <summary>
    ///     Gets all table and array-of-tables sections in source order.
    /// </summary>
    public IReadOnlyList<TomlTableSpan> Tables => _parsed.Tables;

    /// <summary>
    ///     Gets all key-value fields in the document in source order.
    /// </summary>
    public IReadOnlyList<TomlFieldSpan> AllFields => _parsed.AllFields;

    /// <summary>
    ///     Parses TOML text into a document without throwing on syntax errors.
    /// </summary>
    public static TomlDocument Parse(string text, string? sourcePath = null)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        TomlDocumentParser.ParseOutput parsed = TomlDocumentParser.Parse(text, sourcePath);
        return new TomlDocument(text, hasUtf8Bom: false, sourcePath, parsed);
    }

    /// <summary>
    ///     Parses UTF-8 TOML bytes into a document while preserving UTF-8 BOM presence.
    /// </summary>
    public static TomlDocument Parse(ReadOnlySpan<byte> utf8Bytes, string? sourcePath = null)
    {
        bool hasBom = utf8Bytes.Length >= 3 &&
                      utf8Bytes[0] == 0xEF &&
                      utf8Bytes[1] == 0xBB &&
                      utf8Bytes[2] == 0xBF;
        ReadOnlySpan<byte> contentBytes = hasBom ? utf8Bytes.Slice(3) : utf8Bytes;
        string text = UTF8_NO_BOM.GetString(contentBytes);
        TomlDocumentParser.ParseOutput parsed = TomlDocumentParser.Parse(text, sourcePath);
        return new TomlDocument(text, hasBom, sourcePath, parsed);
    }

    /// <summary>
    ///     Loads and parses a TOML file from disk.
    /// </summary>
    public static Result<TomlDocument> Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<TomlDocument>.FromFailure("File path is required.");
        }

        if (!File.Exists(filePath))
        {
            return Result<TomlDocument>.FromFailure($"File not found: {filePath}");
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            return Result<TomlDocument>.FromSuccess(Parse(bytes, filePath));
        }
        catch (Exception ex)
        {
            return Result<TomlDocument>.FromFailure(ex);
        }
    }

    /// <summary>
    ///     Encodes the current document text to UTF-8 bytes preserving original BOM presence.
    /// </summary>
    public byte[] ToBytes()
    {
        if (!HasUtf8Bom)
        {
            return UTF8_NO_BOM.GetBytes(_text);
        }

        byte[] preamble = UTF8_WITH_BOM.GetPreamble();
        int bodyByteCount = UTF8_NO_BOM.GetByteCount(_text);
        var buffer = new byte[preamble.Length + bodyByteCount];
        Buffer.BlockCopy(preamble, 0, buffer, 0, preamble.Length);
        UTF8_NO_BOM.GetBytes(_text, 0, _text.Length, buffer, preamble.Length);
        return buffer;
    }

    /// <summary>
    ///     Gets a table by header path and zero-based occurrence index.
    /// </summary>
    public Result<TomlTableSpan> GetTable(string tablePath, int occurrenceIndex = 0)
    {
        if (occurrenceIndex < 0)
        {
            return Result<TomlTableSpan>.FromFailure("Table occurrence index cannot be negative.");
        }

        for (int i = 0; i < _parsed.Tables.Count; i++)
        {
            TomlTableSpan table = _parsed.Tables[i];
            if (string.Equals(table.HeaderPath, tablePath, StringComparison.Ordinal) &&
                table.OccurrenceIndex == occurrenceIndex)
            {
                return Result<TomlTableSpan>.FromSuccess(table);
            }
        }

        return Result<TomlTableSpan>.FromFailure($"Table '{tablePath}'[{occurrenceIndex}] was not found.");
    }

    /// <summary>
    ///     Gets all tables matching <paramref name="tablePath"/> in source order.
    /// </summary>
    public IReadOnlyList<TomlTableSpan> GetTables(string tablePath)
    {
        List<TomlTableSpan> matches = [];
        for (int i = 0; i < _parsed.Tables.Count; i++)
        {
            TomlTableSpan table = _parsed.Tables[i];
            if (string.Equals(table.HeaderPath, tablePath, StringComparison.Ordinal))
            {
                matches.Add(table);
            }
        }

        return matches;
    }

    /// <summary>
    ///     Gets a root-level field by logical key name.
    /// </summary>
    public Result<TomlFieldSpan> GetRootField(string key)
    {
        for (int i = 0; i < _parsed.RootFields.Count; i++)
        {
            if (string.Equals(_parsed.RootFields[i].Key, key, StringComparison.Ordinal))
            {
                return Result<TomlFieldSpan>.FromSuccess(_parsed.RootFields[i]);
            }
        }

        return Result<TomlFieldSpan>.FromFailure($"Root field '{key}' was not found.");
    }

    /// <summary>
    ///     Gets a field inside the specified table header path and occurrence index.
    /// </summary>
    public Result<TomlFieldSpan> GetField(string tablePath, string key, int tableOccurrenceIndex = 0)
    {
        if (string.IsNullOrEmpty(tablePath))
        {
            return GetRootField(key);
        }

        Result<TomlTableSpan> tableResult = GetTable(tablePath, tableOccurrenceIndex);
        if (!tableResult.Success)
        {
            return Result<TomlFieldSpan>.FromFailure(tableResult.Message);
        }

        return GetFieldInTable(tableResult.Value.Index, key);
    }

    /// <summary>
    ///     Gets a field inside the table at <paramref name="tableIndex"/>.
    /// </summary>
    public Result<TomlFieldSpan> GetFieldInTable(int tableIndex, string key)
    {
        if (tableIndex < 0 || tableIndex >= _parsed.Tables.Count)
        {
            return Result<TomlFieldSpan>.FromFailure($"Table index {tableIndex} is out of range.");
        }

        IReadOnlyList<TomlFieldSpan> fields = _parsed.Tables[tableIndex].Fields;
        for (int i = 0; i < fields.Count; i++)
        {
            if (string.Equals(fields[i].Key, key, StringComparison.Ordinal))
            {
                return Result<TomlFieldSpan>.FromSuccess(fields[i]);
            }
        }

        return Result<TomlFieldSpan>.FromFailure($"Field '{key}' was not found in table '{_parsed.Tables[tableIndex].HeaderPath}'.");
    }

    /// <summary>
    ///     Replaces the entire document text directly and re-parses syntax spans and diagnostics.
    /// </summary>
    public void SetRawText(string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        _text = text;
        _parsed = TomlDocumentParser.Parse(text, _sourcePath);
    }

    /// <summary>
    ///     Replaces a field's value span with a raw TOML expression without reformatting other content.
    /// </summary>
    public Result SetRawValue(in TomlFieldSpan field, string rawTomlValue)
    {
        if (HasSyntaxErrors)
        {
            return Result.FromFailure("Cannot apply structured edit to a document with syntax errors.");
        }

        if (string.IsNullOrWhiteSpace(rawTomlValue))
        {
            return Result.FromFailure("Raw TOML value cannot be empty.");
        }

        if (!IsValidSpan(field.ValueSpan))
        {
            return Result.FromFailure("Field value span is out of bounds for the current document.");
        }

        string candidate = TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(field.ValueSpan.Start, field.ValueSpan.Length, rawTomlValue)]);
        return CommitValidatedText(candidate);
    }

    /// <summary>
    ///     Sets a string scalar value on <paramref name="field"/>, preserving single-quote style when valid.
    /// </summary>
    public Result SetString(in TomlFieldSpan field, string value)
    {
        string formatted = TomlValueFormatter.FormatString(value, field.RawValue);
        return SetRawValue(in field, formatted);
    }

    /// <summary>
    ///     Sets an integer scalar value on <paramref name="field"/>.
    /// </summary>
    public Result SetInteger(in TomlFieldSpan field, long value)
    {
        return SetRawValue(in field, TomlValueFormatter.FormatInteger(value));
    }

    /// <summary>
    ///     Sets a floating-point scalar value on <paramref name="field"/>.
    /// </summary>
    public Result SetFloat(in TomlFieldSpan field, double value)
    {
        return SetRawValue(in field, TomlValueFormatter.FormatFloat(value));
    }

    /// <summary>
    ///     Sets a boolean scalar value on <paramref name="field"/>.
    /// </summary>
    public Result SetBoolean(in TomlFieldSpan field, bool value)
    {
        return SetRawValue(in field, TomlValueFormatter.FormatBoolean(value));
    }

    /// <summary>
    ///     Replaces an array field with the provided strings while preserving inline or multi-line layout.
    /// </summary>
    public Result SetStringArray(in TomlFieldSpan field, IReadOnlyList<string> values)
    {
        if (HasSyntaxErrors)
        {
            return Result.FromFailure("Cannot apply structured edit to a document with syntax errors.");
        }

        bool isMultiline = field.RawValue.Contains('\n');
        string itemIndent = isMultiline ? DetectArrayItemIndent(in field) : string.Empty;
        string closeIndent = isMultiline ? DetectArrayCloseIndent(in field) : string.Empty;
        bool trailingComma = isMultiline && DetectArrayTrailingComma(in field);

        string formatted = TomlValueFormatter.FormatStringArray(
            values,
            isMultiline,
            LineEnding,
            itemIndent,
            closeIndent,
            trailingComma);
        return SetRawValue(in field, formatted);
    }

    /// <summary>
    ///     Adds a new root-level key-value field and re-aligns root fields only if previously aligned.
    /// </summary>
    public Result AddRootField(string key, string rawTomlValue)
    {
        return AddRootField([key], rawTomlValue);
    }

    /// <summary>
    ///     Adds a new root-level key-value field using explicit dotted key segments.
    /// </summary>
    public Result AddRootField(IReadOnlyList<string> keySegments, string rawTomlValue)
    {
        if (HasSyntaxErrors)
        {
            return Result.FromFailure("Cannot apply structured edit to a document with syntax errors.");
        }

        if (keySegments.Count == 0 || string.IsNullOrWhiteSpace(rawTomlValue))
        {
            return Result.FromFailure("Key and value are required.");
        }

        string logicalKey = keySegments.Count == 1 ? keySegments[0] : string.Join('.', keySegments);
        if (GetRootField(logicalKey).Success)
        {
            return Result.FromFailure($"Root field '{logicalKey}' already exists.");
        }

        (bool wasAligned, int previousAlignCol) = TomlTableAlignment.Detect(_text, _parsed.RootFields);
        int minPadding = wasAligned ? TomlTableAlignment.ComputeMinPadding(_parsed.RootFields, previousAlignCol) : 1;
        string formattedKey = TomlValueFormatter.FormatDottedKey(keySegments);
        string newEntry = $"{formattedKey} = {rawTomlValue}";

        string candidate = InsertFieldAfterAnchor(
            _parsed.RootFields.Count > 0 ? _parsed.RootFields[_parsed.RootFields.Count - 1].LineSpan : (TomlTextSpan?)null,
            newEntry);

        return CommitWithOptionalRealignment(candidate, tableIndex: -1, wasAligned, minPadding);
    }

    /// <summary>
    ///     Adds a new field to <paramref name="table"/> and re-aligns only that table if previously aligned.
    /// </summary>
    public Result AddField(in TomlTableSpan table, string key, string rawTomlValue)
    {
        return AddField(in table, [key], rawTomlValue);
    }

    /// <summary>
    ///     Adds a new field with explicit dotted key segments to <paramref name="table"/>.
    /// </summary>
    public Result AddField(in TomlTableSpan table, IReadOnlyList<string> keySegments, string rawTomlValue)
    {
        if (HasSyntaxErrors)
        {
            return Result.FromFailure("Cannot apply structured edit to a document with syntax errors.");
        }

        if (table.Index < 0 || table.Index >= _parsed.Tables.Count)
        {
            return Result.FromFailure($"Table index {table.Index} is out of range.");
        }

        if (keySegments.Count == 0 || string.IsNullOrWhiteSpace(rawTomlValue))
        {
            return Result.FromFailure("Key and value are required.");
        }

        TomlTableSpan currentTable = _parsed.Tables[table.Index];
        string logicalKey = keySegments.Count == 1 ? keySegments[0] : string.Join('.', keySegments);
        if (GetFieldInTable(currentTable.Index, logicalKey).Success)
        {
            return Result.FromFailure($"Field '{logicalKey}' already exists in table '{currentTable.HeaderPath}'.");
        }

        string formattedKey = TomlValueFormatter.FormatDottedKey(keySegments);
        string newEntry = $"{formattedKey} = {rawTomlValue}";
        TomlTextSpan anchorLine = currentTable.Fields.Count > 0
            ? currentTable.Fields[currentTable.Fields.Count - 1].LineSpan
            : currentTable.HeaderLineSpan;

        int minPadding = currentTable.IsColumnAligned
            ? TomlTableAlignment.ComputeMinPadding(currentTable.Fields, currentTable.AlignmentColumn)
            : 1;
        string candidate = InsertFieldAfterAnchor(anchorLine, newEntry);
        return CommitWithOptionalRealignment(
            candidate,
            currentTable.Index,
            currentTable.IsColumnAligned,
            minPadding);
    }

    /// <summary>
    ///     Removes a key-value field line and re-aligns its owning table only if previously aligned.
    /// </summary>
    public Result RemoveField(in TomlFieldSpan field)
    {
        if (HasSyntaxErrors)
        {
            return Result.FromFailure("Cannot apply structured edit to a document with syntax errors.");
        }

        if (!IsValidSpan(field.LineSpan))
        {
            return Result.FromFailure("Field line span is out of bounds for the current document.");
        }

        (bool wasAligned, int minPadding) = GetOwnerAlignment(field.TableIndex);

        int removeStart = field.LineSpan.Start;
        int removeEnd = field.LineSpan.End;
        bool lineHadNewline = removeEnd > removeStart && _text[removeEnd - 1] == '\n';
        if (!lineHadNewline && removeStart > 0 && _text[removeStart - 1] == '\n')
        {
            removeStart--;
            if (removeStart > 0 && _text[removeStart - 1] == '\r')
            {
                removeStart--;
            }
        }

        string candidate = TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(removeStart, removeEnd - removeStart, string.Empty)]);
        return CommitWithOptionalRealignment(candidate, field.TableIndex, wasAligned, minPadding);
    }

    /// <summary>
    ///     Appends a raw TOML expression to an array field preserving inline or multi-line layout.
    /// </summary>
    public Result AppendArrayItem(in TomlFieldSpan arrayField, string rawItemValue)
    {
        if (HasSyntaxErrors)
        {
            return Result.FromFailure("Cannot apply structured edit to a document with syntax errors.");
        }

        if (arrayField.ValueKind != TomlValueKind.Array)
        {
            return Result.FromFailure($"Field '{arrayField.Key}' is not an array.");
        }

        if (string.IsNullOrWhiteSpace(rawItemValue))
        {
            return Result.FromFailure("Array item value cannot be empty.");
        }

        if (!IsValidSpan(arrayField.ValueSpan) || arrayField.ValueSpan.Length < 2)
        {
            return Result.FromFailure("Array value span is invalid.");
        }

        int openBracketIndex = arrayField.ValueSpan.Start;
        int closeBracketIndex = arrayField.ValueSpan.End - 1;
        bool isMultiline = arrayField.RawValue.Contains('\n');

        string candidate;
        if (arrayField.ArrayElements.Count == 0)
        {
            if (!isMultiline)
            {
                candidate = TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(openBracketIndex + 1, closeBracketIndex - openBracketIndex - 1, rawItemValue)]);
            }
            else
            {
                string closeIndent = DetectArrayCloseIndent(in arrayField);
                string itemIndent = string.Concat(closeIndent, "    ");
                string insertion = $"{itemIndent}{rawItemValue},{LineEnding}{closeIndent}";
                int lineStartBeforeClose = FindLineStart(closeBracketIndex);
                candidate = TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(lineStartBeforeClose, closeBracketIndex - lineStartBeforeClose, insertion)]);
            }

            return CommitValidatedText(candidate);
        }

        TomlArrayElementSpan lastElement = arrayField.ArrayElements[arrayField.ArrayElements.Count - 1];
        if (!isMultiline)
        {
            if (lastElement.CommaSpan.HasValue)
            {
                int insertAt = lastElement.CommaSpan.Value.End;
                candidate = TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(insertAt, 0, $" {rawItemValue}")]);
            }
            else
            {
                int insertAt = lastElement.ValueSpan.End;
                candidate = TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(insertAt, 0, $", {rawItemValue}")]);
            }

            return CommitValidatedText(candidate);
        }

        string indent = DetectArrayItemIndent(in arrayField);
        int lastLineEndContent = FindLineContentEnd(lastElement.ValueSpan.End);
        if (lastElement.CommaSpan.HasValue)
        {
            string insertion = $"{LineEnding}{indent}{rawItemValue},";
            candidate = TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(lastLineEndContent, 0, insertion)]);
        }
        else
        {
            List<TomlSpanEdit> edits =
            [
                new TomlSpanEdit(lastElement.ValueSpan.End, 0, ","),
                new TomlSpanEdit(lastLineEndContent, 0, $"{LineEnding}{indent}{rawItemValue}")
            ];
            candidate = TomlSpanEdit.ApplyAll(_text, edits);
        }

        return CommitValidatedText(candidate);
    }

    /// <summary>
    ///     Appends a formatted string item to an array field.
    /// </summary>
    public Result AppendArrayStringItem(in TomlFieldSpan arrayField, string value)
    {
        return AppendArrayItem(in arrayField, TomlValueFormatter.FormatBasicString(value));
    }

    /// <summary>
    ///     Removes the array element at <paramref name="index"/> from <paramref name="arrayField"/>.
    /// </summary>
    public Result RemoveArrayItem(in TomlFieldSpan arrayField, int index)
    {
        if (HasSyntaxErrors)
        {
            return Result.FromFailure("Cannot apply structured edit to a document with syntax errors.");
        }

        if (arrayField.ValueKind != TomlValueKind.Array)
        {
            return Result.FromFailure($"Field '{arrayField.Key}' is not an array.");
        }

        if (index < 0 || index >= arrayField.ArrayElements.Count)
        {
            return Result.FromFailure($"Array index {index} is out of range.");
        }

        int count = arrayField.ArrayElements.Count;
        bool isMultiline = arrayField.RawValue.Contains('\n');

        string candidate;
        if (count == 1)
        {
            int innerStart = arrayField.ValueSpan.Start + 1;
            int innerLength = arrayField.ValueSpan.Length - 2;
            candidate = TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(innerStart, innerLength, string.Empty)]);
            return CommitValidatedText(candidate);
        }

        TomlArrayElementSpan target = arrayField.ArrayElements[index];
        if (!isMultiline)
        {
            int removeStart;
            int removeEnd;
            if (index == count - 1)
            {
                TomlArrayElementSpan previous = arrayField.ArrayElements[index - 1];
                removeStart = previous.ValueSpan.End;
                removeEnd = target.CommaSpan.HasValue ? target.CommaSpan.Value.End : target.ValueSpan.End;
            }
            else
            {
                TomlArrayElementSpan next = arrayField.ArrayElements[index + 1];
                removeStart = target.ValueSpan.Start;
                removeEnd = next.ValueSpan.Start;
            }

            candidate = TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(removeStart, removeEnd - removeStart, string.Empty)]);
            return CommitValidatedText(candidate);
        }

        int lineStart = FindLineStart(target.ValueSpan.Start);
        int lineEnd = FindLineEndIncludingNewline(target.CommaSpan.HasValue ? target.CommaSpan.Value.End : target.ValueSpan.End);
        candidate = TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(lineStart, lineEnd - lineStart, string.Empty)]);
        return CommitValidatedText(candidate);
    }

    /// <summary>
    ///     Moves one array element from <paramref name="fromIndex"/> to <paramref name="toIndex"/>.
    /// </summary>
    public Result MoveArrayItem(in TomlFieldSpan arrayField, int fromIndex, int toIndex)
    {
        if (arrayField.ValueKind != TomlValueKind.Array)
        {
            return Result.FromFailure($"Field '{arrayField.Key}' is not an array.");
        }

        int count = arrayField.ArrayElements.Count;
        if (fromIndex < 0 || fromIndex >= count || toIndex < 0 || toIndex >= count)
        {
            return Result.FromFailure("Array move indices are out of range.");
        }

        if (fromIndex == toIndex)
        {
            return Result.FromSuccess();
        }

        List<int> order = new(count);
        for (int i = 0; i < count; i++)
        {
            order.Add(i);
        }

        int moved = order[fromIndex];
        order.RemoveAt(fromIndex);
        order.Insert(toIndex, moved);
        return ReorderArrayItems(in arrayField, order);
    }

    /// <summary>
    ///     Reorders array elements according to the index permutation <paramref name="newOrder"/>.
    /// </summary>
    public Result ReorderArrayItems(in TomlFieldSpan arrayField, IReadOnlyList<int> newOrder)
    {
        if (HasSyntaxErrors)
        {
            return Result.FromFailure("Cannot apply structured edit to a document with syntax errors.");
        }

        if (arrayField.ValueKind != TomlValueKind.Array)
        {
            return Result.FromFailure($"Field '{arrayField.Key}' is not an array.");
        }

        int count = arrayField.ArrayElements.Count;
        if (newOrder.Count != count)
        {
            return Result.FromFailure($"Expected {count} order indices, received {newOrder.Count}.");
        }

        var seen = new bool[count];
        for (int i = 0; i < count; i++)
        {
            int sourceIndex = newOrder[i];
            if (sourceIndex < 0 || sourceIndex >= count || seen[sourceIndex])
            {
                return Result.FromFailure("New array order must be a valid permutation of existing indices.");
            }

            seen[sourceIndex] = true;
        }

        List<TomlSpanEdit> edits = new(count);
        for (int i = 0; i < count; i++)
        {
            TomlArrayElementSpan slot = arrayField.ArrayElements[i];
            string replacementRaw = arrayField.ArrayElements[newOrder[i]].RawValue;
            if (!string.Equals(slot.RawValue, replacementRaw, StringComparison.Ordinal))
            {
                edits.Add(new TomlSpanEdit(slot.ValueSpan.Start, slot.ValueSpan.Length, replacementRaw));
            }
        }

        if (edits.Count == 0)
        {
            return Result.FromSuccess();
        }

        string candidate = TomlSpanEdit.ApplyAll(_text, edits);
        return CommitValidatedText(candidate);
    }

    /// <summary>
    ///     Appends a new <c>[[tablePath]]</c> array-of-tables entry with the specified fields.
    /// </summary>
    public Result<TomlTableSpan> AppendArrayTable(
        string tablePath,
        IReadOnlyList<KeyValuePair<string, string>> rawFields)
    {
        return AppendTableCore(tablePath, isArrayTable: true, rawFields);
    }

    /// <summary>
    ///     Appends a new <c>[tablePath]</c> standard table section with the specified fields.
    /// </summary>
    public Result<TomlTableSpan> AddTable(
        string tablePath,
        IReadOnlyList<KeyValuePair<string, string>> rawFields)
    {
        return AppendTableCore(tablePath, isArrayTable: false, rawFields);
    }

    /// <summary>
    ///     Renames a field's key and re-aligns its owning table only if previously aligned.
    /// </summary>
    public Result RenameKey(in TomlFieldSpan field, string newKey)
    {
        return RenameKey(in field, [newKey]);
    }

    /// <summary>
    ///     Renames a field's key using explicit dotted segments and re-aligns its table only if previously aligned.
    /// </summary>
    public Result RenameKey(in TomlFieldSpan field, IReadOnlyList<string> newKeySegments)
    {
        if (HasSyntaxErrors)
        {
            return Result.FromFailure("Cannot apply structured edit to a document with syntax errors.");
        }

        if (newKeySegments.Count == 0)
        {
            return Result.FromFailure("New key cannot be empty.");
        }

        if (!IsValidSpan(field.KeySpan))
        {
            return Result.FromFailure("Field key span is out of bounds for the current document.");
        }

        (bool wasAligned, int minPadding) = GetOwnerAlignment(field.TableIndex);
        string formattedKey = TomlValueFormatter.FormatDottedKey(newKeySegments);
        string candidate = TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(field.KeySpan.Start, field.KeySpan.Length, formattedKey)]);
        return CommitWithOptionalRealignment(candidate, field.TableIndex, wasAligned, minPadding);
    }

    /// <summary>
    ///     Replaces every non-overlapping span in <paramref name="spans"/> with <paramref name="newRawValue"/>.
    /// </summary>
    public Result<int> ReplaceValueAcrossSpans(IReadOnlyList<TomlTextSpan> spans, string newRawValue)
    {
        if (HasSyntaxErrors)
        {
            return Result<int>.FromFailure("Cannot apply structured edit to a document with syntax errors.");
        }

        if (string.IsNullOrWhiteSpace(newRawValue))
        {
            return Result<int>.FromFailure("Replacement TOML value cannot be empty.");
        }

        if (spans.Count == 0)
        {
            return Result<int>.FromSuccess(0);
        }

        List<TomlSpanEdit> edits = new(spans.Count);
        for (int i = 0; i < spans.Count; i++)
        {
            TomlTextSpan span = spans[i];
            if (!IsValidSpan(span))
            {
                return Result<int>.FromFailure("Replacement span is out of bounds for the current document.");
            }

            edits.Add(new TomlSpanEdit(span.Start, span.Length, newRawValue));
        }

        string candidate;
        try
        {
            candidate = TomlSpanEdit.ApplyAll(_text, edits);
        }
        catch (Exception ex)
        {
            return Result<int>.FromFailure(ex);
        }

        Result commit = CommitValidatedText(candidate);
        return commit.Success
            ? Result<int>.FromSuccess(edits.Count)
            : Result<int>.FromFailure(commit.Message);
    }

    /// <summary>
    ///     Replaces all string scalar values and string array elements equal to <paramref name="oldValue"/>.
    /// </summary>
    public Result<int> RenameStringValue(string oldValue, string newValue)
    {
        if (HasSyntaxErrors)
        {
            return Result<int>.FromFailure("Cannot apply structured edit to a document with syntax errors.");
        }

        List<TomlSpanEdit> edits = [];
        for (int i = 0; i < _parsed.AllFields.Count; i++)
        {
            TomlFieldSpan field = _parsed.AllFields[i];
            if (field.ValueKind == TomlValueKind.String &&
                field.TypedValue is string fieldString &&
                string.Equals(fieldString, oldValue, StringComparison.Ordinal))
            {
                string replacement = TomlValueFormatter.FormatString(newValue, field.RawValue);
                edits.Add(new TomlSpanEdit(field.ValueSpan.Start, field.ValueSpan.Length, replacement));
                continue;
            }

            if (field.ValueKind == TomlValueKind.Array)
            {
                CollectArrayStringRenameEdits(field.ArrayElements, oldValue, newValue, edits);
            }
        }

        if (edits.Count == 0)
        {
            return Result<int>.FromSuccess(0);
        }

        string candidate = TomlSpanEdit.ApplyAll(_text, edits);
        Result commit = CommitValidatedText(candidate);
        return commit.Success
            ? Result<int>.FromSuccess(edits.Count)
            : Result<int>.FromFailure(commit.Message);
    }

    private static void CollectArrayStringRenameEdits(
        IReadOnlyList<TomlArrayElementSpan> elements,
        string oldValue,
        string newValue,
        List<TomlSpanEdit> edits)
    {
        for (int i = 0; i < elements.Count; i++)
        {
            TomlArrayElementSpan element = elements[i];
            if (element.ValueKind != TomlValueKind.String ||
                element.TypedValue is not string elementString ||
                !string.Equals(elementString, oldValue, StringComparison.Ordinal))
            {
                continue;
            }

            string replacement = TomlValueFormatter.FormatString(newValue, element.RawValue);
            edits.Add(new TomlSpanEdit(element.ValueSpan.Start, element.ValueSpan.Length, replacement));
        }
    }

    private Result<TomlTableSpan> AppendTableCore(
        string tablePath,
        bool isArrayTable,
        IReadOnlyList<KeyValuePair<string, string>> rawFields)
    {
        if (HasSyntaxErrors)
        {
            return Result<TomlTableSpan>.FromFailure("Cannot apply structured edit to a document with syntax errors.");
        }

        if (string.IsNullOrWhiteSpace(tablePath))
        {
            return Result<TomlTableSpan>.FromFailure("Table path cannot be empty.");
        }

        IReadOnlyList<TomlTableSpan> existingSamePath = GetTables(tablePath);
        bool alignFields = false;
        for (int i = 0; i < existingSamePath.Count; i++)
        {
            if (existingSamePath[i].IsColumnAligned)
            {
                alignFields = true;
                break;
            }
        }

        int maxKeyLength = 0;
        var formattedKeys = new string[rawFields.Count];
        for (int i = 0; i < rawFields.Count; i++)
        {
            formattedKeys[i] = TomlValueFormatter.FormatKeySegment(rawFields[i].Key);
            if (formattedKeys[i].Length > maxKeyLength)
            {
                maxKeyLength = formattedKeys[i].Length;
            }
        }

        var block = new StringBuilder();
        string header = isArrayTable ? $"[[{tablePath}]]" : $"[{tablePath}]";
        block.Append(header);

        for (int i = 0; i < rawFields.Count; i++)
        {
            block.Append(LineEnding);
            block.Append(formattedKeys[i]);
            int spaces = alignFields ? (maxKeyLength - formattedKeys[i].Length) + 1 : 1;
            block.Append(' ', spaces);
            block.Append("= ");
            block.Append(rawFields[i].Value);
        }

        string prefix;
        if (_text.Length == 0)
        {
            prefix = string.Empty;
        }
        else if (_text.EndsWith("\r\n\r\n", StringComparison.Ordinal) || _text.EndsWith("\n\n", StringComparison.Ordinal))
        {
            prefix = string.Empty;
        }
        else if (_text.EndsWith('\n'))
        {
            prefix = LineEnding;
        }
        else
        {
            prefix = string.Concat(LineEnding, LineEnding);
        }

        string suffix = HasTrailingNewline ? LineEnding : string.Empty;
        string candidate = string.Concat(_text, prefix, block.ToString(), suffix);

        int previousTableCount = _parsed.Tables.Count;
        Result commit = CommitValidatedText(candidate);
        if (!commit.Success)
        {
            return Result<TomlTableSpan>.FromFailure(commit.Message);
        }

        if (_parsed.Tables.Count <= previousTableCount)
        {
            return Result<TomlTableSpan>.FromFailure("Failed to locate newly appended table.");
        }

        return Result<TomlTableSpan>.FromSuccess(_parsed.Tables[_parsed.Tables.Count - 1]);
    }

    private Result CommitValidatedText(string candidate)
    {
        TomlDocumentParser.ParseOutput parsed = TomlDocumentParser.Parse(candidate, _sourcePath);
        if (parsed.HasSyntaxErrors)
        {
            string reason = parsed.Diagnostics.Count > 0
                ? parsed.Diagnostics[0].Message
                : "Edit produced invalid TOML syntax.";
            return Result.FromFailure(reason);
        }

        _text = candidate;
        _parsed = parsed;
        return Result.FromSuccess();
    }

    private Result CommitWithOptionalRealignment(
        string candidate,
        int tableIndex,
        bool wasAligned,
        int minPadding)
    {
        TomlDocumentParser.ParseOutput parsed = TomlDocumentParser.Parse(candidate, _sourcePath);
        if (parsed.HasSyntaxErrors)
        {
            string reason = parsed.Diagnostics.Count > 0
                ? parsed.Diagnostics[0].Message
                : "Edit produced invalid TOML syntax.";
            return Result.FromFailure(reason);
        }

        if (wasAligned)
        {
            IReadOnlyList<TomlFieldSpan> updatedFields = tableIndex < 0
                ? parsed.RootFields
                : tableIndex < parsed.Tables.Count
                    ? parsed.Tables[tableIndex].Fields
                    : [];

            List<TomlSpanEdit> alignmentEdits = TomlTableAlignment.ComputeRealignmentEdits(
                candidate,
                updatedFields,
                minPadding);

            if (alignmentEdits.Count > 0)
            {
                string alignedText = TomlSpanEdit.ApplyAll(candidate, alignmentEdits);
                TomlDocumentParser.ParseOutput alignedParsed = TomlDocumentParser.Parse(alignedText, _sourcePath);
                if (!alignedParsed.HasSyntaxErrors)
                {
                    candidate = alignedText;
                    parsed = alignedParsed;
                }
            }
        }

        _text = candidate;
        _parsed = parsed;
        return Result.FromSuccess();
    }

    private string InsertFieldAfterAnchor(TomlTextSpan? anchorLine, string newEntry)
    {
        if (!anchorLine.HasValue)
        {
            if (_text.Length == 0)
            {
                return newEntry;
            }

            return string.Concat(newEntry, LineEnding, _text);
        }

        TomlTextSpan line = anchorLine.Value;
        bool endsWithNewline = line.End > line.Start && _text[line.End - 1] == '\n';
        if (endsWithNewline)
        {
            string insertion = string.Concat(newEntry, LineEnding);
            return TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(line.End, 0, insertion)]);
        }

        string appended = string.Concat(LineEnding, newEntry);
        return TomlSpanEdit.ApplyAll(_text, [new TomlSpanEdit(line.End, 0, appended)]);
    }

    private (bool IsAligned, int MinPadding) GetOwnerAlignment(int tableIndex)
    {
        if (tableIndex < 0)
        {
            (bool isAligned, int alignCol) = TomlTableAlignment.Detect(_text, _parsed.RootFields);
            int pad = isAligned ? TomlTableAlignment.ComputeMinPadding(_parsed.RootFields, alignCol) : 1;
            return (isAligned, pad);
        }

        if (tableIndex < _parsed.Tables.Count)
        {
            TomlTableSpan owner = _parsed.Tables[tableIndex];
            int pad = owner.IsColumnAligned ? TomlTableAlignment.ComputeMinPadding(owner.Fields, owner.AlignmentColumn) : 1;
            return (owner.IsColumnAligned, pad);
        }

        return (false, 1);
    }

    private bool IsValidSpan(in TomlTextSpan span)
    {
        return span.Start >= 0 && span.Length >= 0 && span.End <= _text.Length;
    }

    private string DetectArrayItemIndent(in TomlFieldSpan arrayField)
    {
        if (arrayField.ArrayElements.Count > 0)
        {
            int valueStart = arrayField.ArrayElements[0].ValueSpan.Start;
            int lineStart = FindLineStart(valueStart);
            return ExtractLeadingWhitespace(lineStart, valueStart);
        }

        return string.Concat(DetectArrayCloseIndent(in arrayField), "    ");
    }

    private string DetectArrayCloseIndent(in TomlFieldSpan arrayField)
    {
        int closeIndex = arrayField.ValueSpan.End - 1;
        int lineStart = FindLineStart(closeIndex);
        return ExtractLeadingWhitespace(lineStart, closeIndex);
    }

    private bool DetectArrayTrailingComma(in TomlFieldSpan arrayField)
    {
        if (arrayField.ArrayElements.Count == 0)
        {
            return true;
        }

        return arrayField.ArrayElements[arrayField.ArrayElements.Count - 1].CommaSpan.HasValue;
    }

    private string ExtractLeadingWhitespace(int start, int limit)
    {
        int cursor = start;
        while (cursor < limit && (_text[cursor] == ' ' || _text[cursor] == '\t'))
        {
            cursor++;
        }

        return _text.Substring(start, cursor - start);
    }

    private int FindLineStart(int offset)
    {
        int cursor = Math.Clamp(offset, 0, _text.Length);
        while (cursor > 0 && _text[cursor - 1] != '\n')
        {
            cursor--;
        }

        return cursor;
    }

    private int FindLineContentEnd(int offset)
    {
        int cursor = Math.Clamp(offset, 0, _text.Length);
        while (cursor < _text.Length && _text[cursor] != '\r' && _text[cursor] != '\n')
        {
            cursor++;
        }

        return cursor;
    }

    private int FindLineEndIncludingNewline(int offset)
    {
        int cursor = Math.Clamp(offset, 0, _text.Length);
        while (cursor < _text.Length && _text[cursor] != '\n')
        {
            cursor++;
        }

        if (cursor < _text.Length && _text[cursor] == '\n')
        {
            cursor++;
        }

        return cursor;
    }
}
