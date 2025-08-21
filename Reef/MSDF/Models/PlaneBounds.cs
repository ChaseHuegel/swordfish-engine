namespace Reef.MSDF.Models;

internal sealed record PlaneBounds(
    double left,
    double bottom,
    double right,
    double top
)
{
    public static readonly PlaneBounds Zero = new(0, 0, 0, 0);
}