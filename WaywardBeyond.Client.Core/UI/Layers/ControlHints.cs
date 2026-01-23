using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI.Layers;

internal class ControlHints(
    in OrientationSelector orientationSelector,
    in ShapeSelector shapeSelector,
    in ILocalization localization
) : IUILayer
{
    private readonly OrientationSelector _orientationSelector = orientationSelector;
    private readonly ShapeSelector _shapeSelector = shapeSelector;
    private readonly ILocalization _localization = localization;

    public bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.Playing;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
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
                using (ui.Text(_localization.GetString("ui.hint.changeShape")!)) {}
            }

            if (_orientationSelector.Available)
            {
                using (ui.Text(_localization.GetString("ui.hint.changeOrientation")!)) {}
            }
            
            using (ui.Text(_localization.GetString("ui.hint.snapBrick")!)) {}
            using (ui.Text(_localization.GetString("ui.hint.pickBrick")!)) {}
            using (ui.Text(_localization.GetString("ui.hint.breakBrick")!)) {}
            using (ui.Text(_localization.GetString("ui.hint.placeBrick")!)) {}
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Fixed(8),
                };
            }
            
            using (ui.Text(_localization.GetString("ui.hint.movement.roll")!)) {}
            using (ui.Text(_localization.GetString("ui.hint.movement.up")!)) {}
            using (ui.Text(_localization.GetString("ui.hint.movement.down")!)) {}
            using (ui.Text(_localization.GetString("ui.hint.movement")!)) {}
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Fixed(8),
                };
            }
            
            using (ui.Text(_localization.GetString("ui.hint.quit")!)) {}
            using (ui.Text(_localization.GetString("ui.hint.quicksave")!)) {}
            using (ui.Text(_localization.GetString("ui.hint.inventory")!)) {}
        }
        
        return Result.FromSuccess();
    }
}