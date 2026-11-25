using Swordfish.Graphics;

namespace Swordfish.IO;

/// <summary>Defines a material asset loaded from TOML.</summary>
public struct MaterialDefinition()
{
    /// <summary>The shader asset identifier used by the material.</summary>
    public string Shader = string.Empty;

    /// <summary>The ordered texture asset paths bound to the material.</summary>
    public string[] Textures = [];

    /// <summary>The alpha transparency mode used during rasterization.</summary>
    public AlphaMode AlphaMode = AlphaMode.Opaque;

    /// <summary>The alpha discard threshold for masked materials.</summary>
    public float AlphaCutoff = 0.5f;
}