using Reef.Constraints;

namespace Reef.UI;

public struct Constraints
{
    public Anchors Anchors;
    
    public IConstraint? X;
    public IConstraint? Y;
    public IConstraint? Width;
    public IConstraint? Height;
    
    public int MinWidth;
    public int MinHeight;
}