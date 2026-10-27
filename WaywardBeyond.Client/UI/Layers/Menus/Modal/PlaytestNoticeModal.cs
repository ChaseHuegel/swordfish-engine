using Reef.Constraints;
using Reef.UI;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using System.Threading.Tasks;
using System;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Shows the playtest notice with links to the community pages.</summary>
internal class PlaytestNoticeModal(
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in ExternalAppService externalAppService
) : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.playtest");

    private readonly ILocalization _localization = localization;
    private readonly ExternalAppService _externalAppService = externalAppService;

    private readonly Widgets.ActionButtonOptions _closeButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Primary,
        soundEffectService
    );

    private readonly Widgets.IconButtonOptions _brandIconOptions = new(
        FontId: UiTheme.FONT_BRANDS,
        FontSize: UiMetrics.LARGE_ICON_FONT,
        SoundEffectService: soundEffectService
    );

    public Modal ID => Modal;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        int panelWidth = Math.Clamp(ui.Width - 160, 520, 760);
        int panelHeight = Math.Clamp(ui.Height - 160, 460, 500);

        using (ui.ModalDialog(panelWidth, panelHeight, UiTheme.ScrimHeavy))
        {
            ui.Label(
                _localization.GetRequired("ui.menu.playtest"),
                UiMetrics.MENU_TITLE_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(30),
                },
                HorizontalAlignment.Center
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
                _localization.GetRequired("ui.text.playtest"),
                UiMetrics.MENU_BODY_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            );

            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
                },
                spacing: 8
            ))
            {
                if (ui.IconButton(
                    "Button_Community",
                    MenuGlyphs.DISCORD,
                    _brandIconOptions,
                    new Constraints
                    {
                        Width = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
                        Height = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
                    }
                ))
                {
                    Task.Run(_externalAppService.TryOpenDiscordAsync);
                }

                if (ui.IconButton(
                    "Button_Wishlist",
                    MenuGlyphs.STEAM,
                    _brandIconOptions,
                    new Constraints
                    {
                        Width = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
                        Height = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
                    }
                ))
                {
                    Task.Run(_externalAppService.TryOpenSteamAsync);
                }

                ui.Filler();

                if (ui.ActionButton(
                    "Button_Close",
                    _localization.GetRequired("ui.button.ok"),
                    _closeButton with
                    {
                        Layout = new Constraints
                        {
                            Width = new Fixed(200),
                            Height = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
                            Anchors = Anchors.Center,
                        },
                    }
                ))
                {
                    menu.GoBack();
                }
            }
        }

        return Result.FromSuccess();
    }
}
