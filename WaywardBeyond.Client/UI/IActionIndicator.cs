using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.UI;

internal interface IActionIndicator
{
    bool IsVisible();
    
    Result RenderIndicator(double delta, UIBuilder<Material> ui);
}