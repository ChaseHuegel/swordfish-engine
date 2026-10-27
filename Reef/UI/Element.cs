using System.Collections.Generic;
using Reef.Text;

namespace Reef.UI;

/// <summary>
///     Represents a single UI element in the immediate-mode element tree.
/// </summary>
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
    public bool Overlay;

    internal IntVector2 ContentSize;
    internal TextLayout? TextLayout;
}
