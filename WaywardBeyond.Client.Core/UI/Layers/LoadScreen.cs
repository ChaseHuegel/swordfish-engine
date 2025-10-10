using System.Numerics;
using System.Text;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI.Layers;

internal sealed class LoadScreen : IUILayer
{
    private double _currentTime;
    
    public bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.Loading;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        _currentTime += delta;
        
        using (ui.Element())
        {
            ui.Color = new Vector4(0.25f, 0.25f, 0.25f, 0.75f);
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };

            var statusBuilder = new StringBuilder("Loading");
            int steps = MathS.WrapInt((int)(_currentTime * 2d), 0, 3);
            for (var i = 0; i < steps; i++)
            {
                statusBuilder.Append('.');
            }

            using (ui.Text(statusBuilder.ToString()))
            {
                ui.FontSize = 30;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    X = new Relative(0.5f),
                    Y = new Relative(0.5f),
                };
            }
        }
        
        return Result.FromSuccess();
    }
}