using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.UI.Layers;

internal class CrosshairOverlay : IUILayer
{
    private readonly GameplayConfig _gameplayConfig;
    private readonly Material? _crosshairMaterial;
    
    public CrosshairOverlay(ILogger<CrosshairOverlay> logger, IAssetDatabase<Material> materialDatabase, GameplayConfig gameplayConfig)
    {
        _gameplayConfig = gameplayConfig;
        
        Result<Material> materialResult = materialDatabase.Get("ui/crosshair");
        if (!materialResult)
        {
            logger.LogError(materialResult, "Failed to load the crosshair material, it will not be able to render.");
            return;
        }
        
        _crosshairMaterial = materialResult;
    }
    
    public bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.Playing && _gameplayConfig.Crosshair.Get();
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (_crosshairMaterial == null)
        {
            return Result.FromSuccess();
        }
        
        using (ui.Image(_crosshairMaterial))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed((int)(ui.Width * 0.01f)),
                Height = new Fixed((int)(ui.Width * 0.01f)),
            };
        }
        
        return Result.FromSuccess();
    }
}