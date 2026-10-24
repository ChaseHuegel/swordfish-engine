namespace Reef.UI;

public struct FontOptions
{
    public string? ID;
    public int Size;

    /// <summary>When true, text is laid out on one line regardless of the available width.</summary>
    public bool NoWrap;
}