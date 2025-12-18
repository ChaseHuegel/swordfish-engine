using System.Numerics;
using System.Text;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Integrations;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.UI.Layers;

internal sealed class LoadScreen(in GameSaveService gameSaveService) : IUILayer
{
    private readonly GameSaveService _gameSaveService = gameSaveService;
    
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
            ui.Color = new Vector4(0f, 0f, 0f, 1f);
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Spacing = 20;

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Height = new Fill(),
                };
            }

            var statusBuilder = new StringBuilder();
            int steps = MathS.WrapInt((int)(_currentTime * 3d), 0, 3);
            for (var i = 0; i < 3; i++)
            {
                statusBuilder.Append(i == steps ? FontAwesome.CIRCLE_DOT : FontAwesome.CIRCLE);
                if (i != 2)
                {
                    statusBuilder.Append(' ');
                }
            }

            using (ui.Text(statusBuilder.ToString()))
            {
                ui.FontSize = 24;
                ui.FontID = "Font Awesome 6 Free Regular";
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center | Anchors.Bottom,
                    X = new Relative(0.5f),
                };
            }
            
            using (ui.Text(_gameSaveService.GetStatus()))
            {
                ui.FontSize = 16;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center | Anchors.Bottom,
                    X = new Relative(0.5f),
                };
            }
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Height = new Fill(),
                };
            }
        }
        
        return Result.FromSuccess();
    }
}