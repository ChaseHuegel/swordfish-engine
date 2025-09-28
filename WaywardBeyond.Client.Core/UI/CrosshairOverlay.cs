using Reef;
using Reef.Constraints;
using Reef.UI;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core.UI;

internal class CrosshairOverlay : IAutoActivate
{
    private readonly ReefContext _reefContext;
    private readonly Material _crosshairMaterial;
    
    public CrosshairOverlay(ReefContext reefContext, IWindowContext windowContext, IAssetDatabase<Material> materialDatabase)
    {
        _reefContext = reefContext;
        _crosshairMaterial = materialDatabase.Get("ui/crosshair");
        
        windowContext.Update += OnWindowUpdate;
    }

    private void OnWindowUpdate(double delta)
    {
        UIBuilder<Material> ui = _reefContext.Builder;

        using (ui.Image(_crosshairMaterial))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
                Y = new Relative(0.5f),
                Width = new Fixed(72),
                Height = new Fixed(72),
            };
        }
    }
}