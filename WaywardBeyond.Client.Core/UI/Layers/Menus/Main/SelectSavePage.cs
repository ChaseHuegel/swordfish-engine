using System;
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

internal sealed class SelectSavePage(
    in GameSaveManager gameSaveManager,
    in GameSaveService gameSaveService,
    in IInputService inputService,
    in IAudioService audioService,
    in VolumeSettings volumeSettings,
    in ILocalization localization
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectSave;
    
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly GameSaveService _gameSaveService = gameSaveService;
    private readonly IInputService _inputService = inputService;
    private readonly ILocalization _localization = localization;

    private readonly Widgets.ButtonOptions _menuButtonOptions = new(
        new FontOptions 
        {
            Size = 32,
        },
        new Widgets.AudioOptions(audioService, volumeSettings)
    );

    private readonly Widgets.ButtonOptions _buttonOptions = new(
        new FontOptions
        {
            Size = 20,
        },
        new Widgets.AudioOptions(audioService, volumeSettings)
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
                
                for (var i = 0; i < saves.Length; i++)
                {
                    GameSave save = saves[i];
                    using (ui.TextButton(id: $"Button_ContinueGame_{i}", text: save.Name, _buttonOptions, out Widgets.Interactions interactions))
                    {
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Center,
                        };

                        if (interactions.Has(Widgets.Interactions.Click))
                        {
                            _gameSaveManager.ActiveSave = save;
                            Task.Run(_gameSaveManager.Load);
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
            
            using (ui.TextButton(id: "Button_Back", text: "Back", _menuButtonOptions, out Widgets.Interactions interactions))
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