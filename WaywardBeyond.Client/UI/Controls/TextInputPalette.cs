using System.Numerics;

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
        Text: FromRgb(141, 160, 184),
        TextFocused: FromRgb(240, 243, 246),
        Placeholder: FromRgb(69, 84, 107),
        PlaceholderFocused: FromRgb(141, 160, 184),
        Selection: FromRgb(0, 116, 255, 128),
        Caret: FromRgb(240, 243, 246)
    );

    private static Vector4 FromRgb(byte r, byte g, byte b, byte a = 255)
    {
        return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
    }
}
