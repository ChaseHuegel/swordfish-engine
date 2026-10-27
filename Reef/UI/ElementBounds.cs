namespace Reef.UI;

/// <summary>
///     Resolved screen-space bounds, clip rectangle, and unclipped content size of an element.
/// </summary>
public readonly record struct ElementBounds(IntRect Rect, IntRect ClipRect, IntVector2 ContentSize)
{
    public static readonly ElementBounds Empty = default;
}
