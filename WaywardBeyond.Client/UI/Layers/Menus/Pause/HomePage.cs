using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Pause;

internal sealed class HomePage(
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in GameSaveManager gameSaveManager,
    in ModalMenu modalMenu
) : IMenuPage<PausePage>
{
    public PausePage ID => PausePage.Home;

    private const int BUTTON_GAP = 10;
    private const int DIVIDER_GAP = 12;
    private const int DIVIDER_HEIGHT = 1;
    private const int MENU_WIDTH = UiMetrics.MENU_BUTTON_MIN_WIDTH;

    private readonly ILocalization _localization = localization;
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly ModalMenu _modalMenu = modalMenu;

    private readonly Widgets.ActionButtonOptions _primaryButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Primary,
        soundEffectService
    );
    private readonly Widgets.ActionButtonOptions _secondaryButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Secondary,
        soundEffectService
    );
    private readonly Widgets.ActionButtonOptions _dangerButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Danger,
        soundEffectService
    );

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<PausePage> menu)
    {
        using (ui.Column(
            new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(MENU_WIDTH),
            }
        ))
        {
            if (ui.ActionButton(
                "Button_Continue",
                _localization.GetRequired("ui.button.continue"),
                _primaryButton with { Layout = ButtonSlot() }
            ))
            {
                WaywardBeyond.Unpause();
            }

            ui.Spacer(height: BUTTON_GAP);

            if (ui.ActionButton(
                "Button_Settings",
                _localization.GetRequired("ui.button.settings"),
                _secondaryButton with { Layout = ButtonSlot() }
            ))
            {
                menu.GoToPage(PausePage.Settings);
            }

            ui.Spacer(height: BUTTON_GAP);

            if (ui.ActionButton(
                "Button_Feedback",
                _localization.GetRequired("ui.button.feedback"),
                _secondaryButton with { Layout = ButtonSlot() }
            ))
            {
                _modalMenu.GoToPage(FeedbackModal.Modal);
            }

            ui.Spacer(height: DIVIDER_GAP);
            ui.DottedDivider(Widgets.Rect(0, 0, MENU_WIDTH, DIVIDER_HEIGHT));
            ui.Spacer(height: DIVIDER_GAP);

            if (ui.ActionButton(
                "Button_SaveAndExit",
                _localization.GetRequired("ui.button.saveAndExit"),
                _dangerButton with { Layout = ButtonSlot() }
            ))
            {
                _gameSaveManager.SaveAndExit();
            }
        }

        return Result.FromSuccess();
    }

    private static Constraints ButtonSlot()
    {
        return new Constraints
        {
            Width = new Fill(),
            Height = new Fixed(UiMetrics.MENU_BUTTON_HEIGHT),
        };
    }
}
