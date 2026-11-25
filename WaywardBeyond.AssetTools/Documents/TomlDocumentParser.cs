using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace WaywardBeyond.AssetTools.Documents;

/// <summary>
///     Parses TOML source text via Tomlyn syntax trees into immutable span models.
/// </summary>
internal static class TomlDocumentParser
{
    /// <summary>
    ///     Result of parsing a TOML document text into diagnostics, root fields, and tables.
    /// </summary>
    public readonly record struct ParseOutput(
        string LineEnding,
        bool HasTrailingNewline,
        bool HasSyntaxErrors,
        IReadOnlyList<TomlDiagnostic> Diagnostics,
        IReadOnlyList<TomlFieldSpan> RootFields,
        IReadOnlyList<TomlTableSpan> Tables,
        IReadOnlyList<TomlFieldSpan> AllFields);

    /// <summary>
    ///     Parses <paramref name="source"/> via Tomlyn without throwing on malformed input.
    /// </summary>
    public static ParseOutput Parse(string source, string? sourcePath)
    {
        string lineEnding = DetectLineEnding(source);
        bool hasTrailingNewline = source.EndsWith('\n');
        int[] lineStarts = BuildLineStarts(source);

        DocumentSyntax documentSyntax;
        try
        {
            documentSyntax = SyntaxParser.Parse(source, sourcePath, validate: true);
        }
        catch (Exception ex)
        {
            var fallbackSpan = CreateSpan(lineStarts, 0, Math.Min(1, source.Length));
            var fallbackDiagnostic = new TomlDiagnostic(fallbackSpan, ex.Message, IsError: true);
            return new ParseOutput(
                lineEnding,
                hasTrailingNewline,
                HasSyntaxErrors: true,
                Diagnostics: [fallbackDiagnostic],
                RootFields: [],
                Tables: [],
                AllFields: []);
        }

        List<TomlDiagnostic> diagnostics = [];
        bool hasErrors = documentSyntax.HasErrors;
        foreach (DiagnosticMessage message in documentSyntax.Diagnostics)
        {
            bool isError = message.Kind == DiagnosticMessageKind.Error;
            if (isError)
            {
                hasErrors = true;
            }

            TomlTextSpan diagnosticSpan = MapSourceSpan(source, lineStarts, message.Span);
            diagnostics.Add(new TomlDiagnostic(diagnosticSpan, message.Message ?? "TOML syntax error.", isError));
        }

        List<TomlFieldSpan> rootFields = [];
        List<TomlFieldSpan> allFields = [];
        foreach (KeyValueSyntax keyValue in documentSyntax.KeyValues)
        {
            TomlFieldSpan? parsedField = MapField(source, lineStarts, string.Empty, -1, keyValue);
            if (parsedField.HasValue)
            {
                rootFields.Add(parsedField.Value);
                allFields.Add(parsedField.Value);
            }
        }

        List<TomlTableSpan> tables = [];
        Dictionary<string, int> occurrenceCounts = new(StringComparer.Ordinal);
        int tableCount = documentSyntax.Tables.ChildrenCount;
        for (int tableIndex = 0; tableIndex < tableCount; tableIndex++)
        {
            TableSyntaxBase? tableSyntax = documentSyntax.Tables.GetChild(tableIndex);
            if (tableSyntax == null)
            {
                continue;
            }
            bool isArrayTable = tableSyntax is TableArraySyntax;
            (string headerPath, IReadOnlyList<string> headerSegments) = ExtractKeyPath(tableSyntax.Name);

            int occurrenceIndex = occurrenceCounts.TryGetValue(headerPath, out int existingCount)
                ? existingCount
                : 0;
            occurrenceCounts[headerPath] = occurrenceIndex + 1;

            TomlTextSpan headerSpan = MapTableHeaderSpan(source, lineStarts, tableSyntax);
            TomlTextSpan headerLineSpan = ExpandToFullLine(source, lineStarts, headerSpan.Start, headerSpan.End);

            List<TomlFieldSpan> tableFields = [];
            foreach (KeyValueSyntax keyValue in tableSyntax.Items)
            {
                TomlFieldSpan? parsedField = MapField(source, lineStarts, headerPath, tableIndex, keyValue);
                if (parsedField.HasValue)
                {
                    tableFields.Add(parsedField.Value);
                    allFields.Add(parsedField.Value);
                }
            }

            int contentEnd = tableFields.Count > 0
                ? tableFields[tableFields.Count - 1].LineSpan.End
                : headerLineSpan.End;
            TomlTextSpan contentSpan = CreateSpan(lineStarts, headerLineSpan.Start, Math.Max(0, contentEnd - headerLineSpan.Start));

            (bool isAligned, int alignCol) = TomlTableAlignment.Detect(source, tableFields);
            tables.Add(new TomlTableSpan(
                tableIndex,
                headerPath,
                headerSegments,
                isArrayTable,
                occurrenceIndex,
                headerSpan,
                headerLineSpan,
                contentSpan,
                tableFields,
                isAligned,
                alignCol));
        }

        return new ParseOutput(
            lineEnding,
            hasTrailingNewline,
            hasErrors,
            diagnostics,
            rootFields,
            tables,
            allFields);
    }

