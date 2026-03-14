using System;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI;

internal class DefaultUIRenderer : IAutoActivate
{
    private readonly ILogger _logger;
    private readonly ReefContext _reefContext;
    private readonly UISettings _uiSettings;
    private readonly IUILayer[] _layers;
    
    public DefaultUIRenderer(
        ILogger<DefaultUIRenderer> logger,
        ReefContext reefContext,
        UISettings uiSettings,
        IShortcutService shortcutService,
        IWindowContext windowContext,
        IUILayer[] layers
    ) {
        _logger = logger;
        _reefContext = reefContext;
        _uiSettings = uiSettings;
        _layers = layers;
        
        var toggleShortcut = new Shortcut
        {
            Name = "Toggle UI",
            Category = "UI",
            Key = Key.F1,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = OnToggleUI,
        };
        shortcutService.RegisterShortcut(toggleShortcut);
        
        windowContext.Update += OnWindowUpdate;
    }
    
    private void OnToggleUI()
    {
        _uiSettings.Visible.Set(!_uiSettings.Visible);
    }
    
    private void OnWindowUpdate(double delta)
    {
        if (!_uiSettings.Visible)
        {
            return;
        }
        
        for (var i = 0; i < _layers.Length; i++)
        {
            IUILayer layer = _layers[i];
            if (!layer.IsVisible())
            {
                continue;
            }
            
            try
            {
                Result result = layer.RenderUI(delta, _reefContext.Builder);
                if (!result)
                {
                    _logger.LogError(result, "Failed to render UI layer \"{layer}\".", layer.GetType());
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Caught an exception when rendering UI layer \"{layer}\".", layer.GetType());
            }
        }
    }
}