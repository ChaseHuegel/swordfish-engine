using System.Numerics;
using Reef;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI.Layers.Menu;

internal sealed class HomePage(in Entry entry, in IAudioService audioService, in VolumeSettings volumeSettings) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Home;

    private readonly Entry _entry = entry;
    private readonly IAudioService _audioService = audioService;
    private readonly VolumeSettings _volumeSettings = volumeSettings;
    
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
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
                Y = new Relative(0.5f),
            };

            if (ui.TextButton(id: "Button_Singleplayer", text: "Singleplayer", _buttonFontOptions, _audioService, _volumeSettings))
            {
                menu.GoToPage(MenuPage.Singleplayer);
            }

            using (ui.Text("Multiplayer"))
            {
                ui.FontOptions = _buttonFontOptions;
                ui.Color = new Vector4(0.325f, 0.325f, 0.325f, 1f);
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    X = new Relative(0.5f),
                };
            }

            if (ui.TextButton(id: "Button_Settings", text: "Settings", _buttonFontOptions, _audioService, _volumeSettings))
            {
                menu.GoToPage(MenuPage.Settings);
            }
        }
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                X = new Relative(0.5f),
                Y = new Relative(0.99f),
            };

            if (ui.TextButton(id: "Button_Quit", text: "Quit", _buttonFontOptions, _audioService, _volumeSettings))
            {
                _entry.Quit();
            }
        }
        
        return Result.FromSuccess();
    }
}