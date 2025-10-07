using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI;

public interface IUILayer
{
    bool IsVisible();
    
    Result RenderUI(double delta, UIBuilder<Material> ui);
}