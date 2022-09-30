using System.Drawing;
using System.Numerics;
using ImGuiNET;
using Swordfish.Library.Types.Constraints;
using Swordfish.Util;

namespace Swordfish.UI.Elements;

public class DividerElement : Element
{
    protected override void OnRender()
    {
        ImGui.Separator();
    }
}
