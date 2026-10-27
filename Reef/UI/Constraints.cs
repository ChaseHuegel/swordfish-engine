using Reef.Constraints;

namespace Reef.UI;

/// <summary>
///     Position, size, and min/max bounds constraints for an element.
/// </summary>
public struct Constraints
{
    public Anchors Anchors;
    
    public IConstraint? X;
    public IConstraint? Y;
    public IConstraint? Width;
    public IConstraint? Height;
    
    public int MinWidth;
    public int MinHeight;
    public int MaxWidth;
    public int MaxHeight;
}
