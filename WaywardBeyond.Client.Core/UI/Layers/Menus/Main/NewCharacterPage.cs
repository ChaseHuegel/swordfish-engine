using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Client.Core.Services;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class NewCharacterPage : IMenuPage<MenuPage>
{
    private static readonly char[] _characterNameTrimChars = [' ', '\t', '.', '\n', '\r'];

    public MenuPage ID => MenuPage.NewCharacter;
    
    private readonly CharacterSaveService _characterSaveService;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;

    private readonly Widgets.ButtonOptions _menuButtonOptions;
    private readonly Widgets.ButtonOptions _buttonOptions;

    private TextBoxState _nameTextBox;

    public NewCharacterPage(
        in CharacterSaveService characterSaveService,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization
    ) {
        _characterSaveService = characterSaveService;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;

        _menuButtonOptions = new Widgets.ButtonOptions(
            new FontOptions {
                Size = 32,
            },
            new Widgets.AudioOptions(soundEffectService)
        );
        
        _buttonOptions = new Widgets.ButtonOptions(
            new FontOptions {
                Size = 20,
            },
            new Widgets.AudioOptions(soundEffectService)
        );

        var saveNameTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.characterName"),
            MaxCharacters: 20,
            DisallowedCharacters: ['\0', '\\', '/', ':', '*', '?', '"', '<', '>', '|'],
            Constraints: new Constraints
            {
                Width = new Fixed(300),
            }
        );
        _nameTextBox = new TextBoxState(initialValue: string.Empty, options: saveNameTextBoxOptions);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            using (ui.Text(_localization.GetString("ui.menu.createCharacter")!))
            {
                ui.FontSize = 24;
            }
        }
        
        using (ui.Element())
        {
            ui.Spacing = 8;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            ui.TextBox(id: "TextBox_CharacterName", state: ref _nameTextBox, _buttonOptions.FontOptions, _inputService, _soundEffectService);

            var validName = true;
            string nameValue = _nameTextBox.Text.ToString().Trim(_characterNameTrimChars);
            if (string.IsNullOrWhiteSpace(nameValue))
            {
                using (ui.Text(_localization.GetString("ui.notification.name.required")!))
                {
                    ui.Color = new Vector4(1f, 0f, 0f, 1f);
                }
                validName = false;
            }
            else if (_characterSaveService.GetSaves().Any(save => save.Character.Name == nameValue))
            {
                using (ui.Text(_localization.GetString("ui.notification.name.taken")!))
                {
                    ui.Color = new Vector4(1f, 0f, 0f, 1f);
                }
                validName = false;
            }

            using (ui.TextButton(id: "Button_CreateCharacter", text: _localization.GetString("ui.button.createCharacter")!, _buttonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
                
                if (validName && interactions.Has(Widgets.Interactions.Click))
                {
                    var character = new Character(WaywardBeyond.Version, Guid.NewGuid().ToString(), _LastPlayedMs: 0, _AgeMs: 0, nameValue);
                    Task.Run(() => _characterSaveService.CreateSave(character));
                }
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
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            using (ui.TextButton(id: "Button_Back", text: _localization.GetString("ui.button.back")!, _menuButtonOptions, out Widgets.Interactions interactions))
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
        }
        
        return Result.FromSuccess();
    }
}