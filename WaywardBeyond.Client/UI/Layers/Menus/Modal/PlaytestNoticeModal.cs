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

        ui.ModalFrame(
            panelWidth,
            panelHeight,
            UiTheme.ScrimHeavy,
            frame => DrawBody(frame, menu, panelWidth, panelHeight)
        );

        return Result.FromSuccess();
    }

    /// <summary>Draws the notice text and footer actions inside the modal panel.</summary>
    private void DrawBody(UIBuilder<Material> ui, Menu<Modal> menu, int panelWidth, int panelHeight)
    {
        int padding = UiMetrics.MODAL_PADDING;
        int contentWidth = panelWidth - padding * 2;

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
        DrawFooter(ui, menu, padding, panelWidth, buttonY);
    }

    /// <summary>Draws the community, wishlist, and close actions along the panel footer.</summary>
    private void DrawFooter(UIBuilder<Material> ui, Menu<Modal> menu, int padding, int panelWidth, int buttonY)
    {
        bool communityPressed = ui.IconButton(
            "Button_Community",
            Widgets.Rect(padding, buttonY, UiMetrics.ICON_BUTTON_SIZE, UiMetrics.ICON_BUTTON_SIZE),
            MenuGlyphs.DISCORD,
            _brandIconOptions
        );
        if (communityPressed)
        {
            Task.Run(_externalAppService.TryOpenDiscordAsync);
        }

        bool wishlistPressed = ui.IconButton(
            "Button_Wishlist",
            Widgets.Rect(padding + UiMetrics.ICON_BUTTON_SIZE + 8, buttonY, UiMetrics.ICON_BUTTON_SIZE, UiMetrics.ICON_BUTTON_SIZE),
            MenuGlyphs.STEAM,
            _brandIconOptions
        );
        if (wishlistPressed)
        {
            Task.Run(_externalAppService.TryOpenSteamAsync);
        }

        Widgets.ActionButtonOptions closeOptions = _closeButton with
        {
            Rect = Widgets.Rect(panelWidth - padding - 200, buttonY, 200, UiMetrics.ICON_BUTTON_SIZE),
        };

        if (ui.ActionButton("Button_Close", _localization.GetRequired("ui.button.ok"), closeOptions))
        {
            menu.GoBack();
        }
    }
}
