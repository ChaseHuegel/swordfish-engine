using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal struct TextureParams(
    in TextureWrapMode wrapS,
    in TextureWrapMode wrapT,
    in TextureMinFilter minFilter,
    in TextureMagFilter magFilter,
    in bool generateMipmaps = false
) {
    public TextureWrapMode WrapS = wrapS;
    public TextureWrapMode WrapT = wrapT;
    public TextureMinFilter MinFilter = minFilter;
    public TextureMagFilter MagFilter = magFilter;
    public bool GenerateMipmaps = generateMipmaps;

    public static readonly TextureParams ClampNearest = new(
        TextureWrapMode.ClampToEdge,
        TextureWrapMode.ClampToEdge,
        TextureMinFilter.Nearest,
        TextureMagFilter.Nearest
    );
    
    public static readonly TextureParams ClampLinear = new(
        TextureWrapMode.ClampToEdge,
        TextureWrapMode.ClampToEdge,
        TextureMinFilter.Linear,
        TextureMagFilter.Linear
    );
}