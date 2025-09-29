using System;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core.UI;

internal class DebugOverlayRenderer : IUILayer
{
    private readonly ILogger _logger;
    private readonly ReefContext _reefContext;
    private readonly IDebugOverlay[] _overlays;

    private bool _visible;
    
    public DebugOverlayRenderer(ILogger<DebugOverlayRenderer> logger, ReefContext reefContext, IShortcutService shortcutService, IDebugOverlay[] overlays)
    {
        _logger = logger;
        _reefContext = reefContext;
        _overlays = overlays;

        var shortcut = new Shortcut
        {
            Name = "Toggle debug overlay",
            Category = "Developer",
            Key = Key.F3,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = OnToggleDebugOverlay,
        };
        shortcutService.RegisterShortcut(shortcut);
    }

    private void OnToggleDebugOverlay()
    {
        _visible = !_visible;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (!_visible)
        {
            return Result.FromSuccess();
        }
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Padding = new Padding
            {
                Left = 20,
                Top = 20,
                Right = 20,
                Bottom = 20
            };
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };

            for (var i = 0; i < _overlays.Length; i++)
            {
                IDebugOverlay overlay = _overlays[i];
                try
                {
                    Result result = overlay.RenderDebugOverlay(delta, _reefContext.Builder);
                    if (!result)
                    {
                        _logger.LogError(result, "Failed to render debug overlay \"{overlay}\".", overlay.GetType());
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Caught an exception when rendering debug overlay \"{overlay}\".", overlay.GetType());
                }
            }
        }
        
        return Result.FromSuccess();
    }
}