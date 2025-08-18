using System.Numerics;

namespace Reef;

public struct RenderCommand<TTextureData>
{
    public IntRect Rect;
    public Vector4 Color;
    public CornerRadius CornerRadius;
    public string? Text;
    public TTextureData? TextureData;
}