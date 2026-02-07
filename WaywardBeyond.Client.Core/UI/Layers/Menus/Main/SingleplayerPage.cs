using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class SingleplayerPage : IMenuPage<MenuPage>
{
    private static readonly char[] _saveNameTrimChars = [' ', '\t', '.'];

    public MenuPage ID => MenuPage.Singleplayer;
    
    private readonly GameSaveManager _gameSaveManager;
    private readonly GameSaveService _gameSaveService;
    private readonly IInputService _inputService;
    private readonly IAudioService _audioService;
    private readonly VolumeSettings _volumeSettings;
    private readonly ILocalization _localization;
    
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };

    private readonly FontOptions _saveFontOptions = new()
    {
        Size = 20,
    };

    private int _scrollY;
    private TextBoxState _saveNameTextBox;
    private TextBoxState _seedTextBox;

    public SingleplayerPage(in GameSaveManager gameSaveManager,
        in GameSaveService gameSaveService,
        in IInputService inputService,
        in IAudioService audioService,
        in VolumeSettings volumeSettings,
        in ILocalization localization)
    {
        _gameSaveManager = gameSaveManager;
        _gameSaveService = gameSaveService;
        _inputService = inputService;
        _audioService = audioService;
        _volumeSettings = volumeSettings;
        _localization = localization;

        var saveNameTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.saveName"),
            MaxCharacters: 20,
            DisallowedCharacters: ['\0', '\\', '/', ':', '*', '?', '"', '<', '>', '|'],
            Constraints: new Constraints
            {
                Width = new Fixed(300),
            }
        );
        _saveNameTextBox = new TextBoxState(initialValue: string.Empty, options: saveNameTextBoxOptions);
        
        var saveSeedTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.saveSeed"),
            MaxCharacters: 20,
            Constraints: new Constraints
            {
                Width = new Fixed(300),
            }
        );
        
        _seedTextBox = new TextBoxState(initialValue: string.Empty, saveSeedTextBoxOptions);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        GameSave[] saves = _gameSaveService.GetSaves();
        
        using (ui.Element())
        {
            ui.Spacing = 8;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Top,
                X = new Relative(0.5f),
                Y = new Relative(0.35f),
            };
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center | Anchors.Top,
                    X = new Relative(0.5f),
                };

                using (ui.Text(_localization.GetString("ui.menu.createSave")!))
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
                    Width = new Fixed(300),
                    Height = new Fixed(80),
                };
                
                ui.TextBox(id: "TextBox_SaveName", state: ref _saveNameTextBox, _saveFontOptions, _inputService, _audioService, _volumeSettings);
                ui.TextBox(id: "TextBox_SaveSeed", state: ref _seedTextBox, _saveFontOptions, _inputService, _audioService, _volumeSettings);
            }

            string saveNameValue = _saveNameTextBox.Text.ToString().Trim(_saveNameTrimChars);
            if (ui.TextButton(id: "Button_NewGame", text: _localization.GetString("ui.button.newGame")!, _saveFontOptions, _audioService, _volumeSettings) && !string.IsNullOrWhiteSpace(saveNameValue))
            {
                var seedValue = _seedTextBox.Text.ToString();
                string seed = string.IsNullOrWhiteSpace(seedValue) ? "wayward beyond" :  seedValue;
                var options = new GameOptions(saveNameValue, seed);
                Task.Run(() => _gameSaveManager.NewGame(options));
            }
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center | Anchors.Top,
                    X = new Relative(0.5f),
                };

                using (ui.Text(_localization.GetString("ui.menu.saves")!))
                {
                    ui.FontSize = 24;
                }
            }

            using (ui.Element())
            {
                ui.VerticalScroll = true;
                ui.LayoutDirection = LayoutDirection.Vertical;
                
                ui.Constraints = new Constraints
                {
                    Width = new Relative(1f),
                };
                
                ui.ClipConstraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Relative(1f),
                };
                
                float scroll = _inputService.GetMouseScroll();
                _scrollY = Math.Clamp(_scrollY + (int)scroll, -(saves.Length - 1), 0);
                ui.ScrollY = _scrollY * 30;

                for (var i = 0; i < saves.Length; i++)
                {
                    GameSave save = saves[i];
                    if (ui.TextButton(id: $"Button_ContinueGame_{i}", text: save.Name, _saveFontOptions, _audioService, _volumeSettings))
                    {
                        _gameSaveManager.ActiveSave = save;
                        Task.Run(_gameSaveManager.Load);
                    }
                }
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

            if (ui.TextButton(id: "Button_Back", text: "Back", _buttonFontOptions, _audioService, _volumeSettings))
            {
                menu.GoBack();
            }
        }
        
        return Result.FromSuccess();
    }
}