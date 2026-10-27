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
        int padding = UiMetrics.MODAL_PADDING;
        int contentWidth = panelWidth - padding * 2;

        using (ui.ModalDialog(panelWidth, panelHeight, UiTheme.ScrimHeavy))
        {
            ui.Label(
                _localization.GetRequired("ui.menu.playtest"),
                Widgets.Rect(padding, 22, contentWidth, 30),
                UiMetrics.MENU_TITLE_FONT,
                UiTheme.Text,
                Anchors.Center
            );
            ui.Divider(
                Widgets.Rect(padding, 62, contentWidth, 1),
                UiTheme.Border
            );

            int footerTop = panelHeight - 68;
            ui.Paragraph(
                _localization.GetRequired("ui.text.playtest"),
                Widgets.Rect(padding, 78, contentWidth, Math.Max(0, footerTop - 96)),
                UiMetrics.MENU_BODY_FONT,
                UiTheme.Text
            );

            int buttonY = footerTop + 8;
            IntRect communityRect = Widgets.Rect(
                padding,
                buttonY,
                UiMetrics.ICON_BUTTON_SIZE,
                UiMetrics.ICON_BUTTON_SIZE
            );
            if (ui.IconButton("Button_Community", communityRect, MenuGlyphs.DISCORD, _brandIconOptions))
            {
                Task.Run(_externalAppService.TryOpenDiscordAsync);
            }

            IntRect wishlistRect = Widgets.Rect(
                padding + UiMetrics.ICON_BUTTON_SIZE + 8,
                buttonY,
                UiMetrics.ICON_BUTTON_SIZE,
                UiMetrics.ICON_BUTTON_SIZE
            );
            if (ui.IconButton("Button_Wishlist", wishlistRect, MenuGlyphs.STEAM, _brandIconOptions))
            {
                Task.Run(_externalAppService.TryOpenSteamAsync);
            }

            IntRect closeRect = Widgets.Rect(
                panelWidth - padding - 200,
                buttonY,
                200,
                UiMetrics.ICON_BUTTON_SIZE
            );
            string closeLabel = _localization.GetRequired("ui.button.ok");
            if (ui.ActionButton("Button_Close", closeRect, closeLabel, _closeButton))
            {
                menu.GoBack();
            }
        }

        return Result.FromSuccess();
    }
}
