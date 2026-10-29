using System;
using Reef.Constraints;
using Reef.UI;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Thumb geometry for a scrollbar.</summary>
internal readonly record struct ScrollBarState(float ThumbRatio, float ThumbOffset)
{
    /// <summary>Gets the thumb height constraint.</summary>
    public IConstraint ThumbHeight => new Relative(Math.Clamp(ThumbRatio, 0f, 1f));

    /// <summary>Gets the thumb offset constraint.</summary>
    public IConstraint ThumbY => new Relative(Math.Clamp(ThumbOffset, 0f, 1f));
}
