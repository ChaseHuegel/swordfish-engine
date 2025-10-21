using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Graphics;

internal sealed class PBRTextureArrays(
    in TextureArray albedo,
    in TextureArray metallic,
    in TextureArray smoothness,
    in TextureArray normal,
    in TextureArray emissive
) {
    public readonly TextureArray Albedo = albedo;
    public readonly TextureArray Metallic = metallic;
    public readonly TextureArray Smoothness = smoothness;
    public readonly TextureArray Normal = normal;
    public readonly TextureArray Emissive = emissive;

    public Texture[] ToArray()
    {
        return [Albedo, Metallic, Smoothness, Normal, Emissive];
    }
}