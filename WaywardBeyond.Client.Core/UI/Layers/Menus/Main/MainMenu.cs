using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class MainMenu : Menu<MenuPage>
{
    private readonly Material? _titleMaterial;
    private readonly Material? _backgroundMaterial;
    
    public MainMenu(
        ILogger<Menu<MenuPage>> logger,
        IAssetDatabase<Material> materialDatabase,
        ReefContext reefContext,
        IShortcutService shortcutService,
        IMenuPage<MenuPage>[] pages
    ) : base(logger, reefContext, pages)
    {
        Result<Material> materialResult = materialDatabase.Get("ui/menu/title");
        if (materialResult)
        {
            _titleMaterial = materialResult;
        }
        else
        {
            logger.LogError(materialResult, "Failed to load the title material, it will not be able to render.");
        }
        
        materialResult = materialDatabase.Get("ui/menu/background");
        if (materialResult)
        {
            _backgroundMaterial = materialResult;
        }
        else
        {
            logger.LogError(materialResult, "Failed to load the background material, it will not be able to render.");
        }
        
        Shortcut backShortcut = new(
            "Go back",
            "General",
            ShortcutModifiers.None,
            Key.Esc,
            IsVisible,
            () => GoBack()
        );
        shortcutService.RegisterShortcut(backShortcut);
    }
    
    public override bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.MainMenu;
    }

    public override Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (_backgroundMaterial != null)
        {
            using (ui.Image(_backgroundMaterial))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    X = new Relative(0.5f),
                    Y = new Relative(0.5f),
                    Width = new Fixed(_backgroundMaterial.Textures[0].Width),
                    Height = new Fixed(_backgroundMaterial.Textures[0].Height),
                };
            }
        }
        
        if (_titleMaterial != null)
        {
            using (ui.Image(_titleMaterial))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center | Anchors.Top,
                    X = new Relative(0.5f),
                    Y = new Relative(0.1f),
                    Width = new Fixed(_titleMaterial.Textures[0].Width),
                    Height = new Fixed(_titleMaterial.Textures[0].Height),
                };
            }
        }
        
        return base.RenderUI(delta, ui);
    }
}