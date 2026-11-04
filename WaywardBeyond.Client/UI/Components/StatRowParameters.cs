using Reef.Constraints;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Parameters for the labelled value row.</summary>
internal sealed record StatRowParameters(bool Wrap = false)
{
    /// <summary>Gets the row height. A wrapping row sizes to its content.</summary>
    public IConstraint Height => Wrap ? new Fit() : new Fixed(18);
}
