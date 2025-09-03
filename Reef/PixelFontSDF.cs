namespace Reef;

public readonly struct PixelFontSDF(PixelTexture texture, float fwidth)
{
    public readonly PixelTexture Texture = texture;
    public readonly float Fwidth = fwidth;
}