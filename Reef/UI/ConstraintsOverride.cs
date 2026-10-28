using Reef.Constraints;

namespace Reef.UI;

/// <summary>
///     Optional layout constraint overrides passed to a generated markup
///     component. A null field leaves the component's own constraint in place.
/// </summary>
public struct ConstraintsOverride
{
    /// <summary>Anchor override.</summary>
    public Anchors? Anchors;

    /// <summary>Horizontal position override.</summary>
    public IConstraint? X;

    /// <summary>Vertical position override.</summary>
    public IConstraint? Y;

    /// <summary>Width override.</summary>
    public IConstraint? Width;

    /// <summary>Height override.</summary>
    public IConstraint? Height;

    /// <summary>Minimum width override.</summary>
    public int? MinWidth;

    /// <summary>Minimum height override.</summary>
    public int? MinHeight;

    /// <summary>Maximum width override.</summary>
    public int? MaxWidth;

    /// <summary>Maximum height override.</summary>
    public int? MaxHeight;
}
