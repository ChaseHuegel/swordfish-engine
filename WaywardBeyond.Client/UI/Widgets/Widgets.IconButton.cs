using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Creates a borderless icon button with hover and pressed fills.</summary>
    public static bool IconButton(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        string glyph,
        IconButtonOptions options
    )
    {
        return ui.ActionButton(
            id,
            glyph,
            CreateIconButtonOptions(options, RectConstraints(rect))
        );
    }

    /// <summary>Creates a borderless icon button in the current layout.</summary>
    public static bool IconButton(
        this UIBuilder<Material> ui,
        string id,
        string glyph,
        IconButtonOptions options,
        Constraints constraints
    )
    {
        return ui.ActionButton(
            id,
            glyph,
            CreateIconButtonOptions(options, constraints)
        );
    }

    private static ActionButtonOptions CreateIconButtonOptions(
        IconButtonOptions options,
        Constraints constraints
    )
    {
        return new ActionButtonOptions(
            Variant: options.Danger ? ButtonVariant.IconDanger : ButtonVariant.Icon,
            Enabled: options.Enabled,
            Layout: constraints,
            Font: new FontOptions
            {
                ID = options.FontId,
                Size = options.FontSize,
                NoWrap = true,
            },
            SoundEffectService: options.SoundEffectService
        );
    }

    /// <summary>Options for a borderless icon button.</summary>
    public readonly record struct IconButtonOptions(
        bool Danger = false,
        bool Enabled = true,
        string FontId = UiTheme.FONT_ICONS,
        int FontSize = UiMetrics.ICON_FONT,
        SoundEffectService? SoundEffectService = null
    );
}
