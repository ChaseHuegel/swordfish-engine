using System.Drawing;
using System.Runtime.CompilerServices;

namespace Swordfish.UI.Elements;

public interface IColorProperty
{
    Color Color { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    Color GetCurrentColor()
    {
        return ColorBlockElement.TryGetColorBlock(out Color color) ? color : Color;
    }
}
