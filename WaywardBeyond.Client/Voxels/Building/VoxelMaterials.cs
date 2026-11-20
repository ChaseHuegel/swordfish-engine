using Swordfish.Graphics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Graphics;

namespace WaywardBeyond.Client.Voxels.Building;

/// <summary>
///     Owns the shared materials and render state used by voxel geometry and the placement preview.
/// </summary>
internal sealed class VoxelMaterials
{
    public readonly Material Opaque;
    public readonly Material Masked;
    public readonly Material Transparent;
    public readonly RenderOptions OpaqueOptions;
    public readonly RenderOptions MaskedOptions;
    public readonly RenderOptions TransparentOptions;

    /// <summary>Creates the shared material set from the client's voxel shader and texture arrays.</summary>
    public VoxelMaterials(in Shader shader, in PBRTextureArrays textureArrays)
    {
        Opaque = new Material(shader, textureArrays.ToArray());
        Masked = new Material(shader, textureArrays.ToArray())
        {
            AlphaMode = AlphaMode.Masked,
        };
        Transparent = new Material(shader, textureArrays.ToArray())
        {
            AlphaMode = AlphaMode.Blend,
        };

        OpaqueOptions = new RenderOptions
        {
            AlphaMode = AlphaMode.Opaque,
            DoubleFaced = false,
            Wireframe = false,
            CastShadows = true,
            ReceiveShadows = true,
        };

        MaskedOptions = new RenderOptions
        {
            AlphaMode = AlphaMode.Masked,
            DoubleFaced = true,
            Wireframe = false,
            CastShadows = true,
            ReceiveShadows = true,
        };

        TransparentOptions = new RenderOptions
        {
            AlphaMode = AlphaMode.Blend,
            DoubleFaced = true,
            Wireframe = false,
            CastShadows = false,
            ReceiveShadows = true,
        };
    }

    /// <summary>Returns the material that renders the provided brick alpha mode.</summary>
    public Material GetMaterial(BrickAlpha alpha)
    {
        return alpha switch
        {
            BrickAlpha.Masked => Masked,
            BrickAlpha.Blend => Transparent,
            _ => Opaque,
        };
    }

    /// <summary>Returns the render state that renders the provided brick alpha mode.</summary>
    public RenderOptions GetRenderOptions(BrickAlpha alpha)
    {
        return alpha switch
        {
            BrickAlpha.Masked => MaskedOptions,
            BrickAlpha.Blend => TransparentOptions,
            _ => OpaqueOptions,
        };
    }
}
