using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI;

internal sealed class MainMenuSettings : IMenuPage<MainMenuPage>
{
    public MainMenuPage ID => MainMenuPage.Settings;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MainMenuPage> menu)
    {
        return Result.FromSuccess();
    }
}