namespace Reef.Text;

public readonly struct TextLayout(TextConstraints constraints, GlyphLayout[] glyphs, string[] lines, int lineHeight)
{
    public readonly TextConstraints Constraints = constraints;
    public readonly GlyphLayout[] Glyphs = glyphs;
    public readonly string[] Lines = lines;
    public readonly int LineHeight = lineHeight;
}