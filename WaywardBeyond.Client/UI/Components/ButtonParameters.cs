using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Parameters for the shared button component.</summary>
internal sealed record ButtonParameters(
    ButtonVariant Variant = ButtonVariant.Secondary,
    bool Compact = false,
    bool DoubleBorder = true,
    bool Enabled = true
)
{
    /// <summary>Gets whether the button uses the primary variant.</summary>
    public bool Primary => Variant == ButtonVariant.Primary;

    /// <summary>Gets whether the button uses the danger variant.</summary>
    public bool Danger => Variant == ButtonVariant.Danger;

    /// <summary>Gets whether the button uses the subtle variant.</summary>
    public bool Subtle => Variant == ButtonVariant.Subtle;

    /// <summary>Gets whether the button draws an inner border.</summary>
    public bool InnerBorder => DoubleBorder;
}
