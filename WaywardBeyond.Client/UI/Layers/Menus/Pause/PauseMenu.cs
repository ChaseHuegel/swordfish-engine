using Microsoft.Extensions.Logging;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Pause;

internal sealed class PauseMenu : TitleMenu<PausePage>
{
    private readonly InteractionState _interactionState;

    public PauseMenu(
        in ILogger<Menu<PausePage>> logger,
        in IAssetDatabase<Material> materialDatabase,
        in ReefContext reefContext,
        in IShortcutService shortcutService,
        in InteractionState interactionState,
        in IMenuPage<PausePage>[] pages
    ) : base(logger, materialDatabase, reefContext, pages)
    {
        _interactionState = interactionState;

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
            ui.Color = UiTheme.ScrimSoft;
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
            //  A single interaction blocker cannot tell a closable window from a selector, so the
            //  playing state is special-cased to avoid pausing while the inventory closes.
            case GameState.Playing when IsVisible() || !_interactionState.IsInteractionBlocked():
                WaywardBeyond.Pause();
                return;
            case GameState.Paused when GetCurrentPage() == PausePage.Home:
                WaywardBeyond.Unpause();
                return;
        }
    }
}
