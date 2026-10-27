using Reef.Constraints;
using Reef.UI;
using Reef;
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
    private const int MENU_WIDTH = 300;
    private const int MENU_HEIGHT = UiMetrics.MENU_BUTTON_HEIGHT * 4
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
        int buttonX = (MENU_WIDTH - UiMetrics.MENU_BUTTON_MIN_WIDTH) / 2;
        int buttonWidth = UiMetrics.MENU_BUTTON_MIN_WIDTH;
        int buttonHeight = UiMetrics.MENU_BUTTON_HEIGHT;

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

            IntRect continueRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
            string continueLabel = _localization.GetRequired("ui.button.continue");
            if (ui.ActionButton("Button_Continue", continueRect, continueLabel, _primaryButton))
            {
                WaywardBeyond.Unpause();
            }

            y += buttonHeight + BUTTON_GAP;
            IntRect settingsRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
            string settingsLabel = _localization.GetRequired("ui.button.settings");
            if (ui.ActionButton("Button_Settings", settingsRect, settingsLabel, _secondaryButton))
            {
                menu.GoToPage(PausePage.Settings);
            }

            y += buttonHeight + BUTTON_GAP;
            IntRect feedbackRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
            string feedbackLabel = _localization.GetRequired("ui.button.feedback");
            if (ui.ActionButton("Button_Feedback", feedbackRect, feedbackLabel, _secondaryButton))
            {
                _modalMenu.GoToPage(FeedbackModal.Modal);
            }

            y += buttonHeight + DIVIDER_GAP;
            ui.DottedDivider(Widgets.Rect(buttonX, y, buttonWidth, DIVIDER_HEIGHT));
            y += DIVIDER_HEIGHT + DIVIDER_GAP;

            IntRect exitRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
            string saveAndExitLabel = _localization.GetRequired("ui.button.saveAndExit");
            if (ui.ActionButton("Button_SaveAndExit", exitRect, saveAndExitLabel, _dangerButton))
            {
                _gameSaveManager.SaveAndExit();
            }
        }

        return Result.FromSuccess();
    }
}
