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

    private readonly Widgets.ActionButtonOptions _brandIconButton = Widgets.ActionButtonOptions.Icon(
        ButtonVariant.Secondary,
        soundEffectService,
        size: UiMetrics.IconButtonSize,
        fontSize: UiMetrics.LargeIconFont,
        fontId: UiTheme.FontBrands
    );

    public Modal ID => Modal;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        int panelWidth = Math.Clamp(ui.Width - 160, 520, 760);
        int panelHeight = Math.Clamp(ui.Height - 160, 460, 500);

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.ScrimHeavy;
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };

            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    Width = new Fixed(panelWidth),
                    Height = new Fixed(panelHeight),
                };
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(panelWidth),
                    Height = new Fixed(panelHeight),
                };

                using (ui.MenuPanel(Widgets.Rect(0, 0, panelWidth, panelHeight)))
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
                    if (ui.ActionButton(
                            "Button_Community",
                            MenuGlyphs.Discord,
                            _brandIconButton with
                            {
                                Rect = Widgets.Rect(padding, buttonY, UiMetrics.IconButtonSize, UiMetrics.IconButtonSize),
                            }
                        ))
                    {
                        Task.Run(_externalAppService.TryOpenDiscordAsync);
                    }

                    if (ui.ActionButton(
                            "Button_Wishlist",
                            MenuGlyphs.Steam,
                            _brandIconButton with
                            {
                                Rect = Widgets.Rect(padding + UiMetrics.IconButtonSize + 8, buttonY, UiMetrics.IconButtonSize, UiMetrics.IconButtonSize),
                            }
                        ))
                    {
                        Task.Run(_externalAppService.TryOpenSteamAsync);
                    }

                    if (ui.ActionButton(
                            "Button_Close",
                            _localization.GetString("ui.button.ok")!,
                            _closeButton with
                            {
                                Rect = Widgets.Rect(panelWidth - padding - 200, buttonY, 200, UiMetrics.IconButtonSize),
                            }
                        ))
                    {
                        menu.GoBack();
                    }
                }
            }
        }

        return Result.FromSuccess();
    }
}
