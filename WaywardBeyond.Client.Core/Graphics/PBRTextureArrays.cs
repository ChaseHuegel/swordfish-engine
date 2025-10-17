using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Graphics;

internal sealed class PBRTextureArrays(
    in TextureArray diffuse,
    in TextureArray metallic,
    in TextureArray roughness,
    in TextureArray normal,
    in TextureArray emissive
) {
    public readonly TextureArray Diffuse = diffuse;
    public readonly TextureArray Metallic = metallic;
    public readonly TextureArray Roughness = roughness;
    public readonly TextureArray Normal = normal;
    public readonly TextureArray Emissive = emissive;

    public Texture[] ToArray()
    {
        return [Diffuse, Metallic, Roughness, Normal, Emissive];
    }
}