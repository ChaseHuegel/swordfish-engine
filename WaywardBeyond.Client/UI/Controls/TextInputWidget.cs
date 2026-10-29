using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using WaywardBeyond.Client.UI;

namespace WaywardBeyond.Client.UI.Controls;

/// <summary>Draws a styleable, editable text input with no markup-owned chrome.</summary>
internal static class TextInputWidget
{
    /// <summary>Draws an editable text input and updates its interaction flags.</summary>
    public static void TextInput(
        this UIBuilder<Material> ui,
        string id,
        ref TextBoxState state,
        string? placeholder,
        IInputService inputService,
        Style style,
        FontOptions fontOptions,
        Constraints constraints,
        TextInputPalette? palette = null
    )
    {
        Widgets.TextBox(
            ui,
            id,
            ref state,
            fontOptions,
            inputService,
            out _,
            style,
            palette ?? TextInputPalette.Default,
            constraints,
            placeholder
        );
    }
}
