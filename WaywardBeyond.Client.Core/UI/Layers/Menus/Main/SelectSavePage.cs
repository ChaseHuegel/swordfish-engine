using System;
using System.Linq;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Extensions;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Client.Core.Services;
using WaywardBeyond.Client.Core.UI.Layers.Menus.Modal;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class SelectSavePage(
    in GameSaveManager gameSaveManager,
    in GameSaveService gameSaveService,
    in IInputService inputService,
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in CharacterSaveService characterSaveService,
    in ModalMenu modalMenu,
    in ConfirmModal confirmModal
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectSave;
    
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly GameSaveService _gameSaveService = gameSaveService;
    private readonly IInputService _inputService = inputService;
    private readonly ILocalization _localization = localization;
    private readonly CharacterSaveService _characterSaveService = characterSaveService;
    private readonly ModalMenu _modalMenu = modalMenu;
    private readonly ConfirmModal _confirmModal = confirmModal;

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
    
    private readonly Widgets.ButtonOptions _iconOptions = new(
        new FontOptions {
            ID = "Font Awesome 6 Free Solid",
            Size = 32,
        },
        new Widgets.AudioOptions(soundEffectService)
    );
        
    private readonly Widgets.ButtonOptions _smallIconOptions = new(
        new FontOptions {
            ID = "Font Awesome 6 Free Solid",
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

            using (ui.Text(_localization.GetString("ui.menu.saves")!))
            {
                ui.FontSize = 24;
            }
        }

        using (ui.Element("saves"))
        {
            ui.VerticalScroll = true;

            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Height = new Fixed(196),
            };

            ui.ClipConstraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };
            
            GameSave[] saves = _gameSaveService.GetSaves();
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

                using (ui.Element())
                {
                    ui.Spacing = 20;
                    
                    using (ui.Element())
                    {
                        ui.LayoutDirection = LayoutDirection.Vertical;
                        ui.Spacing = 12;
                        
                        foreach (GameSave save in saves.OrderByDescending(save => save.Level.LastPlayedMs))
                        {
                            using (ui.TextButton(id: $"Button_DeleteSave_{save.Name}", text: "\uf2ed", _smallIconOptions, out Widgets.Interactions interactions))
                            {
                                if (interactions.Has(Widgets.Interactions.Click))
                                {
                                    _modalMenu.GoToPage(_confirmModal.Create(DeleteSave));

                                    void DeleteSave()
                                    {
                                        _gameSaveManager.Delete(save);
                                        _gameSaveManager.ActiveSave = _gameSaveManager.GetMostRecentSave();
                                    }
                                }
                            }
                        }
                    }
                    
                    using (ui.Element())
                    {
                        ui.LayoutDirection = LayoutDirection.Vertical;
                        
                        foreach (GameSave save in saves.OrderByDescending(save => save.Level.LastPlayedMs))
                        {
                            using (ui.TextButton(id: $"Button_SelectSave_{save.Name}", text: save.Name, _buttonOptions, out Widgets.Interactions interactions))
                            {
                                if (_gameSaveManager.ActiveSave != null && _gameSaveManager.ActiveSave.Value.Name == save.Name)
                                {
                                    ui.Color = new Vector4(0f, 0.455f, 1f, 0.5f);
                                }

                                if (interactions.Has(Widgets.Interactions.Click))
                                {
                                    _gameSaveManager.ActiveSave = save;
                                }
                            }
                        }
                    }
                }
            }
        }
        
        if (_gameSaveManager.ActiveSave != null)
        {
            GameSave activeSave = _gameSaveManager.ActiveSave.Value;
            
            using (ui.Element())
            {
                ui.Spacing = 8;
                ui.Padding = new Padding(16);
                ui.LayoutDirection = LayoutDirection.Vertical;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
                
                using (ui.Element())
                {
                    ui.Spacing = 8;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };

                    DateTimeOffset lastPlayed = DateTimeOffset.FromUnixTimeMilliseconds(activeSave.Level.LastPlayedMs);
                    using (ui.Text(_localization.GetString("ui.label.lastPlayed")!)) { }
                    using (ui.Text(lastPlayed.ToLocalTime().ToString(format: "g")))
                    {
                        ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
                    }
                }

                TimeSpan age = TimeSpan.FromMilliseconds(activeSave.Level.AgeMs);
                string timePlayedStr = _localization.GetLongString(age);
                using (ui.Element())
                {
                    ui.Spacing = 8;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };
                    
                    using (ui.Text(_localization.GetString("ui.label.timePlayed")!)) { }
                    using (ui.Text(timePlayedStr))
                    {
                        ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
                    }
                }
            }
            
            using (ui.TextButton(id: "Button_Next", text: _localization.GetString("ui.button.next")!, _menuButtonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
            
                if (interactions.Has(Widgets.Interactions.Click))
                {
                    CharacterSave[] characters = _characterSaveService.GetSaves();
                    menu.GoToPage(characters.Length == 0 ? MenuPage.NewCharacter : MenuPage.Characters);
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