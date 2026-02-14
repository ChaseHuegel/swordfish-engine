using Microsoft.Extensions.Logging;
using Swordfish.Library.IO;
using Swordfish.UI.Reef;
using WaywardBeyond.Client.Core.Systems;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Modal;

internal sealed class ModalMenu : Menu<Modal>
{
    private readonly PlayerInteractionService _playerInteractionService;

    private PlayerInteractionService.InteractionBlocker? _interactionBlocker;
    
    public ModalMenu(
        in ILogger<Menu<Modal>> logger,
        in ReefContext reefContext,
        in IShortcutService shortcutService,
        in PlayerInteractionService playerInteractionService,
        in IMenuPage<Modal>[] pages
    ) : base(logger, reefContext, pages)
    {
        _playerInteractionService = playerInteractionService;
        
        Shortcut closeShortcut = new(
            name: "Close modal",
            category: "Modals",
            ShortcutModifiers.None,
            Key.Esc,
            isEnabled: IsVisible,
            action: () => GoBack()
        );
        shortcutService.RegisterShortcut(closeShortcut);
    }

    public override bool IsVisible()
    {
        //  If there isn't a previous page, then there are active modals pushed.
        bool active = GetPreviousPage().Success;
        
        //  ! HACK
        //  ! Relying on IsVisible being called each frame to manage state
        if (WaywardBeyond.IsPlaying())
        {
            if (active && _interactionBlocker == null)
            {
                _interactionBlocker = _playerInteractionService.BlockInteraction();
            }
            else if (!active && _interactionBlocker != null)
            {
                _interactionBlocker.Dispose();
                _interactionBlocker = null;
            }
        }

        return active;
    }
}