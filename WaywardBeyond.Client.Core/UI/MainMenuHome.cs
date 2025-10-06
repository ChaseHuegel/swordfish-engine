using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI;

internal sealed class MainMenuHome : IMenuPage<MainMenuPage>
{
    public MainMenuPage ID => MainMenuPage.Home;

    private readonly Entry _entry;
    private readonly Material? _titleMaterial;
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };
    
    public MainMenuHome(in ILogger<MainMenuHome> logger, in Entry entry, in IAssetDatabase<Material> materialDatabase)
    {
        _entry = entry;
        
        Result<Material> materialResult = materialDatabase.Get("ui/menu/title");
        if (!materialResult)
        {
            logger.LogError(materialResult, "Failed to load the title material, it will not be able to render.");
            return;
        }

        _titleMaterial = materialResult;
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MainMenuPage> menu)
    {
        if (_titleMaterial != null)
        {
            using (ui.Image(_titleMaterial))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center | Anchors.Top,
                    X = new Relative(0.5f),
                    Y = new Relative(0.1f),
                    Width = new Fixed((int)(_titleMaterial.Textures[0].Width * 0.5f)),
                    Height = new Fixed((int)(_titleMaterial.Textures[0].Height * 0.5f)),
                };
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
                Task.Run(_entry.StartGameAsync);
            }

            if (ui.TextButton(id: "Button_ContinueGame", text: "Continue game", _buttonFontOptions))
            {
                Task.Run(_entry.StartGameAsync);
            }

            if (ui.TextButton(id: "Button_Settings", text: "Settings", _buttonFontOptions))
            {
                menu.GoToPage(MainMenuPage.Settings);
            }
            
            if (ui.TextButton(id: "Button_Quit", text: "Quit", _buttonFontOptions))
            {
                _entry.Quit();
            }
        }
        
        return Result.FromSuccess();
    }
}