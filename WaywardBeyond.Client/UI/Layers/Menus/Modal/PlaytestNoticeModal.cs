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
    ) with
    {
        Anchors = Anchors.Center,
    };

    private readonly Widgets.ActionButtonOptions _brandIconButton = Widgets.ActionButtonOptions.Icon(
        ButtonVariant.Secondary,
        soundEffectService,
        size: UiMetrics.IconButtonSize,
        fontSize: UiMetrics.LargeIconFont,
        fontId: "Font Awesome 6 Free Brands"
    );

    public Modal ID => Modal;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        using (ui.Element())
        {
            //  Fullscreen tint
            ui.Color = UiTheme.ScrimHeavy;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Padding = new Padding
            {
                Left = 20,
                Top = 20,
                Right = 20,
                Bottom = 20,
            };
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };

                using (ui.Text(_localization.GetString("ui.menu.playtest")!))
                {
                    ui.FontSize = UiMetrics.TitleFont;
                    ui.Color = UiTheme.Text;
                }
            }

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    Width = new Fixed(500),
                    Height = new Fixed(300),
                };
                ui.ClipConstraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Relative(1f),
                };

                using (ui.Text(_localization.GetString("ui.text.playtest")!)) { }
            }

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };

                if (ui.ActionButton("Button_Close", _localization.GetString("ui.button.close")!, _closeButton))
                {
                    menu.GoBack();
                }
            }

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };

                if (ui.ActionButton("Button_Community", "\uf392", _brandIconButton))
                {
                    Task.Run(_externalAppService.TryOpenDiscordAsync);
                }

                if (ui.ActionButton("Button_Wishlist", "\uf1b6", _brandIconButton))
                {
                    Task.Run(_externalAppService.TryOpenSteamAsync);
                }
            }

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }

            return Result.FromSuccess();
        }
    }
}
