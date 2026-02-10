using System.Numerics;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;
using WaywardBeyond.Client.Core.Systems;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Pause;

internal sealed class PauseMenu : TitleMenu<PausePage>
{
    private readonly PlayerInteractionService _playerInteractionService;
    
    public PauseMenu(
        in ILogger<Menu<PausePage>> logger,
        in IAssetDatabase<Material> materialDatabase,
        in ReefContext reefContext,
        in IShortcutService shortcutService,
        in PlayerInteractionService playerInteractionService,
        in IMenuPage<PausePage>[] pages
    ) : base(logger, materialDatabase, reefContext, pages)
    {
        _playerInteractionService = playerInteractionService;
        
        Shortcut pauseShortcut = new(
            name: "Toggle paused",
            category: "General",
            ShortcutModifiers.None,
            Key.Esc,
            isEnabled: () => WaywardBeyond.GameState >= GameState.Playing,
            action: OnPauseToggled
        );
        shortcutService.RegisterShortcut(pauseShortcut);
        
        Shortcut backShortcut = new(
            name: "Go back",
            category: "Pause",
            ShortcutModifiers.None,
            Key.Esc,
            isEnabled: IsVisible,
            action: () => GoBack()
        );
        shortcutService.RegisterShortcut(backShortcut);
    }

    public override bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.Paused;
    }

    public override Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        //  Tint the screen
        using (ui.Element())
        {
            ui.Color = new Vector4(0f, 0f, 0f, 0.5f);
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };
        }
        
        return base.RenderUI(delta, ui);
    }
    
    private void OnPauseToggled()
    {
        switch (WaywardBeyond.GameState.Get())
        {
            //  TODO #356 Interaction blockers aren't sufficient, some UI layers should be blocked and others should not
            //       Need to introduce a concept of "windows" which are closeable
            //       This `when` condition is here to not pause when pressing ESC to close the inventory,
            //       however pausing is also blocked by Shape and Orientation selectors but really shouldn't be.
            case GameState.Playing when IsVisible() || !_playerInteractionService.IsInteractionBlocked():
                WaywardBeyond.Pause();
                return;
            case GameState.Paused when GetCurrentPage() == PausePage.Home:
                WaywardBeyond.Unpause();
                return;
        }
    }
}