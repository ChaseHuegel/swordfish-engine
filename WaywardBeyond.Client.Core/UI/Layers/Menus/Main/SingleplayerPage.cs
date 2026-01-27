using System;
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

internal sealed class SingleplayerPage(
    in GameSaveManager gameSaveManager,
    in GameSaveService gameSaveService,
    in IInputService inputService,
    in IAudioService audioService,
    in VolumeSettings volumeSettings,
    in ILocalization localization
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Singleplayer;
    
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly GameSaveService _gameSaveService = gameSaveService;
    private readonly IInputService _inputService = inputService;
    private readonly IAudioService _audioService = audioService;
    private readonly VolumeSettings _volumeSettings = volumeSettings;
    private readonly ILocalization _localization = localization;
    
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };

    private readonly FontOptions _saveFontOptions = new()
    {
        Size = 20,
    };

    private int _scrollY;
    private string _saveName = localization.GetString("ui.field.defaultSaveName") ?? string.Empty;
    
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        GameSave[] saves = _gameSaveService.GetSaves();
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Top,
                X = new Relative(0.5f),
                Y = new Relative(0.4f),
            };
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    X = new Relative(0.5f),
                };

                using (ui.Text(_localization.GetString("ui.menu.saves")!))
                {
                    ui.FontSize = 24;
                }
            }
            
            ui.TextBox(id: "TextBox_SaveName", text: ref _saveName, _saveFontOptions, _inputService, _audioService, _volumeSettings);
            
            if (ui.TextButton(id: "Button_NewGame", text: _localization.GetString("ui.button.newGame")!, _saveFontOptions, _audioService, _volumeSettings))
            {
                var options = new GameOptions(_saveName, seed: "wayward beyond");
                Task.Run(() => _gameSaveManager.NewGame(options));
            }
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Height = new Fixed(20),
                };
            }

            using (ui.Element())
            {
                ui.VerticalScroll = true;
                ui.LayoutDirection = LayoutDirection.Vertical;
                
                ui.Constraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Fixed(150),
                };
                
                ui.ClipConstraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Relative(1f),
                };
                
                float scroll = _inputService.GetMouseScroll();
                _scrollY = Math.Clamp(_scrollY + (int)scroll, -saves.Length, 0);
                ui.ScrollY = _scrollY * 22;

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