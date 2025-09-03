namespace Reef.MSDF.Models;

internal sealed record Atlas(
    string type,
    int distanceRange,
    int distanceRangeMiddle,
    double size,
    int width,
    int height,
    string yOrigin
);