using Swordfish.Graphics;

namespace Swordfish.IO;

internal struct MaterialDefinition()
{
    public string Shader = string.Empty;
    public string[] Textures = [];
    public AlphaMode AlphaMode = AlphaMode.Opaque;
    public float AlphaCutoff = 0.5f;
}