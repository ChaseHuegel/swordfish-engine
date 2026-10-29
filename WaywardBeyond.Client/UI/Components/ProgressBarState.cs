using System;
using Reef.Constraints;
using Reef.UI;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Data for a progress bar.</summary>
internal readonly record struct ProgressBarState(float Value)
{
    /// <summary>Gets the fill width constraint.</summary>
    public IConstraint FillWidth => new Relative(Math.Clamp(Value, 0f, 1f));
}
