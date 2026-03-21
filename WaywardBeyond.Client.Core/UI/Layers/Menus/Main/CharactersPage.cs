using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Client.Core.Services;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class CharactersPage(
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in CharacterSaveService characterSaveService
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Characters;
    
    private readonly ILocalization _localization = localization;
    private readonly CharacterSaveService _characterSaveService = characterSaveService;

    private readonly Widgets.ButtonOptions _buttonOptions = new(
        new FontOptions {
            Size = 32,
        },
        new Widgets.AudioOptions(soundEffectService)
    );
    
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        CharacterSave[] characters = _characterSaveService.GetSaves();
        if (characters.Length > 0)
        {
            using (ui.TextButton(id: "Button_SelectCharacter", text: _localization.GetString("ui.button.selectCharacter")!, _buttonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
                
                if (interactions.Has(Widgets.Interactions.Click))
                {
                    menu.GoToPage(MenuPage.SelectCharacter);
                }
            }
        }
        
        using (ui.TextButton(id: "Button_NewCharacter", text: _localization.GetString("ui.button.newCharacter")!, _buttonOptions, out Widgets.Interactions interactions))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            if (interactions.Has(Widgets.Interactions.Click))
            {
                menu.GoToPage(MenuPage.NewCharacter);
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
        
        using (ui.TextButton(id: "Button_Back", text: _localization.GetString("ui.button.back")!, _buttonOptions, out Widgets.Interactions interactions))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            if (interactions.Has(Widgets.Interactions.Click))
            {
                menu.GoBack();
            }
        }
        
        return Result.FromSuccess();
    }
}