using System;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.UI;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core.UI;

internal class DebugOverlay : IAutoActivate
{
    public event Action<UIBuilder<Material>>? Render; 
    
    private readonly ILogger _logger;
    private readonly ReefContext _reefContext;

    private bool _visible;
    
    public DebugOverlay(ILogger<DebugOverlay> logger, ReefContext reefContext, IWindowContext windowContext, IShortcutService shortcutService)
    {
        _logger = logger;
        _reefContext = reefContext;

        var shortcut = new Shortcut
        {
            Name = "Toggle debug overlay",
            Category = "Developer",
            Key = Key.F3,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = OnToggleDebugOverlay,
        };
        shortcutService.RegisterShortcut(shortcut);
        
        windowContext.Update += OnWindowUpdate;
    }

    private void OnToggleDebugOverlay()
    {
        _visible = !_visible;
    }

    private void OnWindowUpdate(double delta)
    {
        if (!_visible)
        {
            return;
        }
        
        UIBuilder<Material> ui = _reefContext.Builder;

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

            try
            {
                Render?.Invoke(ui);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Caught an exception when rendering the debug overlay.");
            }
        }
    }
}