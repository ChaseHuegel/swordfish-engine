using Reef;
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
            new ActionButtonOptions(
                Variant: options.Danger ? ButtonVariant.IconDanger : ButtonVariant.Icon,
                Enabled: options.Enabled,
                Rect: rect,
                Font: new FontOptions
                {
                    ID = options.FontId,
                    Size = options.FontSize,
                    NoWrap = true,
                },
                SoundEffectService: options.SoundEffectService
            )
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
