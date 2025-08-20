namespace Reef.MSDF.Models;

internal sealed record Glyph(
    int unicode,
    double advance,
    PlaneBounds planeBounds,
    AtlasBounds atlasBounds
);