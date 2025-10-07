using System;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core.UI;

internal class DefaultUIRenderer : IAutoActivate
{
    private readonly ILogger _logger;
    private readonly ReefContext _reefContext;
    private readonly IUILayer[] _layers;
    
    public DefaultUIRenderer(ILogger<DefaultUIRenderer> logger, ReefContext reefContext, IWindowContext windowContext, IUILayer[] layers)
    {
        _logger = logger;
        _reefContext = reefContext;
        _layers = layers;
        windowContext.Update += OnWindowUpdate;
    }
    
    private void OnWindowUpdate(double delta)
    {
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