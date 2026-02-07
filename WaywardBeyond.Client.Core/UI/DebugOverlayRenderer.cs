using System;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI;

internal class DebugOverlayRenderer : IUILayer
{
    private readonly ILogger _logger;
    private readonly ReefContext _reefContext;
    private readonly DebugSettings _debugSettings;
    private readonly Swordfish.Settings.DebugSettings _engineDebugSettings;
    private readonly IDebugOverlay[] _overlays;
    
    public DebugOverlayRenderer(
        ILogger<DebugOverlayRenderer> logger,
        ReefContext reefContext,
        IShortcutService shortcutService,
        DebugSettings debugSettings,
        Swordfish.Settings.DebugSettings engineDebugSettings,
        IDebugOverlay[] overlays
    ) {
        _logger = logger;
        _reefContext = reefContext;
        _debugSettings = debugSettings;
        _engineDebugSettings = engineDebugSettings;
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

    public bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.Playing && _debugSettings.OverlayVisible;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Padding = new Padding
            {
                Left = 20,
                Top = 20,
                Right = 20,
                Bottom = 20,
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
    
    private void OnToggleDebugOverlay()
    {
        bool visible = _debugSettings.OverlayVisible;
        _debugSettings.OverlayVisible.Set(!visible);
        _engineDebugSettings.UI.Set(!visible);
    }
}