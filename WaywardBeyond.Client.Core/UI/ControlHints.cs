using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core.UI;

internal class ControlHints(in OrientationSelector orientationSelector, in ShapeSelector shapeSelector) : IUILayer
{
    private readonly OrientationSelector _orientationSelector = orientationSelector;
    private readonly ShapeSelector _shapeSelector = shapeSelector;
    
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (WaywardBeyond.GameState != GameState.Playing)
        {
            return Result.FromSuccess();
        }
        
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
        
        return Result.FromSuccess();
    }
}