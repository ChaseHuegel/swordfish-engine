namespace Reef.MSDF.Models;

internal sealed record GlyphAtlas(
    Atlas atlas,
    Metrics metrics,
    Glyph[] glyphs
);