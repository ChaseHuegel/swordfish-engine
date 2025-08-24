using System.Collections.Generic;

namespace Reef.UI;

public struct Element<TTextureData>
{
    public IntRect Rect;
    public Style Style;
    public Layout Layout;
    public Constraints Constraints;
    public List<Element<TTextureData>>? Children;
    public string? Text;
    public FontOptions FontOptions;
    public TTextureData? TextureData;
    public string? ButtonID;
    public Viewport Viewport;
}