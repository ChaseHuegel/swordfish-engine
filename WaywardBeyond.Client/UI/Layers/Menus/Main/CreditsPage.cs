using Reef.Constraints;
using Reef.UI;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using System;
using WaywardBeyond.Client.Extensions;
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
            twoColumn: false,
            maxPageHeight: 720
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

            IntRect panel = Widgets.Rect(
                0,
                layout.Right.Top,
                layout.PageWidth,
                layout.Right.Size.Y
            );

            using (ui.MenuPanel(panel, _localization.GetRequired("ui.credits.title")))
            {
                DrawPanelBody(ui, in panel);
            }

            string returnLabel = _localization.GetRequired("ui.button.return");
            if (ui.ActionButton("Button_Back", layout.ReturnButton, returnLabel, _returnButton))
            {
                menu.GoToPage(MenuPage.Home);
            }
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the scrollable credits text inside the menu panel.</summary>
    private void DrawPanelBody(UIBuilder<Material> ui, in IntRect panel)
    {
        int padding = 40;
        int contentWidth = Math.Min(760, panel.Size.X - padding * 2);
        int contentX = (panel.Size.X - contentWidth) / 2;
        int viewportY = 38;
        int viewportHeight = Math.Max(1, panel.Size.Y - viewportY - 12);
        int bodyFontSize = contentWidth < 500
            ? UiMetrics.MENU_SMALL_FONT
            : UiMetrics.MENU_BODY_FONT;
        string bodyText = _localization.GetRequired("ui.credits.body");
        string thanksText = _localization.GetRequired("ui.credits.thanks");

        var bodyFont = new FontOptions { ID = UiTheme.FONT_UI, Size = bodyFontSize };
        var thanksFont = new FontOptions { ID = UiTheme.FONT_UI, Size = UiMetrics.MENU_SMALL_FONT };
        int bodyHeight = ui.TextEngine.Layout(bodyFont, bodyText, contentWidth).Constraints.PreferredHeight;
        int thanksHeight = ui.TextEngine.Layout(thanksFont, thanksText, contentWidth).Constraints.PreferredHeight;
        int contentHeight = 62 + bodyHeight + 8 + 1 + 16 + thanksHeight + 12;
        int contentTop = 10;

        ui.ScrollArea(
            Widgets.Rect(0, viewportY, panel.Size.X, viewportHeight),
            contentHeight,
            ref _scrollY,
            _inputService,
            Widgets.Rect(panel.Size.X - 18, viewportY, 6, viewportHeight),
            _ =>
            {
                int y = contentTop;
                ui.Paragraph(
                    bodyText,
                    Widgets.Rect(contentX, y, contentWidth, bodyHeight),
                    bodyFontSize,
                    UiTheme.Text
                );

                y += bodyHeight + 8;
                ui.Divider(Widgets.Rect(contentX, y, contentWidth, 1), UiTheme.Border);
                y += 16;
                ui.Paragraph(
                    thanksText,
                    Widgets.Rect(contentX, y, contentWidth, thanksHeight),
                    UiMetrics.MENU_SMALL_FONT,
                    UiTheme.Text
                );
            }
        );
    }
}
