using System;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.UI.Layers.Menu;

internal sealed class SingleplayerPage(in GameSaveManager gameSaveManager, in GameSaveService gameSaveService, in IInputService inputService) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Singleplayer;

    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly GameSaveService _gameSaveService = gameSaveService;
    private readonly IInputService _inputService = inputService;
    
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };

    private readonly FontOptions _saveFontOptions = new()
    {
        Size = 20,
    };

    private int _scrollY;
    
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

                using (ui.Text("Game Saves"))
                {
                    ui.FontSize = 24;
                }
            }
            
            if (ui.TextButton(id: "Button_NewGame", text: "+ New game", _saveFontOptions))
            {
                var options = new GameOptions(name: $"playtest{saves.Length + 1}", seed: Guid.NewGuid().ToString());
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
                
                foreach (GameSave save in saves)
                {
                    if (ui.TextButton(id: $"Button_ContinueGame_{save.Name}", text: save.Name, _saveFontOptions))
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

            if (ui.TextButton(id: "Button_Back", text: "Back", _buttonFontOptions))
            {
                menu.GoBack();
            }
        }
        
        return Result.FromSuccess();
    }
}