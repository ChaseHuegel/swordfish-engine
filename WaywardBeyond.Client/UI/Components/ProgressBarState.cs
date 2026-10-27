using System;
using System.Numerics;
using Reef.Constraints;
using Reef.UI;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Data for a progress bar.</summary>
internal readonly record struct ProgressBarState(float Value, Vector4? Tint = null)
{
    /// <summary>Gets the fill width constraint.</summary>
    public IConstraint FillWidth => new Relative(Math.Clamp(Value, 0f, 1f));

    /// <summary>Gets the fill color, falling back to the accent token.</summary>
    public Vector4 FillColor => Tint ?? WbPalette.Accent;
}
