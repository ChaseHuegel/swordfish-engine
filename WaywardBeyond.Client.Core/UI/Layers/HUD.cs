using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI.Layers;

internal class HUD(in Hotbar hotbar, in Actions actions) : IUILayer
{
    private readonly Hotbar _hotbar = hotbar;
    private readonly Actions _actions = actions;

    public bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.Playing;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.Spacing = 20;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                Y = new Fixed(-30),
            };
            
            if (_actions.IsVisible())
            {
                //  TODO handle non-success results
                _actions.RenderUI(delta, ui);
            }
            
            if (_hotbar.IsVisible())
            {
                //  TODO handle non-success results
                _hotbar.RenderUI(delta, ui);
            }
        }

        return Result.FromSuccess();
    }
}