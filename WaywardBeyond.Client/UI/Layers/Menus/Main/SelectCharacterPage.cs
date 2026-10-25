using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class SelectCharacterPage(
    in CharacterSaveManager characterSaveManager,
    in ICharacterStorage characterStorage,
    in IInputService inputService,
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in CharacterAssetService characterAssetService,
    in GameSaveManager gameSaveManager,
    in ModalMenu modalMenu,
    in ConfirmModal confirmModal
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectCharacter;
    
    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;
    private readonly ICharacterStorage _characterStorage = characterStorage;
    private readonly IInputService _inputService = inputService;
    private readonly ILocalization _localization = localization;
    private readonly CharacterAssetService _characterAssetService = characterAssetService;
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly ModalMenu _modalMenu = modalMenu;
    private readonly ConfirmModal _confirmModal = confirmModal;

    private readonly Widgets.ActionButtonOptions _primaryButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Primary,
        soundEffectService
    ) with
    {
        Anchors = Anchors.Center,
    };

    private readonly Widgets.ActionButtonOptions _subtleButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Subtle,
        soundEffectService
    ) with
    {
        Anchors = Anchors.Center,
    };

    private readonly Widgets.ActionButtonOptions _characterRowButton = Widgets.ActionButtonOptions.ListRow(
        ButtonVariant.Secondary,
        soundEffectService
    );

    private readonly Widgets.ActionButtonOptions _deleteIconButton = Widgets.ActionButtonOptions.Icon(
        ButtonVariant.Danger,
        soundEffectService,
        size: UiMetrics.SmallIconButtonSize,
        fontSize: UiMetrics.IconFont,
        fontId: "Font Awesome 6 Free Solid"
    );

    private int _scrollY;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.Element())
        {
            ui.Padding = new Padding(8);
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            using (ui.Text(_localization.GetString("ui.menu.characters")!))
            {
                ui.FontSize = UiMetrics.TitleFont;
            }
        }

        using (ui.Element())
        {
            ui.Spacing = 20;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            if (_characterSaveManager.ActiveSave != null)
            {
                Character activeCharacter = _characterSaveManager.ActiveSave.Value;
                
                Material appearanceMaterial = _characterAssetService.GetAppearanceMaterial(activeCharacter);
                using (ui.Image(appearanceMaterial))
                {
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                        Width = new Fixed(196),
                        Height = new Fixed(196),
                    };
                }
                
                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.Vertical;
                    ui.Spacing = 4;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };

                    using (ui.Element())
                    {
                        ui.Spacing = 8;
                        using (ui.Text(_localization.GetString("ui.label.spacer.s")!))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }
                        
                        using (ui.Text(activeCharacter.Strength.ToString()))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            ui.FontSize = UiMetrics.BodyFont;
                            ui.Color = UiTheme.TextSoft;
                        }
                    }

                    using (ui.Element())
                    {
                        ui.Spacing = 8;
                        using (ui.Text(_localization.GetString("ui.label.spacer.p")!))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }

                        using (ui.Text(activeCharacter.Precision.ToString()))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            ui.FontSize = UiMetrics.BodyFont;
                            ui.Color = UiTheme.TextSoft;
                        }
                    }

                    using (ui.Element())
                    {
                        ui.Spacing = 8;
                        using (ui.Text(_localization.GetString("ui.label.spacer.a")!))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }

                        using (ui.Text(activeCharacter.Awareness.ToString()))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            ui.FontSize = UiMetrics.BodyFont;
                            ui.Color = UiTheme.TextSoft;
                        }
                    }

                    using (ui.Element())
                    {
                        ui.Spacing = 8;
                        using (ui.Text(_localization.GetString("ui.label.spacer.c")!))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }

                        using (ui.Text(activeCharacter.Charisma.ToString()))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            ui.FontSize = UiMetrics.BodyFont;
                            ui.Color = UiTheme.TextSoft;
                        }
                    }

                    using (ui.Element())
                    {
                        ui.Spacing = 8;
                        using (ui.Text(_localization.GetString("ui.label.spacer.e")!))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }

                        using (ui.Text(activeCharacter.Education.ToString()))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            ui.FontSize = UiMetrics.BodyFont;
                            ui.Color = UiTheme.TextSoft;
                        }
                    }

                    using (ui.Element())
                    {
                        ui.Spacing = 8;
                        using (ui.Text(_localization.GetString("ui.label.spacer.r")!))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }

                        using (ui.Text(activeCharacter.Resolve.ToString()))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            ui.FontSize = UiMetrics.BodyFont;
                            ui.Color = UiTheme.TextSoft;
                        }
                    }
                }
            }

            using (ui.Element("characters"))
            {
                ui.VerticalScroll = true;

                ui.ClipConstraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Relative(1f),
                };

                _characterSaveManager.ActiveSave ??= _characterSaveManager.GetMostRecentSave();

                List<Character> characters = _characterStorage.GetAllCharacters().ToList();
                if (characters.Count == 0)
                {
                    using (ui.Text(_localization.GetString("ui.text.none")!))
                    {
                        ui.FontSize = UiMetrics.BodyFont;
                        ui.Color = UiTheme.TextDim;
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Center,
                        };
                    }
                }
                else
                {
                    float scroll = _inputService.GetMouseScroll();
                    _scrollY = Math.Clamp(_scrollY + (int)scroll, -(characters.Count - 1), 0);
                    ui.ScrollY = _scrollY * 30;
                    ui.Spacing = 20;

                    using (ui.Element())
                    {
                        ui.LayoutDirection = LayoutDirection.Vertical;
                        ui.Spacing = 12;
                    
                        foreach (Character character in characters.OrderByDescending(character => character.Playtime.LastPlayedMs))
                        {
                            if (ui.ActionButton($"Button_DeleteCharacter_{character.Uuid}", "\uf2ed", _deleteIconButton))
                            {
                                _characterSaveManager.ActiveSave = character;
                                _modalMenu.GoToPage(_confirmModal.Create(DeleteSave));

                                void DeleteSave()
                                {
                                    _characterSaveManager.Delete(character);
                                    _characterSaveManager.ActiveSave = _characterSaveManager.GetMostRecentSave();
                                }
                            }
                        }
                    }
                    
                    using (ui.Element())
                    {
                        ui.LayoutDirection = LayoutDirection.Vertical;

                        foreach (Character character in characters.OrderByDescending(character => character.Playtime.LastPlayedMs))
                        {
                            bool selected = _characterSaveManager.ActiveSave != null &&
                                _characterSaveManager.ActiveSave.Value.Uuid == character.Uuid;
                            Widgets.ActionButtonOptions options = _characterRowButton with
                            {
                                Selected = selected,
                            };

                            if (ui.ActionButton($"Button_SelectCharacter_{character.Uuid}", character.Name, options))
                            {
                                _characterSaveManager.ActiveSave = character;
                            }
                        }
                    }
                }
            }
        }
        
        if (_characterSaveManager.ActiveSave != null)
        {
            Character activeCharacter = _characterSaveManager.ActiveSave.Value;
            
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

                    string lastPlayedStr = activeCharacter.Playtime.LastPlayedMs > 0
                        ? DateTimeOffset.FromUnixTimeMilliseconds(activeCharacter.Playtime.LastPlayedMs).ToLocalTime().ToString(format: "g")
                        : _localization.GetString("ui.label.never")!;
                    
                    using (ui.Text($"{_localization.GetString("ui.label.lastPlayed")!} #BFBFBFFF {lastPlayedStr}")) { }
                }

                TimeSpan age = TimeSpan.FromMilliseconds(activeCharacter.Playtime.AgeMs);
                string timePlayedStr = _localization.GetLongString(age);
                using (ui.Element())
                {
                    ui.Spacing = 8;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };
                    
                    using (ui.Text($"{_localization.GetString("ui.label.timePlayed")!} #BFBFBFFF {timePlayedStr}")) { }
                }
            }
            
            if (ui.ActionButton("Button_LoadSave", _localization.GetString("ui.button.loadSave")!, _primaryButton))
            {
                Task.Run(_gameSaveManager.Load);
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
            
            if (ui.ActionButton("Button_Back", _localization.GetString("ui.button.back")!, _subtleButton))
            {
                menu.GoBack();
            }
        }
        
        return Result.FromSuccess();
    }
}