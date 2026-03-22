using System.Threading.Tasks;
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
using WaywardBeyond.Client.Core.Services;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class MainMenu : TitleMenu<MenuPage>
{
    private readonly ExternalAppService _externalAppService;
    private readonly Material? _backgroundMaterial;
    private readonly Widgets.ButtonOptions _buttonOptions;

    public MainMenu(
        ILogger<Menu<MenuPage>> logger,
        IAssetDatabase<Material> materialDatabase,
        ReefContext reefContext,
        IShortcutService shortcutService,
        SoundEffectService soundEffectService,
        ExternalAppService externalAppService,
        IMenuPage<MenuPage>[] pages
    ) : base(logger, materialDatabase, reefContext, pages)
    {
        _externalAppService = externalAppService;
        
        Result<Material> materialResult = materialDatabase.Get("ui/menu/background");
        if (materialResult)
        {
            _backgroundMaterial = materialResult;
        }
        else
        {
            logger.LogError(materialResult, "Failed to load the background material, it will not be able to render.");
        }
        
        _buttonOptions = new Widgets.ButtonOptions(
            new FontOptions {
                ID = "Font Awesome 6 Free Brands",
                Size = 32,
            },
            new Widgets.AudioOptions(soundEffectService)
        );
        
        Shortcut backShortcut = new(
            name: "Go back",
            category: "General",
            ShortcutModifiers.None,
            Key.Esc,
            isEnabled: IsVisible,
            action: () => GoBack()
        );
        shortcutService.RegisterShortcut(backShortcut);

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
                Width = new Fixed(_backgroundMaterial.Textures[0].Width),
                Height = new Fixed(_backgroundMaterial.Textures[0].Height),
            };
        }

        using (ui.Element())
        {
            ui.Padding = new Padding(20);
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Bottom | Anchors.Right,
            };
            
            using (ui.TextButton(id: "Button_Discord", text: "\uf392", _buttonOptions, out Widgets.Interactions interactions))
            {
                ui.Padding = new Padding(4);
                
                if (interactions.Has(Widgets.Interactions.Click))
                {
                    Task.Run(_externalAppService.TryOpenDiscordAsync);
                }
            }
        
            using (ui.TextButton(id: "Button_Steam", text: "\uf1b6", _buttonOptions, out Widgets.Interactions interactions))
            {
                ui.Padding = new Padding(4);
                
                if (interactions.Has(Widgets.Interactions.Click))
                {
                    Task.Run(_externalAppService.TryOpenSteamAsync);
                }
            }
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