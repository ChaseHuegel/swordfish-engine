using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
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

        using (ui.Page(layout))
        {
            ui.Padding = new Padding(left: 0, top: MenuLayout.PAGE_GAP, right: 0, bottom: 0);
            ui.Spacing = MenuLayout.PAGE_GAP;

            using (ui.MenuPanel(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                },
                _localization.GetRequired("ui.credits.title"),
                padding: 24
            ))
            {
                DrawPanelBody(ui, layout.PageWidth);
            }

            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(MenuLayout.FOOTER_HEIGHT),
                },
                spacing: 12
            ))
            {
                if (ui.ActionButton(
                    "Button_Back",
                    _localization.GetRequired("ui.button.return"),
                    _returnButton.FooterButton(MenuLayout.RETURN_BUTTON_WIDTH)
                ))
                {
                    menu.GoToPage(MenuPage.Home);
                }

                ui.Filler();
            }
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the scrollable credits text inside the menu panel.</summary>
    private void DrawPanelBody(UIBuilder<Material> ui, int pageWidth)
    {
        int contentWidth = Math.Min(760, pageWidth - 80);
        int bodyFontSize = contentWidth < 500
            ? UiMetrics.MENU_SMALL_FONT
            : UiMetrics.MENU_BODY_FONT;
        string bodyText = _localization.GetRequired("ui.credits.body");
        string thanksText = _localization.GetRequired("ui.credits.thanks");

        var bodyFont = new FontOptions { ID = UiTheme.FONT_UI, Size = bodyFontSize };
        var thanksFont = new FontOptions { ID = UiTheme.FONT_UI, Size = UiMetrics.MENU_SMALL_FONT };
        int bodyHeight = ui.TextEngine.Layout(bodyFont, bodyText, contentWidth).Constraints.PreferredHeight;
        int thanksHeight = ui.TextEngine.Layout(thanksFont, thanksText, contentWidth).Constraints.PreferredHeight;
        int contentHeight = bodyHeight + 8 + 1 + 16 + thanksHeight;

        ui.ScrollArea(
            "Credits.Content",
            contentHeight,
            ref _scrollY,
            _inputService,
            _ =>
            {
                using (ui.Column(
                    new Constraints
                    {
                        Anchors = Anchors.Center,
                        Width = new Fixed(contentWidth),
                    }
                ))
                {
                    ui.Paragraph(
                        bodyText,
                        bodyFontSize,
                        UiTheme.Text,
                        new Constraints
                        {
                            Width = new Fill(),
                            Height = new Fixed(bodyHeight),
                        }
                    );
                    ui.Spacer(height: 8);
                    ui.Divider(
                        UiTheme.Border,
                        new Constraints
                        {
                            Width = new Fill(),
                            Height = new Fixed(1),
                        }
                    );
                    ui.Spacer(height: 16);
                    ui.Paragraph(
                        thanksText,
                        UiMetrics.MENU_SMALL_FONT,
                        UiTheme.Text,
                        new Constraints
                        {
                            Width = new Fill(),
                            Height = new Fixed(thanksHeight),
                        }
                    );
                }
            }
        );
    }
}
