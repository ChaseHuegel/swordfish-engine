using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class HomePage(
    in Entry entry,
    in IAudioService audioService,
    in VolumeSettings volumeSettings,
    in ILocalization localization,
    in GameSaveService gameSaveService
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Home;
    
    private readonly Entry _entry = entry;
    private readonly ILocalization _localization = localization;
    private readonly GameSaveService _gameSaveService = gameSaveService;
    
    private readonly Widgets.ButtonOptions _buttonOptions = new(
        new FontOptions {
            Size = 32,
        },
        new Widgets.AudioOptions(audioService, volumeSettings)
    );
    
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        GameSave[] saves = _gameSaveService.GetSaves();
        if (saves.Length > 0)
        {
            using (ui.TextButton(id: "Button_SelectSave", text: _localization.GetString("ui.button.selectSave")!, _buttonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };

                if (interactions.Has(Widgets.Interactions.Click))
                {
                    menu.GoToPage(MenuPage.SelectSave);
                }
            }
        }

        using (ui.TextButton(id: "Button_NewSave", text: _localization.GetString("ui.button.newSave")!, _buttonOptions, out Widgets.Interactions interactions))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            if (interactions.Has(Widgets.Interactions.Click))
            {
                menu.GoToPage(MenuPage.NewSave);
            }
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
        
        using (ui.TextButton(id: "Button_Feedback", text: _localization.GetString("ui.button.feedback")!, _buttonOptions, out Widgets.Interactions interactions))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            if (interactions.Has(Widgets.Interactions.Click))
            {
                menu.GoToPage(MenuPage.Feedback);
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