    /// <summary>
    ///     Computes a one-based line and column span for a character range using precomputed line starts.
    /// </summary>
    public static TomlTextSpan CreateSpan(int[] lineStarts, int start, int length)
    {
        int clampedStart = Math.Max(0, start);
        int clampedLength = Math.Max(0, length);
        (int line, int column) = GetLineAndColumn(lineStarts, clampedStart);
        return new TomlTextSpan(clampedStart, clampedLength, line, column);
    }

    /// <summary>
    ///     Builds a sorted array of zero-based character offsets for the start of each line.
    /// </summary>
    public static int[] BuildLineStarts(string source)
    {
        List<int> starts = [0];
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return [.. starts];
    }

    private static TomlFieldSpan? MapField(
        string source,
        int[] lineStarts,
        string tablePath,
        int tableIndex,
        KeyValueSyntax keyValue)
    {
        if (keyValue.Key == null || keyValue.EqualToken == null || keyValue.Value == null)
        {
            return null;
        }

        TomlTextSpan keySpan = MapKeySpan(source, lineStarts, keyValue.Key);
        TomlTextSpan equalsSpan = MapTokenSpan(source, lineStarts, keyValue.EqualToken);
        TomlTextSpan valueSpan = MapValueSpan(source, lineStarts, keyValue.Value);
        if (keySpan.Length == 0 || equalsSpan.Length == 0 || valueSpan.Length == 0)
        {
            return null;
        }

        TomlTextSpan lineSpan = ExpandToFullLine(source, lineStarts, keySpan.Start, valueSpan.End);
        (string keyPath, IReadOnlyList<string> keySegments) = ExtractKeyPath(keyValue.Key);
        string rawKey = keySpan.Slice(source);
        string rawValue = valueSpan.Slice(source);
        (TomlValueKind kind, object? typedValue, IReadOnlyList<TomlArrayElementSpan> arrayElements) =
            ExtractValue(source, lineStarts, keyValue.Value);

        return new TomlFieldSpan(
            tablePath,
            tableIndex,
            keyPath,
            keySegments,
            rawKey,
            keySpan,
            equalsSpan,
            valueSpan,
            lineSpan,
            rawValue,
            kind,
            typedValue,
            arrayElements);
    }

    private static (TomlValueKind Kind, object? TypedValue, IReadOnlyList<TomlArrayElementSpan> ArrayElements)
        ExtractValue(string source, int[] lineStarts, ValueSyntax valueSyntax)
    {
        switch (valueSyntax)
        {
            case StringValueSyntax stringSyntax:
                return (TomlValueKind.String, stringSyntax.Value, []);

            case IntegerValueSyntax intSyntax:
                return (TomlValueKind.Integer, intSyntax.Value, []);

            case FloatValueSyntax floatSyntax:
                return (TomlValueKind.Float, floatSyntax.Value, []);

            case BooleanValueSyntax boolSyntax:
                return (TomlValueKind.Boolean, boolSyntax.Value, []);

            case DateTimeValueSyntax dateSyntax:
                return (TomlValueKind.DateTime, dateSyntax.Value, []);

            case InlineTableSyntax:
                return (TomlValueKind.InlineTable, null, []);

            case ArraySyntax arraySyntax:
                return ExtractArrayValue(source, lineStarts, arraySyntax);

            default:
                return (TomlValueKind.Unknown, null, []);
        }
    }

