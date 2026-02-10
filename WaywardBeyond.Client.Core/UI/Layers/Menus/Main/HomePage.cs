using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class HomePage(
    in Entry entry,
    in IAudioService audioService,
    in VolumeSettings volumeSettings,
    in ILocalization localization
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Home;
    
    private readonly Entry _entry = entry;
    private readonly ILocalization _localization = localization;
    
    private readonly Widgets.ButtonOptions _buttonOptions = new(
        new FontOptions {
            Size = 32,
        },
        new Widgets.AudioOptions(audioService, volumeSettings)
    );
    
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.TextButton(id: "Button_Singleplayer", text: _localization.GetString("ui.button.singleplayer")!, _buttonOptions, out Widgets.Interactions interactions))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            if (interactions.Has(Widgets.Interactions.Click))
            {
                menu.GoToPage(MenuPage.Singleplayer);
            }
        }
        
        using (ui.Text(_localization.GetString("ui.button.multiplayer")!))
        {
            ui.FontOptions = _buttonOptions.FontOptions;
            ui.Color = new Vector4(0.325f, 0.325f, 0.325f, 1f);
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
        }
        
        using (ui.TextButton(id: "Button_Settings", text: _localization.GetString("ui.button.settings")!, _buttonOptions, out Widgets.Interactions interactions))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            if (interactions.Has(Widgets.Interactions.Click))
            {
                menu.GoToPage(MenuPage.Settings);
            }
        }
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }
        
        using (ui.TextButton(id: "Button_Quit", text: _localization.GetString("ui.button.quit")!, _buttonOptions, out Widgets.Interactions interactions))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            if (interactions.Has(Widgets.Interactions.Click))
            {
                _entry.Quit();
            }
        }
        
        return Result.FromSuccess();
    }
}