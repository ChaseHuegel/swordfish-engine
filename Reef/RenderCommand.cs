using System.Numerics;
using Reef.UI;

namespace Reef;

public readonly struct RenderCommand<TTextureData>(
    IntRect rect,
    Vector4 color,
    CornerRadius cornerRadius,
    FontOptions fontOptions,
    string? text,
    TTextureData? textureData)
{
    public readonly IntRect Rect = rect;
    public readonly Vector4 Color = color;
    public readonly CornerRadius CornerRadius = cornerRadius;
    public readonly FontOptions FontOptions = fontOptions;
    public readonly string? Text = text;
    public readonly TTextureData? TextureData = textureData;
}