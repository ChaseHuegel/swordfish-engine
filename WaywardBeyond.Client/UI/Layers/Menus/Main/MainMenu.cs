using Microsoft.Extensions.Logging;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;
using System.Threading.Tasks;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class MainMenu : TitleMenu<MenuPage>
{
    private readonly ExternalAppService _externalAppService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ModalMenu _modalMenu;
    private readonly MainMenuOverlayView _view;

    public MainMenu(
        ILogger<Menu<MenuPage>> logger,
        IAssetDatabase<Material> materialDatabase,
        ReefContext reefContext,
        IShortcutService shortcutService,
        SoundEffectService soundEffectService,
        ExternalAppService externalAppService,
        ILocalization localization,
        ModalMenu modalMenu,
        IMenuPage<MenuPage>[] pages
    ) : base(logger, materialDatabase, reefContext, pages)
    {
        _externalAppService = externalAppService;
        _soundEffectService = soundEffectService;
        _modalMenu = modalMenu;
        _view = new MainMenuOverlayView(localization)
        {
            FeedbackRequested = Feedback,
            DiscordRequested = OpenDiscord,
            SteamRequested = OpenSteam
        };

        Shortcut backShortcut = new(
            name: "Go back",
            category: "General",
            ShortcutModifiers.None,
            Key.Esc,
            isEnabled: IsVisible,
            action: () => GoBack()
        );
        shortcutService.RegisterShortcut(backShortcut);

        WaywardBeyond.GameState.Changed += OnGameStateChanged;
    }

    public override bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.MainMenu;
    }

    public override Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (GetCurrentPage() != MenuPage.Home)
        {
            return base.RenderUI(delta, ui);
        }

        _view.Render(ui);
        return base.RenderUI(delta, ui);
    }

    private void Feedback()
    {
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        _modalMenu.GoToPage(FeedbackModal.Modal);
    }

    private void OpenDiscord()
    {
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        Task.Run(_externalAppService.TryOpenDiscordAsync);
    }

    private void OpenSteam()
    {
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        Task.Run(_externalAppService.TryOpenSteamAsync);
    }

    private void OnGameStateChanged(object? sender, DataChangedEventArgs<GameState> e)
    {
        if (e.NewValue != GameState.MainMenu)
        {
            return;
        }

        GoToPage(MenuPage.Home);
    }
}
