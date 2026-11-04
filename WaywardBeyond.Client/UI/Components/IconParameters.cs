namespace WaywardBeyond.Client.UI.Components;

/// <summary>Parameters for the shared icon component.</summary>
internal sealed record IconParameters(
    IconGlyph Glyph = IconGlyph.Dice,
    IconTone Tone = IconTone.Muted,
    int Size = 20,
    bool Brand = false,
    bool Dim = false
)
{
    /// <summary>Gets the glyph character to draw.</summary>
    public string Text => Glyph switch
    {
        IconGlyph.Dice => "\uf074",
        IconGlyph.Trash => "\uf2ed",
        IconGlyph.Server => "\uf233",
        IconGlyph.Star => "\uf005",
        IconGlyph.Signal => "\uf012",
        IconGlyph.Rotate => "\uf021",
        IconGlyph.Shapes => "\uf61f",
        IconGlyph.Discord => "\uf392",
        IconGlyph.Steam => "\uf1b6",
        IconGlyph.Diamond => "\uf219",
        _ => string.Empty,
    };

    /// <summary>Gets whether the icon uses the dim tone.</summary>
    public bool IsDim => Dim || Tone == IconTone.Dim;

    /// <summary>Gets whether the icon uses the accent tone.</summary>
    public bool Accent => Tone == IconTone.Accent;

    /// <summary>Gets whether the icon uses the bright accent tone.</summary>
    public bool Bright => Tone == IconTone.Bright;

    /// <summary>Gets whether the icon uses the danger tone.</summary>
    public bool Danger => Tone == IconTone.Danger;

    /// <summary>Gets whether the icon uses the primary tone.</summary>
    public bool Primary => Tone == IconTone.Primary;
}
