using System.Collections.Generic;
using System.Linq;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class CharactersPage(
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in ICharacterStorage characterSaveService
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Characters;

    private readonly ILocalization _localization = localization;
    private readonly ICharacterStorage _characterSaveService = characterSaveService;

    private readonly Widgets.ActionButtonOptions _primaryButton = MenuButton(ButtonVariant.Primary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _secondaryButton = MenuButton(ButtonVariant.Secondary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _subtleButton = MenuButton(ButtonVariant.Subtle, soundEffectService);

    private static Widgets.ActionButtonOptions MenuButton(ButtonVariant variant, SoundEffectService soundEffectService)
    {
        return Widgets.ActionButtonOptions.Menu(variant, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        IEnumerable<Character> characters = _characterSaveService.GetAllCharacters();
        if (characters.Any())
        {
            if (ui.ActionButton("Button_SelectCharacter", _localization.GetString("ui.button.selectCharacter")!, _primaryButton))
            {
                menu.GoToPage(MenuPage.SelectCharacter);
            }
        }

        if (ui.ActionButton("Button_NewCharacter", _localization.GetString("ui.button.newCharacter")!, _secondaryButton))
        {
            menu.GoToPage(MenuPage.NewCharacter);
        }

        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }

        if (ui.ActionButton("Button_Back", _localization.GetString("ui.button.back")!, _subtleButton))
        {
            menu.GoBack();
        }

        return Result.FromSuccess();
    }
}
