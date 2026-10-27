using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Components;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class CreditsPage : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Credits;

    private readonly SoundEffectService _soundEffectService;
    private readonly CreditsView _view;
    private readonly ScrollAreaState _scroll = new();
    private Menu<MenuPage>? _menu;

    public CreditsPage(
        in SoundEffectService soundEffectService,
        in ILocalization localization
    )
    {
        _soundEffectService = soundEffectService;
        _view = new CreditsView(localization, _scroll)
        {
            BackRequested = Back
        };
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        _menu = menu;

        _scroll.Measure(ui, "Credits.Scroll");

        _view.Render(ui);
        return Result.FromSuccess();
    }

    private void Back()
    {
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        _menu?.GoToPage(MenuPage.Home);
    }
}
