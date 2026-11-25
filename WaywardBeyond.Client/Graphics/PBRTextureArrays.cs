using Swordfish.Graphics;

namespace WaywardBeyond.Client.Graphics;

internal sealed class PBRTextureArrays(
    in TextureArray albedo,
    in TextureArray metallic,
    in TextureArray smoothness,
    in TextureArray normal,
    in TextureArray emissive,
    in TextureArray tintMask
) {
    public readonly TextureArray Albedo = albedo;
    public readonly TextureArray Metallic = metallic;
    public readonly TextureArray Smoothness = smoothness;
    public readonly TextureArray Normal = normal;
    public readonly TextureArray Emissive = emissive;
    public readonly TextureArray TintMask = tintMask;

    public Texture[] ToArray()
    {
        return [Albedo, Metallic, Smoothness, Normal, Emissive, TintMask];
    }
}