    private static (TomlValueKind Kind, object? TypedValue, IReadOnlyList<TomlArrayElementSpan> ArrayElements)
        ExtractArrayValue(string source, int[] lineStarts, ArraySyntax arraySyntax)
    {
        List<TomlArrayElementSpan> elements = [];
        List<object?> typedItems = [];

        int count = arraySyntax.Items.ChildrenCount;
        for (int i = 0; i < count; i++)
        {
            ArrayItemSyntax? item = arraySyntax.Items.GetChild(i);
            if (item == null || item.Value == null)
            {
                continue;
            }

            TomlTextSpan elemValueSpan = MapValueSpan(source, lineStarts, item.Value);
            TomlTextSpan? commaSpan = item.Comma != null
                ? MapTokenSpan(source, lineStarts, item.Comma)
                : null;
            (TomlValueKind elemKind, object? elemTyped, _) = ExtractValue(source, lineStarts, item.Value);
            string elemRaw = elemValueSpan.Slice(source);

            elements.Add(new TomlArrayElementSpan(
                elements.Count,
                elemValueSpan,
                commaSpan,
                elemRaw,
                elemKind,
                elemTyped));
            typedItems.Add(elemTyped);
        }

        return (TomlValueKind.Array, typedItems, elements);
    }

    private static (string Path, IReadOnlyList<string> Segments) ExtractKeyPath(KeySyntax? keySyntax)
    {
        if (keySyntax == null || keySyntax.Key == null)
        {
            return (string.Empty, []);
        }

        List<string> segments = [ExtractKeySegmentName(keySyntax.Key)];
        int dotCount = keySyntax.DotKeys.ChildrenCount;
        for (int i = 0; i < dotCount; i++)
        {
            DottedKeyItemSyntax? dotItem = keySyntax.DotKeys.GetChild(i);
            if (dotItem != null && dotItem.Key != null)
            {
                segments.Add(ExtractKeySegmentName(dotItem.Key));
            }
        }

        string path = segments.Count == 1 ? segments[0] : string.Join('.', segments);
        return (path, segments);
    }

    private static string ExtractKeySegmentName(BareKeyOrStringValueSyntax segment)
    {
        return segment switch
        {
            BareKeySyntax bare => bare.Key?.Text ?? string.Empty,
            StringValueSyntax quoted => quoted.Value ?? string.Empty,
            _ => segment.ToString()
        };
    }

    private static TomlTextSpan MapTableHeaderSpan(string source, int[] lineStarts, TableSyntaxBase tableSyntax)
    {
        if (tableSyntax.OpenBracket != null && tableSyntax.CloseBracket != null)
        {
            TomlTextSpan openSpan = MapTokenSpan(source, lineStarts, tableSyntax.OpenBracket);
            TomlTextSpan closeSpan = MapTokenSpan(source, lineStarts, tableSyntax.CloseBracket);
            int length = Math.Max(0, closeSpan.End - openSpan.Start);
            return CreateSpan(lineStarts, openSpan.Start, length);
        }

        return MapSourceSpan(source, lineStarts, tableSyntax.Span);
    }

    private static TomlTextSpan MapKeySpan(string source, int[] lineStarts, KeySyntax keySyntax)
    {
        SyntaxToken? firstToken = GetKeySegmentToken(keySyntax.Key);
        SyntaxToken? lastToken = firstToken;
        int dotCount = keySyntax.DotKeys.ChildrenCount;
        if (dotCount > 0)
        {
            DottedKeyItemSyntax? lastDot = keySyntax.DotKeys.GetChild(dotCount - 1);
            SyntaxToken? dotLast = lastDot != null ? GetKeySegmentToken(lastDot.Key) : null;
            if (dotLast != null)
            {
                lastToken = dotLast;
            }
        }

        if (firstToken != null && lastToken != null)
        {
            TomlTextSpan startSpan = MapTokenSpan(source, lineStarts, firstToken);
            TomlTextSpan endSpan = MapTokenSpan(source, lineStarts, lastToken);
            return CreateSpan(lineStarts, startSpan.Start, Math.Max(0, endSpan.End - startSpan.Start));
        }

        return MapSourceSpan(source, lineStarts, keySyntax.Span);
    }

