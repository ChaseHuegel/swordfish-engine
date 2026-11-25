using System.Text;
using Swordfish.Library.Util;

namespace WaywardBeyond.AssetTools.Documents;

/// <summary>
///     Explicit house formatter for TOML files used by the format command and never run implicitly on save.
/// </summary>
public static class TomlFormatter
{
    private const string HOUSE_LINE_ENDING = "\n";
    private const string ARRAY_ITEM_INDENT = "    ";

    /// <summary>
    ///     Formats a parsed TOML document into house style while preserving comments and key order.
    /// </summary>
    public static Result<string> Format(TomlDocument document)
    {
        if (document == null)
        {
            return Result<string>.FromFailure("Document is required.");
        }

        if (document.HasSyntaxErrors)
        {
            return Result<string>.FromFailure("Cannot format a TOML document with syntax errors.");
        }

        string source = document.Text;
        if (source.Length == 0)
        {
            return Result<string>.FromSuccess(string.Empty);
        }

        string normalized = NormalizeLineEndingsAndTrailingWhitespace(source);
        TomlDocument working = TomlDocument.Parse(normalized);
        if (working.HasSyntaxErrors)
        {
            return Result<string>.FromFailure("Normalized document failed TOML syntax validation.");
        }

        string aligned = ApplyHouseFieldFormatting(working);
        TomlDocument alignedDoc = TomlDocument.Parse(aligned);
        if (alignedDoc.HasSyntaxErrors)
        {
            return Result<string>.FromFailure("Aligned document failed TOML syntax validation.");
        }

        string spaced = NormalizeSectionSpacing(alignedDoc);

        TomlDocument verified = TomlDocument.Parse(spaced);
        if (verified.HasSyntaxErrors)
        {
            return Result<string>.FromFailure("Formatted output failed TOML syntax validation.");
        }

        return Result<string>.FromSuccess(spaced);
    }

    /// <summary>
    ///     Parses and formats raw TOML text into house style.
    /// </summary>
    public static Result<string> Format(string tomlText, string? sourcePath = null)
    {
        if (tomlText == null)
        {
            return Result<string>.FromFailure("TOML text is required.");
        }

        return Format(TomlDocument.Parse(tomlText, sourcePath));
    }

    private static string ApplyHouseFieldFormatting(TomlDocument document)
    {
        string source = document.Text;
        List<TomlSpanEdit> edits = [];

        CollectSectionEdits(source, document.RootFields, edits);
        for (int i = 0; i < document.Tables.Count; i++)
        {
            CollectSectionEdits(source, document.Tables[i].Fields, edits);
        }

        return edits.Count == 0 ? source : TomlSpanEdit.ApplyAll(source, edits);
    }

    private static void CollectSectionEdits(
        string source,
        IReadOnlyList<TomlFieldSpan> fields,
        List<TomlSpanEdit> edits)
    {
        if (fields.Count == 0)
        {
            return;
        }

        int maxKeyWidth = 0;
        for (int i = 0; i < fields.Count; i++)
        {
            int keyWidth = fields[i].KeySpan.Length;
            if (keyWidth > maxKeyWidth)
            {
                maxKeyWidth = keyWidth;
            }
        }

        bool alignColumns = fields.Count >= 2;
        for (int i = 0; i < fields.Count; i++)
        {
            TomlFieldSpan field = fields[i];
            int desiredBeforeEquals = alignColumns ? (maxKeyWidth - field.KeySpan.Length) + 1 : 1;
            int currentBeforeEquals = field.EqualsSpan.Start - field.KeySpan.End;
            if (currentBeforeEquals != desiredBeforeEquals || !IsOnlySpaces(source, field.KeySpan.End, field.EqualsSpan.Start))
            {
                edits.Add(new TomlSpanEdit(
                    field.KeySpan.End,
                    field.EqualsSpan.Start - field.KeySpan.End,
                    new string(' ', desiredBeforeEquals)));
            }

            int afterEqualsStart = field.EqualsSpan.End;
            int afterEqualsLength = field.ValueSpan.Start - afterEqualsStart;
            if (afterEqualsLength != 1 || source[afterEqualsStart] != ' ')
            {
                edits.Add(new TomlSpanEdit(afterEqualsStart, afterEqualsLength, " "));
            }

            if (field.ValueKind == TomlValueKind.Array && field.RawValue.Contains('\n'))
            {
                string? formattedArray = TryFormatMultilineArray(field);
                if (formattedArray != null && !string.Equals(formattedArray, field.RawValue, StringComparison.Ordinal))
                {
                    edits.Add(new TomlSpanEdit(field.ValueSpan.Start, field.ValueSpan.Length, formattedArray));
                }
            }
        }
    }

    private static string? TryFormatMultilineArray(in TomlFieldSpan arrayField)
    {
        if (arrayField.RawValue.Contains('#'))
        {
            return null;
        }

        if (arrayField.ArrayElements.Count == 0)
        {
            return "[]";
        }

        var builder = new StringBuilder();
        builder.Append('[');
        builder.Append(HOUSE_LINE_ENDING);
        for (int i = 0; i < arrayField.ArrayElements.Count; i++)
        {
            builder.Append(ARRAY_ITEM_INDENT);
            builder.Append(arrayField.ArrayElements[i].RawValue);
            builder.Append(',');
            builder.Append(HOUSE_LINE_ENDING);
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static string NormalizeLineEndingsAndTrailingWhitespace(string source)
    {
        string lf = source.Replace("\r\n", HOUSE_LINE_ENDING, StringComparison.Ordinal)
                          .Replace("\r", HOUSE_LINE_ENDING, StringComparison.Ordinal);
        string[] lines = lf.Split('\n');
        var builder = new StringBuilder(lf.Length + 1);
        int lastNonEmpty = lines.Length - 1;
        while (lastNonEmpty >= 0 && string.IsNullOrWhiteSpace(lines[lastNonEmpty]))
        {
            lastNonEmpty--;
        }

        if (lastNonEmpty < 0)
        {
            return string.Empty;
        }

        for (int i = 0; i <= lastNonEmpty; i++)
        {
            builder.Append(lines[i].TrimEnd(' ', '\t'));
            builder.Append(HOUSE_LINE_ENDING);
        }

        return builder.ToString();
    }

    private static string NormalizeSectionSpacing(TomlDocument document)
    {
        HashSet<int> tableHeaderLines = [];
        for (int i = 0; i < document.Tables.Count; i++)
        {
            tableHeaderLines.Add(document.Tables[i].HeaderSpan.Line - 1);
        }

        string[] lines = document.Text.Split('\n');
        var output = new List<string>(lines.Length);
        bool previousWasBlank = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (i == lines.Length - 1 && line.Length == 0)
            {
                break;
            }

            bool isBlank = line.Length == 0;
            if (isBlank)
            {
                if (!previousWasBlank && output.Count > 0)
                {
                    output.Add(string.Empty);
                    previousWasBlank = true;
                }

                continue;
            }

            bool isTableHeader = tableHeaderLines.Contains(i);
            if (isTableHeader && output.Count > 0 && !previousWasBlank && !output[output.Count - 1].StartsWith('#'))
            {
                output.Add(string.Empty);
            }

            output.Add(line);
            previousWasBlank = false;
        }

        while (output.Count > 0 && output[output.Count - 1].Length == 0)
        {
            output.RemoveAt(output.Count - 1);
        }

        if (output.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(HOUSE_LINE_ENDING, output) + HOUSE_LINE_ENDING;
    }

    private static bool IsOnlySpaces(string source, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if (source[i] != ' ')
            {
                return false;
            }
        }

        return true;
    }
}
