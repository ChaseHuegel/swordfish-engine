using Swordfish.Library.Util;

namespace Swordfish.Graphics;

/// <summary>Renders isolated meshes into offscreen textures.</summary>
public interface IOffscreenRenderer
{
    /// <summary>Renders one mesh into a new texture.</summary>
    Result<Texture> RenderToTexture(in OffscreenRenderRequest request);

    /// <summary>Renders a batch of meshes into new textures, reusing compatible render targets.</summary>
    Result<Texture>[] RenderBatchToTextures(ReadOnlySpan<OffscreenRenderRequest> requests);
}
