using System.Numerics;
using Reef.Text;
using Reef.UI;

namespace Reef;

/// <summary>
///     Describes a positioned and clipped UI primitive to be drawn by a renderer.
/// </summary>
public readonly struct RenderCommand<TRendererData>(
    string? id,
    IntRect rect,
    IntRect clipRect,
    Vector4 color,
    Vector4 backgroundColor,
    CornerRadius cornerRadius,
    BorderStyle border,
    CornerCapStyle cornerCaps,
    FontOptions fontOptions,
    string? text,
    bool passthrough,
    TRendererData? rendererData,
    TextLayout? textLayout = null,
    IntVector2 contentSize = default,
    bool scrollable = false
)
{
    public readonly string? ID = id;
    public readonly IntRect Rect = rect;
    public readonly IntRect ClipRect = clipRect;
    public readonly Vector4 Color = color;
    public readonly Vector4 BackgroundColor = backgroundColor;
    public readonly CornerRadius CornerRadius = cornerRadius;
    public readonly BorderStyle Border = border;
    public readonly CornerCapStyle CornerCaps = cornerCaps;
    public readonly FontOptions FontOptions = fontOptions;
    public readonly string? Text = text;
    public readonly TRendererData? RendererData = rendererData;
    public readonly TextLayout? TextLayout = textLayout;
    
    internal readonly bool Passthrough = passthrough;
    internal readonly IntVector2 ContentSize = contentSize;
    internal readonly bool Scrollable = scrollable;
}
