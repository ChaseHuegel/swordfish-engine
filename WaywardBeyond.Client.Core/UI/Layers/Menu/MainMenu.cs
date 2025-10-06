using Microsoft.Extensions.Logging;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core.UI.Layers.Menu;

internal sealed class MainMenu(
    ILogger<Menu<MenuPage>> logger,
    ReefContext reefContext,
    IMenuPage<MenuPage>[] pages
) : Menu<MenuPage>(logger, reefContext, pages)
{
    public override bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.MainMenu;
    }
}