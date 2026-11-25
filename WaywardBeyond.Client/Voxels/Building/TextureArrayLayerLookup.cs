using Swordfish.Graphics;
using WaywardBeyond.Meshing;

namespace WaywardBeyond.Client.Voxels.Building;

/// <summary>Adapts a <see cref="TextureArray"/> to <see cref="ITextureLayerLookup"/> for shared voxel meshing.</summary>
internal sealed class TextureArrayLayerLookup(TextureArray albedo) : ITextureLayerLookup
{
    private readonly TextureArray _albedo = albedo;

    /// <inheritdoc/>
    public int IndexOf(string name)
    {
        return _albedo.IndexOf(name);
    }
}
