using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using System.Threading.Tasks;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Shows the playtest notice with links to the community pages.</summary>
internal class PlaytestNoticeModal : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.playtest");

    private readonly ILocalization _localization;
    private readonly ExternalAppService _externalAppService;
    private readonly SoundEffectService _soundEffectService;
    private readonly PlaytestNoticeView _view = new();
    private Menu<Modal>? _menu;

    public PlaytestNoticeModal(
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in ExternalAppService externalAppService
    )
    {
        _soundEffectService = soundEffectService;
        _localization = localization;
        _externalAppService = externalAppService;

        _view.CloseRequested = Close;
        _view.DiscordRequested = OpenDiscord;
        _view.SteamRequested = OpenSteam;
    }

    public Modal ID => Modal;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        _menu = menu;

        _view.Render(ui, new PlaytestNoticeState(
            _localization.GetRequired("ui.dialog.playtest"),
            _localization.GetRequired("ui.dialog.playtest.body"),
            _localization.GetRequired("ui.button.ok"),
            MenuGlyphs.DISCORD,
            MenuGlyphs.STEAM
        ));

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
