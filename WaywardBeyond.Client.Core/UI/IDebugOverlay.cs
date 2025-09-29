using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI;

public interface IDebugOverlay
{
    Result RenderDebugOverlay(double delta, UIBuilder<Material> ui);
}