using Microsoft.Extensions.Logging;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core.UI;

internal sealed class MainMenu(
    ILogger<Menu<MainMenuPage>> logger,
    ReefContext reefContext,
    IMenuPage<MainMenuPage>[] pages
) : Menu<MainMenuPage>(logger, reefContext, pages)
{
    public override bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.MainMenu;
    }
}