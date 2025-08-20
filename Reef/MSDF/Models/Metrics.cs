namespace Reef.MSDF.Models;

internal sealed record Metrics(
    float emSize,
    double lineHeight,
    double ascender,
    double descender,
    double underlineY,
    double underlineThickness
);