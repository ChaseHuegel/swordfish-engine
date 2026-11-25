using System;
using Swordfish.Graphics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Graphics;

namespace WaywardBeyond.Client.Voxels.Building;

/// <summary>
///     Owns the shared materials and render state used by voxel geometry and the placement preview.
/// </summary>
internal sealed class VoxelMaterials : IDisposable
{
    public readonly Material Opaque;
    public readonly Material Masked;
    public readonly Material Transparent;
    public readonly Material Ghost;
    public readonly RenderOptions OpaqueOptions;
    public readonly RenderOptions OpaqueNoShadowOptions;
    public readonly RenderOptions MaskedOptions;
    public readonly RenderOptions TransparentOptions;
    public readonly RenderOptions GhostOptions;

    private readonly Shader? _ownedGhostShader;

    /// <summary>Creates the shared material set from the client's voxel shader, optional ghost shader, and texture arrays.</summary>
    public VoxelMaterials(in Shader shader, in PBRTextureArrays textureArrays, Shader? ghostShader = null)
    {
        _ownedGhostShader = ghostShader;
        Opaque = new Material(shader, textureArrays.ToArray());
        Masked = new Material(shader, textureArrays.ToArray())
        {
            AlphaMode = AlphaMode.Masked,
        };
        Transparent = new Material(shader, textureArrays.ToArray())
        {
            AlphaMode = AlphaMode.Blend,
        };
        Ghost = new Material(ghostShader ?? shader, textureArrays.ToArray())
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

        OpaqueNoShadowOptions = OpaqueOptions with
        {
            CastShadows = false,
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

        GhostOptions = new RenderOptions
        {
            AlphaMode = AlphaMode.Blend,
            DoubleFaced = true,
            Wireframe = false,
            CastShadows = false,
            ReceiveShadows = false,
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

    /// <inheritdoc/>
    public void Dispose()
    {
        _ownedGhostShader?.Dispose();
    }
}
