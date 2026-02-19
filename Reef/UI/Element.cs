using System.Collections.Generic;

namespace Reef.UI;

public struct Element<TRendererData>
{
    public bool Debug;
    public IntRect Rect;
    public Style Style;
    public Layout Layout;
    public Constraints Constraints;
    public List<Element<TRendererData>>? Children;
    public string? Text;
    public FontOptions FontOptions;
    public TRendererData? TextureData;
    public string? ID;
    public Viewport Viewport;
    public bool Passthrough;
}