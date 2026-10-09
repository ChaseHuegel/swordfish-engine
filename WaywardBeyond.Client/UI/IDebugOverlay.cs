using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.UI;

public interface IDebugOverlay
{
    bool IsVisible();
    
    Result RenderDebugOverlay(double delta, UIBuilder<Material> ui);
}