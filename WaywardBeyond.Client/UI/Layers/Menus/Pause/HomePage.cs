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

    private readonly ILocalization _localization = localization;
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly ModalMenu _modalMenu = modalMenu;

    private readonly Widgets.ActionButtonOptions _primaryButton = MenuButton(ButtonVariant.Primary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _secondaryButton = MenuButton(ButtonVariant.Secondary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _dangerButton = MenuButton(ButtonVariant.Danger, soundEffectService);

    private static Widgets.ActionButtonOptions MenuButton(ButtonVariant variant, SoundEffectService soundEffectService)
    {
        return Widgets.ActionButtonOptions.Menu(variant, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<PausePage> menu)
    {
        if (ui.ActionButton("Button_Continue", _localization.GetString("ui.button.continue")!, _primaryButton))
        {
            WaywardBeyond.Unpause();
        }

        if (ui.ActionButton("Button_Settings", _localization.GetString("ui.button.settings")!, _secondaryButton))
        {
            menu.GoToPage(PausePage.Settings);
        }

        if (ui.ActionButton("Button_Feedback", _localization.GetString("ui.button.feedback")!, _secondaryButton))
        {
            _modalMenu.GoToPage(FeedbackModal.Modal);
        }

        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }

        if (ui.ActionButton("Button_SaveAndExit", _localization.GetString("ui.button.saveAndExit")!, _dangerButton))
        {
            _gameSaveManager.SaveAndExit();
        }

        return Result.FromSuccess();
    }
}
