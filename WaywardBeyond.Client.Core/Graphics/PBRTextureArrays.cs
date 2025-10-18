using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Graphics;

internal sealed class PBRTextureArrays(
    in TextureArray diffuse,
    in TextureArray metallic,
    in TextureArray smoothness,
    in TextureArray normal,
    in TextureArray emissive
) {
    public readonly TextureArray Diffuse = diffuse;
    public readonly TextureArray Metallic = metallic;
    public readonly TextureArray Smoothness = smoothness;
    public readonly TextureArray Normal = normal;
    public readonly TextureArray Emissive = emissive;

    public Texture[] ToArray()
    {
        return [Diffuse, Metallic, Smoothness, Normal, Emissive];
    }
}