using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI;
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
        _view = new CreditsView(localization, _scroll);
        _view.BackRequested = Back;
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        _menu = menu;

        ElementBounds viewport = ui.GetElementBounds("Credits.Scroll");
        ElementBounds content = ui.GetElementBounds("Credits.Content");
        if (viewport.Rect.Size.Y > 0 && content.Rect.Size.Y > 0)
        {
            _scroll.SetMetrics(viewport.Rect.Size.Y, content.Rect.Size.Y);
        }

        _view.Render(ui);
        return Result.FromSuccess();
    }

    private void Back()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        _menu?.GoToPage(MenuPage.Home);
    }
}
