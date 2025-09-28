using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core.UI;

internal class ControlHints : IAutoActivate
{
    private readonly ReefContext _reefContext;
    private readonly OrientationSelector _orientationSelector;
    private readonly ShapeSelector _shapeSelector;
    
    public ControlHints(
        ReefContext reefContext,
        IWindowContext windowContext,
        OrientationSelector orientationSelector,
        ShapeSelector shapeSelector
    ) {
        _reefContext = reefContext;
        _orientationSelector = orientationSelector;
        _shapeSelector = shapeSelector;
        
        windowContext.Update += OnWindowUpdate;
    }

    private void OnWindowUpdate(double delta)
    {
        UIBuilder<Material> ui = _reefContext.Builder;

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Bottom | Anchors.Right,
                X = new Relative(0.99f),
                Y = new Fixed(ui.Height - 100),
            };

            if (_shapeSelector.Available)
            {
                using (ui.Text("R: Change shape")) {}
            }

            if (_orientationSelector.Available)
            {
                using (ui.Text("T: Change orientation")) {}
            }
            
            using (ui.Text("MMB: Select brick")) {}
            using (ui.Text("LMB: Break brick")) {}
            using (ui.Text("RMB: Place brick")) {}
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Fixed(8),
                };
            }
            
            using (ui.Text("Tab: Toggle mouselook")) {}

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Fixed(8),
                };
            }
            
            using (ui.Text("Q/E: Roll")) {}
            using (ui.Text("Space: Up")) {}
            using (ui.Text("Ctrl: Down")) {}
            using (ui.Text("W/A/S/D: Fly")) {}
        }
    }
}