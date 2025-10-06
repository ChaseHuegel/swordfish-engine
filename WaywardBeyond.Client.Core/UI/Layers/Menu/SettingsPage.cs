using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI.Layers.Menu;

internal sealed class SettingsPage : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Settings;
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
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

            if (ui.TextButton(id: "Button_Display", text: "Display", _buttonFontOptions))
            {
                menu.GoToPage(MenuPage.DisplaySettings);
            }
            
            ui.TextButton(id: "Button_Controls", text: "Controls", _buttonFontOptions);

            if (ui.TextButton(id: "Button_MainMenu", text: "Back", _buttonFontOptions))
            {
                menu.GoBack();
            }
        }
        
        return Result.FromSuccess();
    }
}