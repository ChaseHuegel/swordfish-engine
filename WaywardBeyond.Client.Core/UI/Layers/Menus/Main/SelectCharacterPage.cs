using System;
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

internal sealed class SelectCharacterPage(
    in CharacterSaveManager characterSaveManager,
    in CharacterSaveService characterSaveService,
    in IInputService inputService,
    in SoundEffectService soundEffectService,
    in ILocalization localization
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectCharacter;
    
    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;
    private readonly CharacterSaveService _characterSaveService = characterSaveService;
    private readonly IInputService _inputService = inputService;
    private readonly ILocalization _localization = localization;

    private readonly Widgets.ButtonOptions _menuButtonOptions = new(
        new FontOptions 
        {
            Size = 32,
        },
        new Widgets.AudioOptions(soundEffectService)
    );

    private readonly Widgets.ButtonOptions _buttonOptions = new(
        new FontOptions
        {
            Size = 20,
        },
        new Widgets.AudioOptions(soundEffectService)
    );
    
    private int _scrollY;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            using (ui.Text(_localization.GetString("ui.menu.characters")!))
            {
                ui.FontSize = 24;
            }
        }

        using (ui.Element("characters"))
        {
            ui.VerticalScroll = true;
            ui.LayoutDirection = LayoutDirection.Vertical;

            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            ui.ClipConstraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };
            
            CharacterSave[] saves = _characterSaveService.GetSaves();
            if (saves.Length == 0)
            {
                using (ui.Text(_localization.GetString("ui.text.none")!))
                {
                    ui.FontOptions = _buttonOptions.FontOptions;
                    ui.Color = new Vector4(0.325f, 0.325f, 0.325f, 1f);
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };
                }
            }
            else
            {
                float scroll = _inputService.GetMouseScroll();
                _scrollY = Math.Clamp(_scrollY + (int)scroll, -(saves.Length - 1), 0);
                ui.ScrollY = _scrollY * 30;
                
                for (var i = 0; i < saves.Length; i++)
                {
                    CharacterSave save = saves[i];
                    using (ui.TextButton(id: $"Button_SelectCharacter_{i}", text: save.Character.Name, _buttonOptions, out Widgets.Interactions interactions))
                    {
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Center,
                        };

                        if (_characterSaveManager.ActiveSave != null && _characterSaveManager.ActiveSave.Value.Path == save.Path)
                        {
                            ui.Color = new Vector4(0.5f);
                        }

                        if (interactions.Has(Widgets.Interactions.Click))
                        {
                            _characterSaveManager.ActiveSave = save;
                            Task.Run(_characterSaveManager.Load);
                        }
                    }
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