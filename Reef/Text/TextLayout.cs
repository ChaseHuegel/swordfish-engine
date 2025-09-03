namespace Reef.Text;

public readonly struct TextLayout(TextConstraints constraints, GlyphLayout[] glyphs)
{
    public readonly TextConstraints Constraints = constraints;
    public readonly GlyphLayout[] Glyphs = glyphs;
}