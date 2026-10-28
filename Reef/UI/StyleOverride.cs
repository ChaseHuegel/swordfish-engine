using System.Numerics;

namespace Reef.UI;

/// <summary>
///     Optional visual style overrides passed to a generated markup component.
///     A null field leaves the component's own style in place.
/// </summary>
public struct StyleOverride
{
    /// <summary>Text or tint color override.</summary>
    public Vector4? Color;

    /// <summary>Background color override.</summary>
    public Vector4? BackgroundColor;

    /// <summary>Padding override.</summary>
    public Padding? Padding;

    /// <summary>Corner radius override.</summary>
    public CornerRadius? CornerRadius;

    /// <summary>Border override.</summary>
    public BorderStyle? Border;

    /// <summary>Corner cap override.</summary>
    public CornerCapStyle? CornerCaps;
}
