using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.UI.Layers;

internal class VersionWatermark : IUILayer
{
    private readonly VersionWatermarkView _view = new();

    public bool IsVisible()
    {
        return true;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        _view.Render(ui, new VersionWatermarkState(WaywardBeyond.Version.Name));

        return Result.FromSuccess();
    }
}
