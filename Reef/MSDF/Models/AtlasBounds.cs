namespace Reef.MSDF.Models;

internal sealed record AtlasBounds(
    double left,
    double bottom,
    double right,
    double top
)
{
    public static readonly AtlasBounds Zero = new(0, 0, 0, 0);
}