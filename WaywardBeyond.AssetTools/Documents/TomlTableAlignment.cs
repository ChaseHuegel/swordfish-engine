namespace WaywardBeyond.AssetTools.Documents;

/// <summary>
///     Detects and re-applies column alignment on equals signs within an edited TOML table.
/// </summary>
internal static class TomlTableAlignment
{
    /// <summary>
    ///     Analyzes whether a table's fields have column-aligned equals tokens and returns the column offset.
    /// </summary>
    public static (bool IsAligned, int AlignmentColumn) Detect(string source, IReadOnlyList<TomlFieldSpan> fields)
    {
        if (fields.Count == 0)
        {
            return (false, 0);
        }

        var equalsColumns = new int[fields.Count];
        var keyEndColumns = new int[fields.Count];
        var spaceCounts = new int[fields.Count];
        bool hasPaddedField = false;

        for (int i = 0; i < fields.Count; i++)
        {
            TomlFieldSpan field = fields[i];
            int lineStart = field.LineSpan.Start;
            int gapStart = field.KeySpan.End;
            int gapEnd = field.EqualsSpan.Start;

            if (gapEnd <= gapStart || !IsOnlySpaces(source, gapStart, gapEnd))
            {
                return (false, 0);
            }

            int spaces = gapEnd - gapStart;
            equalsColumns[i] = gapEnd - lineStart;
            keyEndColumns[i] = gapStart - lineStart;
            spaceCounts[i] = spaces;
            if (spaces >= 2)
            {
                hasPaddedField = true;
            }
        }

        if (!hasPaddedField)
        {
            return (false, 0);
        }

        if (fields.Count == 1)
        {
            return (true, equalsColumns[0]);
        }

        int targetColumn = FindDominantPaddedColumn(equalsColumns, spaceCounts);
        for (int i = 0; i < fields.Count; i++)
        {
            if (equalsColumns[i] == targetColumn)
            {
                continue;
            }

            bool isOverflowKey = keyEndColumns[i] >= targetColumn && spaceCounts[i] == 1;
            if (!isOverflowKey)
            {
                return (false, 0);
            }
        }

        return (true, targetColumn);
    }

    /// <summary>
    ///     Computes whitespace span edits to re-align equals tokens across <paramref name="fields"/>.
    /// </summary>
    public static List<TomlSpanEdit> ComputeRealignmentEdits(
        string source,
        IReadOnlyList<TomlFieldSpan> fields,
        int minPadding = 1)
    {
        List<TomlSpanEdit> edits = [];
        if (fields.Count == 0)
        {
            return edits;
        }

        int maxKeyEnd = 0;
        for (int i = 0; i < fields.Count; i++)
        {
            TomlFieldSpan field = fields[i];
            int gapStart = field.KeySpan.End;
            int gapEnd = field.EqualsSpan.Start;
            if (gapEnd <= gapStart || !IsOnlySpaces(source, gapStart, gapEnd))
            {
                return edits;
            }

            int keyEndColumn = gapStart - field.LineSpan.Start;
            if (keyEndColumn > maxKeyEnd)
            {
                maxKeyEnd = keyEndColumn;
            }
        }

        int targetColumn = maxKeyEnd + Math.Max(1, minPadding);

        for (int i = 0; i < fields.Count; i++)
        {
            TomlFieldSpan field = fields[i];
            int gapStart = field.KeySpan.End;
            int gapEnd = field.EqualsSpan.Start;
            int keyEndColumn = gapStart - field.LineSpan.Start;
            int desiredSpaces = Math.Max(1, targetColumn - keyEndColumn);
            int currentSpaces = gapEnd - gapStart;

            if (currentSpaces != desiredSpaces)
            {
                edits.Add(new TomlSpanEdit(gapStart, currentSpaces, new string(' ', desiredSpaces)));
            }
        }

        return edits;
    }

    /// <summary>
    ///     Computes the minimum space padding between the longest non-overflow key and the alignment column.
    /// </summary>
    public static int ComputeMinPadding(IReadOnlyList<TomlFieldSpan> fields, int alignmentColumn)
    {
        int maxNonOverflowKeyEnd = 0;
        for (int i = 0; i < fields.Count; i++)
        {
            TomlFieldSpan field = fields[i];
            int keyEndColumn = field.KeySpan.End - field.LineSpan.Start;
            int equalsColumn = field.EqualsSpan.Start - field.LineSpan.Start;
            if (equalsColumn == alignmentColumn && keyEndColumn > maxNonOverflowKeyEnd)
            {
                maxNonOverflowKeyEnd = keyEndColumn;
            }
        }

        return Math.Max(1, alignmentColumn - maxNonOverflowKeyEnd);
    }

    private static int FindDominantPaddedColumn(int[] equalsColumns, int[] spaceCounts)
    {
        int bestColumn = equalsColumns[0];
        int bestCount = -1;

        for (int i = 0; i < equalsColumns.Length; i++)
        {
            if (spaceCounts[i] < 2)
            {
                continue;
            }

            int candidate = equalsColumns[i];
            int count = 0;
            for (int j = 0; j < equalsColumns.Length; j++)
            {
                if (equalsColumns[j] == candidate)
                {
                    count++;
                }
            }

            if (count > bestCount || (count == bestCount && candidate > bestColumn))
            {
                bestCount = count;
                bestColumn = candidate;
            }
        }

        return bestColumn;
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
