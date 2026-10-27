using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Composes the primary in-game HUD overlays: top skill bars and bottom hotbar/actions.</summary>
internal sealed class HUD(in Hotbar hotbar, in Actions actions, in Bars bars) : IUILayer
{
    private readonly Hotbar _hotbar = hotbar;
    private readonly Actions _actions = actions;
    private readonly Bars _bars = bars;

    /// <inheritdoc/>
    public bool IsVisible() => WaywardBeyond.GameState == GameState.Playing;

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.Spacing = 8;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                Y = new Fixed(-20),
                Width = new Fixed(ui.Width),
            };

            if (_actions.IsVisible())
            {
                Result actionsResult = _actions.RenderUI(delta, ui);
                if (!actionsResult)
                {
                    return actionsResult;
                }
            }

            if (_hotbar.IsVisible())
            {
                Result hotbarResult = _hotbar.RenderUI(delta, ui);
                if (!hotbarResult)
                {
                    return hotbarResult;
                }
            }
        }

        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.Spacing = 12;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Top,
                Y = new Fixed(20),
                Width = new Fixed(ui.Width),
            };

            if (_bars.IsVisible())
            {
                Result barsResult = _bars.RenderUI(delta, ui);
                if (!barsResult)
                {
                    return barsResult;
                }
            }
        }

        return Result.FromSuccess();
    }
}
