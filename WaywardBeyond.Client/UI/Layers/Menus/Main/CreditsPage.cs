using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class CreditsPage(
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in IInputService inputService
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Credits;

    private const int CONTENT_HEIGHT = 300;

    private readonly ILocalization _localization = localization;
    private readonly IInputService _inputService = inputService;
    private readonly Widgets.ActionButtonOptions _returnButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Secondary,
        soundEffectService
    );

    private int _scrollY;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        MenuLayout layout = MenuLayout.Compute(
            ui.Width,
            ui.Height,
            maxPageWidth: 1160,
            twoColumn: false
        );

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };

            ui.Label(
                _localization.GetString("ui.menu.credits")!,
                layout.Title,
                UiMetrics.MenuTitleFont,
                UiTheme.Text,
                Anchors.Center
            );

            IntRect panel = Widgets.Rect(
                0,
                layout.Right.Top,
                layout.PageWidth,
                layout.Right.Size.Y
            );

            using (ui.MenuPanel(panel, _localization.GetString("ui.credits.manifest")!))
            {
                int padding = 40;
                int contentWidth = Math.Min(760, panel.Size.X - padding * 2);
                int contentX = (panel.Size.X - contentWidth) / 2;
                int viewportY = 38;
                int viewportHeight = Math.Max(1, panel.Size.Y - viewportY - 12);
                int maxScroll = Math.Max(0, CONTENT_HEIGHT - viewportHeight);
                float wheel = _inputService.GetMouseScroll();
                if (wheel != 0f)
                {
                    _scrollY -= (int)MathF.Round(wheel * UiMetrics.ScrollStep);
                }
                _scrollY = Math.Clamp(_scrollY, 0, maxScroll);

                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Color = UiTheme.Transparent;
                    ui.Constraints = Widgets.RectConstraints(
                        Widgets.Rect(0, viewportY, panel.Size.X, viewportHeight)
                    );
                    ui.ClipConstraints = new Constraints
                    {
                        Width = new Fixed(panel.Size.X),
                        Height = new Fixed(viewportHeight),
                    };
                    ui.ScrollY = -_scrollY;

                    int y = maxScroll > 0 ? 0 : Math.Max(0, (viewportHeight - CONTENT_HEIGHT) / 2);

                    ui.Label(
                        _localization.GetString("ui.credits.producedBy")!,
                        Widgets.Rect(0, y, panel.Size.X, 18),
                        UiMetrics.MenuSmallFont,
                        UiTheme.TextMuted,
                        Anchors.Center
                    );
                    ui.Label(
                        "BITFISH LLC",
                        Widgets.Rect(0, y + 22, panel.Size.X, 30),
                        UiMetrics.MenuTitleFont,
                        UiTheme.Primary,
                        Anchors.Center
                    );

                    y += 62;
                    DrawCreditSection(
                        ui,
                        _localization.GetString("ui.credits.development")!,
                        "CHASE M. HUEGEL",
                        contentX,
                        ref y,
                        contentWidth
                    );
                    DrawCreditSection(
                        ui,
                        _localization.GetString("ui.credits.engine")!,
                        "SWORDFISH CUSTOM ENGINE",
                        contentX,
                        ref y,
                        contentWidth
                    );
                    DrawCreditSection(
                        ui,
                        _localization.GetString("ui.credits.contributions")!,
                        _localization.GetString("ui.credits.audioVisual")!,
                        contentX,
                        ref y,
                        contentWidth
                    );

                    y += 8;
                    ui.Divider(Widgets.Rect(contentX, y, contentWidth, 1), UiTheme.Border);
                    y += 16;
                    ui.Label(
                        _localization.GetString("ui.credits.thanks")!,
                        Widgets.Rect(contentX, y, contentWidth, 18),
                        UiMetrics.MenuSmallFont,
                        UiTheme.TextMuted,
                        Anchors.Left | Anchors.Center
                    );
                    y += 20;
                    ui.Paragraph(
                        _localization.GetString("ui.credits.community")!,
                        Widgets.Rect(contentX, y, contentWidth, 24),
                        UiMetrics.MenuSmallFont,
                        UiTheme.Text
                    );
                }

                if (maxScroll > 0)
                {
                    ui.Scrollbar(
                        Widgets.Rect(panel.Size.X - 18, viewportY, 6, viewportHeight),
                        CONTENT_HEIGHT,
                        viewportHeight,
                        _scrollY
                    );
                }
            }

            if (ui.ActionButton(
                    "Button_Back",
                    _localization.GetString("ui.button.return")!,
                    _returnButton with { Rect = layout.ReturnButton }
                ))
            {
                menu.GoToPage(MenuPage.Home);
            }
        }

        return Result.FromSuccess();
    }

    private static void DrawCreditSection(
        UIBuilder<Material> ui,
        string label,
        string value,
        int x,
        ref int y,
        int width
    )
    {
        ui.Label(
            label,
            Widgets.Rect(x, y, width, 18),
            UiMetrics.MenuSmallFont,
            UiTheme.Accent,
            Anchors.Left | Anchors.Center
        );
        y += 20;
        ui.Paragraph(
            value,
            Widgets.Rect(x, y, width, 24),
            width < 500 ? UiMetrics.MenuSmallFont : UiMetrics.MenuBodyFont,
            UiTheme.Text
        );
        y += 42;
    }
}
