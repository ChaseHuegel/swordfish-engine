using System;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
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
        FontId: UiTheme.FontBrands,
        FontSize: UiMetrics.LargeIconFont,
        SoundEffectService: soundEffectService
    );

    public Modal ID => Modal;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        int panelWidth = Math.Clamp(ui.Width - 160, 520, 760);
        int panelHeight = Math.Clamp(ui.Height - 160, 460, 500);

        using (ui.ModalDialog(panelWidth, panelHeight, UiTheme.ScrimHeavy))
        {
            int padding = 28;
            int contentWidth = panelWidth - padding * 2;

            ui.Label(
                _localization.GetString("ui.menu.playtest")!,
                Widgets.Rect(padding, 22, contentWidth, 30),
                UiMetrics.MenuTitleFont,
                UiTheme.Text,
                Anchors.Center
            );
            ui.Divider(
                Widgets.Rect(padding, 62, contentWidth, 1),
                UiTheme.Border
            );

            int footerTop = panelHeight - 68;
            ui.Paragraph(
                _localization.GetString("ui.text.playtest")!,
                Widgets.Rect(padding, 78, contentWidth, Math.Max(0, footerTop - 96)),
                UiMetrics.MenuBodyFont,
                UiTheme.Text
            );

            int buttonY = footerTop + 8;
            IntRect communityRect = Widgets.Rect(padding, buttonY, UiMetrics.IconButtonSize, UiMetrics.IconButtonSize);
            if (ui.IconButton("Button_Community", communityRect, MenuGlyphs.Discord, _brandIconOptions))
            {
                Task.Run(_externalAppService.TryOpenDiscordAsync);
            }

            IntRect wishlistRect = Widgets.Rect(padding + UiMetrics.IconButtonSize + 8, buttonY, UiMetrics.IconButtonSize, UiMetrics.IconButtonSize);
            if (ui.IconButton("Button_Wishlist", wishlistRect, MenuGlyphs.Steam, _brandIconOptions))
            {
                Task.Run(_externalAppService.TryOpenSteamAsync);
            }

            IntRect closeRect = Widgets.Rect(panelWidth - padding - 200, buttonY, 200, UiMetrics.IconButtonSize);
            string closeLabel = _localization.GetString("ui.button.ok")!;
            if (ui.ActionButton("Button_Close", closeRect, closeLabel, _closeButton))
            {
                menu.GoBack();
            }
        }

        return Result.FromSuccess();
    }
}
