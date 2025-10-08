using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.UI.Layers.Menu;

internal sealed class HomePage(in Entry entry, in GameSaveManager gameSaveManager, in GameSaveService gameSaveService) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Home;

    private readonly Entry _entry = entry;
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly GameSaveService _gameSaveService = gameSaveService;
    
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                X = new Relative(0.5f),
                Y = new Relative(0.99f),
            };

            if (ui.TextButton(id: "Button_Quit", text: "Quit", _buttonFontOptions))
            {
                _entry.Quit();
            }
        }
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
                Y = new Relative(0.5f),
            };

            if (ui.TextButton(id: "Button_NewGame", text: "New game", _buttonFontOptions))
            {
                var options = new GameOptions
                {
                    Seed = "wayward beyond",
                };
                Task.Run(() => _gameSaveManager.NewGame(options));
            }

            if (ui.TextButton(id: "Button_ContinueGame", text: "Continue game", _buttonFontOptions))
            {
                GameSave[] saves = _gameSaveService.GetSaves();
                if (saves.Length > 0)
                {
                    _gameSaveManager.ActiveSave = saves[0];
                    Task.Run(_gameSaveManager.Load);
                }
            }

            if (ui.TextButton(id: "Button_Settings", text: "Settings", _buttonFontOptions))
            {
                menu.GoToPage(MenuPage.Settings);
            }
        }
        
        return Result.FromSuccess();
    }
}