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
    private readonly IAudioService _audioService = audioService;
    private readonly VolumeSettings _volumeSettings = volumeSettings;
    private readonly ILocalization _localization = localization;
    
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        if (ui.TextButton(id: "Button_Singleplayer", text: _localization.GetString("ui.button.singleplayer")!, _buttonFontOptions, _audioService, _volumeSettings))
        {
            menu.GoToPage(MenuPage.Singleplayer);
        }

        using (ui.Text(_localization.GetString("ui.button.multiplayer")!))
        {
            ui.FontOptions = _buttonFontOptions;
            ui.Color = new Vector4(0.325f, 0.325f, 0.325f, 1f);
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Top,
                X = new Relative(0.5f),
            };
        }

        if (ui.TextButton(id: "Button_Settings", text: _localization.GetString("ui.button.settings")!, _buttonFontOptions, _audioService, _volumeSettings))
        {
            menu.GoToPage(MenuPage.Settings);
        }
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }
        
        if (ui.TextButton(id: "Button_Quit", text: _localization.GetString("ui.button.quit")!, _buttonFontOptions, _audioService, _volumeSettings))
        {
            _entry.Quit();
        }
        
        return Result.FromSuccess();
    }
}