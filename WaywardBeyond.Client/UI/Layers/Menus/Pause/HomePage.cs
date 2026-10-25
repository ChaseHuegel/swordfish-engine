using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
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
    private const int MENU_WIDTH = 300;
    private const int MENU_HEIGHT = UiMetrics.MenuButtonHeight * 4
        + BUTTON_GAP * 2
        + DIVIDER_GAP * 2
        + DIVIDER_HEIGHT;

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
        int buttonX = (MENU_WIDTH - UiMetrics.MenuButtonMinWidth) / 2;
        int buttonWidth = UiMetrics.MenuButtonMinWidth;
        int buttonHeight = UiMetrics.MenuButtonHeight;

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(MENU_WIDTH),
                Height = new Fixed(MENU_HEIGHT),
            };
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(MENU_WIDTH),
                Height = new Fixed(MENU_HEIGHT),
            };

            var y = 0;
            ui.MenuChevron(buttonX - 8, y + (buttonHeight - 6) / 2);
            if (ui.ActionButton(
                    "Button_Continue",
                    _localization.GetString("ui.button.continue")!,
                    _primaryButton with
                    {
                        Rect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight),
                    }
                ))
            {
                WaywardBeyond.Unpause();
            }

            y += buttonHeight + BUTTON_GAP;
            if (ui.ActionButton(
                    "Button_Settings",
                    _localization.GetString("ui.button.settings")!,
                    _secondaryButton with
                    {
                        Rect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight),
                    }
                ))
            {
                menu.GoToPage(PausePage.Settings);
            }

            y += buttonHeight + BUTTON_GAP;
            if (ui.ActionButton(
                    "Button_Feedback",
                    _localization.GetString("ui.button.feedback")!,
                    _secondaryButton with
                    {
                        Rect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight),
                    }
                ))
            {
                _modalMenu.GoToPage(FeedbackModal.Modal);
            }

            y += buttonHeight + DIVIDER_GAP;
            ui.DottedDivider(Widgets.Rect(buttonX, y, buttonWidth, DIVIDER_HEIGHT));
            y += DIVIDER_HEIGHT + DIVIDER_GAP;

            if (ui.ActionButton(
                    "Button_SaveAndExit",
                    _localization.GetString("ui.button.saveAndExit")!,
                    _dangerButton with
                    {
                        Rect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight),
                    }
                ))
            {
                _gameSaveManager.SaveAndExit();
            }
        }

        return Result.FromSuccess();
    }
}
