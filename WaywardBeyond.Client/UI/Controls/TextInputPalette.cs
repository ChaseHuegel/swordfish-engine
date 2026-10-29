using System.Numerics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Controls;

/// <summary>Colors used by a text input for parts that Reef style does not carry.</summary>
internal readonly record struct TextInputPalette(
    Vector4 Text,
    Vector4 TextFocused,
    Vector4 Placeholder,
    Vector4 PlaceholderFocused,
    Vector4 Selection,
    Vector4 Caret
)
{
    /// <summary>The shared palette for menu and HUD text inputs.</summary>
    public static TextInputPalette Default { get; } = new(
        Text: UiTheme.TextMuted,
        TextFocused: UiTheme.Text,
        Placeholder: UiTheme.TextDim,
        PlaceholderFocused: UiTheme.TextMuted,
        Selection: UiTheme.RowSelected,
        Caret: UiTheme.Text
    );
}
