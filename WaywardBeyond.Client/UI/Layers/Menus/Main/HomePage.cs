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

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class HomePage(
    in Entry entry,
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in GameSaveService gameSaveService,
    in ModalMenu modalMenu
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Home;

    private readonly Entry _entry = entry;
    private readonly ILocalization _localization = localization;
    private readonly GameSaveService _gameSaveService = gameSaveService;
    private readonly ModalMenu _modalMenu = modalMenu;

    private readonly Widgets.ActionButtonOptions _primaryButton = MenuButton(ButtonVariant.Primary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _secondaryButton = MenuButton(ButtonVariant.Secondary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _dangerButton = MenuButton(ButtonVariant.Danger, soundEffectService);

    private bool _hasShownNotice;

    private static Widgets.ActionButtonOptions MenuButton(ButtonVariant variant, SoundEffectService soundEffectService)
    {
        return Widgets.ActionButtonOptions.Menu(variant, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        if (!_hasShownNotice)
        {
            _hasShownNotice = true;
            _modalMenu.GoToPage(PlaytestNoticeModal.Modal);
        }

        GameSave[] saves = _gameSaveService.GetSaves();
        if (saves.Length > 0)
        {
            if (ui.ActionButton("Button_SelectSave", _localization.GetString("ui.button.selectSave")!, _primaryButton))
            {
                menu.GoToPage(MenuPage.SelectSave);
            }
        }

        if (_gameSaveService.CanCreateSave)
        {
            if (ui.ActionButton("Button_NewSave", _localization.GetString("ui.button.newSave")!, _secondaryButton))
            {
                menu.GoToPage(MenuPage.NewSave);
            }
        }

        if (ui.ActionButton("Button_Multiplayer", _localization.GetString("ui.button.multiplayer")!, _secondaryButton))
        {
            menu.GoToPage(MenuPage.Multiplayer);
        }

        if (ui.ActionButton("Button_Settings", _localization.GetString("ui.button.settings")!, _secondaryButton))
        {
            menu.GoToPage(MenuPage.Settings);
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

        if (ui.ActionButton("Button_Quit", _localization.GetString("ui.button.quit")!, _dangerButton))
        {
            _entry.Quit();
        }

        return Result.FromSuccess();
    }
}
