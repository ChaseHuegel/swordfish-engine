using System.Text;

namespace WaywardBeyond.AssetTools.Documents;

/// <summary>
///     Single character-span replacement applied against a TOML source string.
/// </summary>
internal readonly record struct TomlSpanEdit(int Start, int Length, string Replacement)
{
    /// <summary>
    ///     Gets the exclusive end offset of this edit in the source string.
    /// </summary>
    public int End => Start + Length;

    /// <summary>
    ///     Applies a set of non-overlapping span edits to <paramref name="source"/> in reverse offset order.
    /// </summary>
    public static string ApplyAll(string source, IReadOnlyList<TomlSpanEdit> edits)
    {
        if (edits.Count == 0)
        {
            return source;
        }

        if (edits.Count == 1)
        {
            TomlSpanEdit single = edits[0];
            return string.Concat(
                source.AsSpan(0, single.Start),
                single.Replacement.AsSpan(),
                source.AsSpan(single.End));
        }

        var ordered = new TomlSpanEdit[edits.Count];
        for (int i = 0; i < edits.Count; i++)
        {
            ordered[i] = edits[i];
        }

        Array.Sort(ordered, static (left, right) =>
        {
            int startCompare = left.Start.CompareTo(right.Start);
            return startCompare != 0 ? startCompare : left.Length.CompareTo(right.Length);
        });

        int totalDelta = 0;
        for (int i = 0; i < ordered.Length; i++)
        {
            TomlSpanEdit edit = ordered[i];
            if (edit.Start < 0 || edit.Length < 0 || edit.End > source.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(edits), "Span edit exceeds source bounds.");
            }

            if (i > 0 && ordered[i - 1].End > edit.Start)
            {
                throw new InvalidOperationException("Span edits must not overlap.");
            }

            totalDelta += edit.Replacement.Length - edit.Length;
        }

        var builder = new StringBuilder(source.Length + totalDelta);
        int cursor = 0;
        for (int i = 0; i < ordered.Length; i++)
        {
            TomlSpanEdit edit = ordered[i];
            if (edit.Start > cursor)
            {
                builder.Append(source, cursor, edit.Start - cursor);
            }

            builder.Append(edit.Replacement);
            cursor = edit.End;
        }

        if (cursor < source.Length)
        {
            builder.Append(source, cursor, source.Length - cursor);
        }

        return builder.ToString();
    }
}
