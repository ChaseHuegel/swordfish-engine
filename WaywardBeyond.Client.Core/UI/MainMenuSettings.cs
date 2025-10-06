using System.Threading.Tasks;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI;

internal sealed class MainMenuSettings : IMenuPage<MainMenuPage>
{
    public MainMenuPage ID => MainMenuPage.Settings;
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MainMenuPage> menu)
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Left,
                X = new Relative(0.01f),
                Y = new Relative(0.5f),
            };

            ui.TextButton(id: "Button_Controls", text: "Controls", _buttonFontOptions);
            ui.TextButton(id: "Button_Display", text: "Display", _buttonFontOptions);
            ui.TextButton(id: "Button_Audio", text: "Audio", _buttonFontOptions);

            if (ui.TextButton(id: "Button_MainMenu", text: "Back", _buttonFontOptions))
            {
                menu.GoToPage(MainMenuPage.Home);
            }
        }
        
        return Result.FromSuccess();
    }
}