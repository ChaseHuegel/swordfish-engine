namespace Reef.UI;

/// <summary>
///     Typography, wrapping, and alignment options for text elements.
/// </summary>
public struct FontOptions
{
    public string? ID;
    public int Size;

    /// <summary>
    ///     When true, text is laid out on one line regardless of the available width.
    /// </summary>
    public bool NoWrap;

    /// <summary>
    ///     Horizontal alignment of lines within the text element.
    /// </summary>
    public HorizontalAlignment HorizontalAlignment;

    /// <summary>
    ///     Vertical alignment of the text block within the text element.
    /// </summary>
    public VerticalAlignment VerticalAlignment;
}
