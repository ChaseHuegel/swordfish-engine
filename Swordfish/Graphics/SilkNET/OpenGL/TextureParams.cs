using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal struct TextureParams(
    in TextureWrapMode wrapS,
    in TextureWrapMode wrapT,
    in TextureMinFilter minFilter,
    in TextureMagFilter magFilter,
    in bool generateMipmaps = false,
    in bool compareMode = false
) {
    public TextureWrapMode WrapS = wrapS;
    public TextureWrapMode WrapT = wrapT;
    public TextureMinFilter MinFilter = minFilter;
    public TextureMagFilter MagFilter = magFilter;
    public bool GenerateMipmaps = generateMipmaps;
    public bool CompareMode = compareMode;

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

    public static readonly TextureParams ShadowMap = new(
        TextureWrapMode.ClampToEdge,
        TextureWrapMode.ClampToEdge,
        TextureMinFilter.Linear,
        TextureMagFilter.Linear,
        generateMipmaps: false,
        compareMode: true
    );

    public static readonly TextureParams ShadowMapNearest = new(
        TextureWrapMode.ClampToEdge,
        TextureWrapMode.ClampToEdge,
        TextureMinFilter.Nearest,
        TextureMagFilter.Nearest,
        generateMipmaps: false,
        compareMode: true
    );
}
