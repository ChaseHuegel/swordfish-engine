using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;

namespace WaywardBeyond.Client.UI.Layers.Menus.Pause;

internal sealed class HomePage : IMenuPage<PausePage>
{
    public PausePage ID => PausePage.Home;

    private readonly HomePageView _view;
    private readonly GameSaveManager _gameSaveManager;
    private readonly ModalMenu _modalMenu;
    private readonly SoundEffectService _soundEffectService;
    private Menu<PausePage>? _menu;

    public HomePage(
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in GameSaveManager gameSaveManager,
        in ModalMenu modalMenu
    ) {
        _soundEffectService = soundEffectService;
        _gameSaveManager = gameSaveManager;
        _modalMenu = modalMenu;
        _view = new HomePageView(localization)
        {
            ContinueRequested = Continue,
            SettingsRequested = OpenSettings,
            FeedbackRequested = OpenFeedback,
            SaveAndExitRequested = SaveAndExit
        };
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<PausePage> menu)
    {
        _menu = menu;
        _view.Render(ui);
        return Result.FromSuccess();
    }

    private void Continue()
    {
        PlayClick();
        WaywardBeyond.Unpause();
    }

    private void OpenSettings()
    {
        PlayClick();
        _menu?.GoToPage(PausePage.Settings);
    }

    private void OpenFeedback()
    {
        PlayClick();
        _modalMenu.GoToPage(FeedbackModal.Modal);
    }

    private void SaveAndExit()
    {
        PlayClick();
        _gameSaveManager.SaveAndExit();
    }

    private void PlayClick()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
    }
}
