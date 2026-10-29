using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using System.Threading.Tasks;
using WaywardBeyond.Client.Services;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Shows the playtest notice with links to the community pages.</summary>
internal class PlaytestNoticeModal : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.playtest");

    private readonly ExternalAppService _externalAppService;
    private readonly SoundEffectService _soundEffectService;
    private readonly PlaytestNoticeView _view;
    private Menu<Modal>? _menu;

    public PlaytestNoticeModal(
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in ExternalAppService externalAppService
    )
    {
        _soundEffectService = soundEffectService;
        _externalAppService = externalAppService;
        _view = new PlaytestNoticeView(localization);

        _view.CloseRequested = Close;
        _view.DiscordRequested = OpenDiscord;
        _view.SteamRequested = OpenSteam;
    }

    public Modal ID => Modal;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        _menu = menu;

        _view.Render(ui);

        return Result.FromSuccess();
    }

    private void Close()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        _menu?.GoBack();
    }

    private void OpenDiscord()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        Task.Run(_externalAppService.TryOpenDiscordAsync);
    }

    private void OpenSteam()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        Task.Run(_externalAppService.TryOpenSteamAsync);
    }
}
