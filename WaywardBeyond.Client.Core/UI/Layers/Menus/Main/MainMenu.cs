using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class MainMenu : TitleMenu<MenuPage>
{
    private readonly Material? _backgroundMaterial;
    
    public MainMenu(
        ILogger<Menu<MenuPage>> logger,
        IAssetDatabase<Material> materialDatabase,
        ReefContext reefContext,
        IShortcutService shortcutService,
        IMenuPage<MenuPage>[] pages
    ) : base(logger, materialDatabase, reefContext, shortcutService, pages)
    {
        Result<Material> materialResult = materialDatabase.Get("ui/menu/background");
        if (materialResult)
        {
            _backgroundMaterial = materialResult;
        }
        else
        {
            logger.LogError(materialResult, "Failed to load the background material, it will not be able to render.");
        }

        WaywardBeyond.GameState.Changed += OnGameStateChanged;
    }

    public override bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.MainMenu;
    }

    public override Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (_backgroundMaterial == null)
        {
            return base.RenderUI(delta, ui);
        }

        //  Render the background
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

        return base.RenderUI(delta, ui);
    }
    
    private void OnGameStateChanged(object? sender, DataChangedEventArgs<GameState> e)
    {
        if (e.NewValue != GameState.MainMenu)
        {
            return;
        }
        
        GoToPage(MenuPage.Home);
    }
}