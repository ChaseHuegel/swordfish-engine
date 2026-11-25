using Microsoft.Extensions.Logging;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Systems;

namespace WaywardBeyond.Client.UI.Layers.Menus.Pause;

internal sealed class PauseMenu : TitleMenu<PausePage>
{
    private readonly InteractionState _interactionState;
    private readonly PauseScrimView _scrim = new();

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

        GameShortcuts.RegisterOrAddHandler(
            shortcutService,
            GameShortcuts.CreateBack(),
            () => WaywardBeyond.GameState >= GameState.Playing,
            OnPauseToggled
        );
        GameShortcuts.RegisterOrAddHandler(
            shortcutService,
            GameShortcuts.CreateBack(),
            IsVisible,
            () => GoBack()
        );
    }

    public override bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.Paused;
    }

    public override Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        _scrim.Render(ui);

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
