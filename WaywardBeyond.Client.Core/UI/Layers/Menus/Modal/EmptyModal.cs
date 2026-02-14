using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Modal;

internal class EmptyModal : IMenuPage<Modal>
{
    public Modal ID { get; } = new(id: "empty");
    
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        return Result.FromSuccess();
    }
}