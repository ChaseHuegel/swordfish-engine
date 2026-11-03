namespace WaywardBeyond.Client.UI.Components;

/// <summary>Parameters for the shared icon button component.</summary>
internal sealed record IconButtonParameters(
    IconGlyph Glyph = IconGlyph.Dice,
    IconTone Tone = IconTone.Muted,
    int Size = 32,
    int GlyphSize = 20,
    bool Brand = false,
    bool Dim = false
)
{
    /// <summary>Gets whether the button uses the danger hover treatment.</summary>
    public bool Danger => Tone == IconTone.Danger;
}