    private static SyntaxToken? GetKeySegmentToken(BareKeyOrStringValueSyntax? segment)
    {
        return segment switch
        {
            BareKeySyntax bare => bare.Key,
            StringValueSyntax quoted => quoted.Token,
            _ => null
        };
    }

    private static TomlTextSpan MapValueSpan(string source, int[] lineStarts, ValueSyntax valueSyntax)
    {
        switch (valueSyntax)
        {
            case StringValueSyntax stringSyntax when stringSyntax.Token != null:
                return MapTokenSpan(source, lineStarts, stringSyntax.Token);

            case IntegerValueSyntax intSyntax when intSyntax.Token != null:
                return MapTokenSpan(source, lineStarts, intSyntax.Token);

            case FloatValueSyntax floatSyntax when floatSyntax.Token != null:
                return MapTokenSpan(source, lineStarts, floatSyntax.Token);

            case BooleanValueSyntax boolSyntax when boolSyntax.Token != null:
                return MapTokenSpan(source, lineStarts, boolSyntax.Token);

            case DateTimeValueSyntax dateSyntax when dateSyntax.Token != null:
                return MapTokenSpan(source, lineStarts, dateSyntax.Token);

            case ArraySyntax arraySyntax when arraySyntax.OpenBracket != null && arraySyntax.CloseBracket != null:
            {
                TomlTextSpan openSpan = MapTokenSpan(source, lineStarts, arraySyntax.OpenBracket);
                TomlTextSpan closeSpan = MapTokenSpan(source, lineStarts, arraySyntax.CloseBracket);
                return CreateSpan(lineStarts, openSpan.Start, Math.Max(0, closeSpan.End - openSpan.Start));
            }

            case InlineTableSyntax inlineTable when inlineTable.OpenBrace != null && inlineTable.CloseBrace != null:
            {
                TomlTextSpan openSpan = MapTokenSpan(source, lineStarts, inlineTable.OpenBrace);
                TomlTextSpan closeSpan = MapTokenSpan(source, lineStarts, inlineTable.CloseBrace);
                return CreateSpan(lineStarts, openSpan.Start, Math.Max(0, closeSpan.End - openSpan.Start));
            }

            default:
                return MapSourceSpan(source, lineStarts, valueSyntax.Span);
        }
    }

    private static TomlTextSpan MapTokenSpan(string source, int[] lineStarts, SyntaxToken token)
    {
        int offset = Math.Clamp(token.Span.Offset, 0, source.Length);
        int length = token.Text != null && offset + token.Text.Length <= source.Length
            ? token.Text.Length
            : Math.Clamp(token.Span.Length, 0, source.Length - offset);
        return CreateSpan(lineStarts, offset, length);
    }

    private static TomlTextSpan MapSourceSpan(string source, int[] lineStarts, SourceSpan span)
    {
        int offset = Math.Clamp(span.Offset, 0, source.Length);
        int length = Math.Clamp(span.Length, 0, source.Length - offset);
        return CreateSpan(lineStarts, offset, length);
    }

    private static TomlTextSpan ExpandToFullLine(string source, int[] lineStarts, int startOffset, int endOffset)
    {
        int lineStart = startOffset;
        while (lineStart > 0 && source[lineStart - 1] != '\n')
        {
            lineStart--;
        }

        int lineEnd = endOffset;
        while (lineEnd < source.Length && source[lineEnd] != '\n')
        {
            lineEnd++;
        }

        if (lineEnd < source.Length && source[lineEnd] == '\n')
        {
            lineEnd++;
        }

        return CreateSpan(lineStarts, lineStart, Math.Max(0, lineEnd - lineStart));
    }

    private static (int Line, int Column) GetLineAndColumn(int[] lineStarts, int offset)
    {
        int index = Array.BinarySearch(lineStarts, offset);
        if (index < 0)
        {
            index = (~index) - 1;
        }

        if (index < 0)
        {
            index = 0;
        }

        int line = index + 1;
        int column = (offset - lineStarts[index]) + 1;
        return (line, column);
    }

    private static string DetectLineEnding(string source)
    {
        int newlineIndex = source.IndexOf('\n');
        if (newlineIndex > 0 && source[newlineIndex - 1] == '\r')
        {
            return "\r\n";
        }

        return "\n";
    }
}
