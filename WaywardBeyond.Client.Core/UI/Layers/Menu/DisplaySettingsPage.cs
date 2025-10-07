using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI.Layers.Menu;

internal sealed class DisplaySettingsPage(in DisplaySettings displaySettings) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.DisplaySettings;

    private readonly DisplaySettings _displaySettings = displaySettings;
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

            if (ui.TextButton(id: "Button_Back", text: "Back", _buttonFontOptions))
            {
                menu.GoBack();
            }
        }
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Top,
                X = new Relative(0.5f),
                Y = new Relative(0.25f),
                Width = new Relative(0.25f),
                Height = new Relative(1f),
            };
            
            bool value = ui.Checkbox(id: "Checkbox_Fullscreen", text: "Fullscreen", _displaySettings.Fullscreen.Get());
            _displaySettings.Fullscreen.Set(value);
        }
        
        return Result.FromSuccess();
    }
}