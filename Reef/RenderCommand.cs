using System.Numerics;
using Reef.UI;

namespace Reef;

public readonly struct RenderCommand<TRendererData>(
    IntRect rect,
    IntRect clipRect,
    Vector4 color,
    Vector4 backgroundColor,
    CornerRadius cornerRadius,
    FontOptions fontOptions,
    string? text,
    TRendererData? rendererData)
{
    public readonly IntRect Rect = rect;
    public readonly IntRect ClipRect = clipRect;
    public readonly Vector4 Color = color;
    public readonly Vector4 BackgroundColor = backgroundColor;
    public readonly CornerRadius CornerRadius = cornerRadius;
    public readonly FontOptions FontOptions = fontOptions;
    public readonly string? Text = text;
    public readonly TRendererData? RendererData = rendererData;
}