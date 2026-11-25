namespace WaywardBeyond.AssetTools.Documents;

/// <summary>
///     Character span and one-based line and column location inside a TOML source string.
/// </summary>
public readonly record struct TomlTextSpan(int Start, int Length, int Line, int Column)
{
    /// <summary>
    ///     Gets the exclusive end character offset of this span.
    /// </summary>
    public int End => Start + Length;

    /// <summary>
    ///     Gets a value indicating whether this span has zero length.
    /// </summary>
    public bool IsEmpty => Length == 0;

    /// <summary>
    ///     Returns the exact substring of <paramref name="source"/> covered by this span.
    /// </summary>
    public string Slice(string source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (Start < 0 || Length < 0 || Start + Length > source.Length)
        {
            return string.Empty;
        }

        return source.Substring(Start, Length);
    }
}
