using System.Collections.Generic;

namespace WaywardBeyond.Bricks;

/// <summary>Defines a brick asset.</summary>
internal struct BrickDefinition()
{
    public string ID = string.Empty;
    public BrickAlpha Alpha = BrickAlpha.Opaque;
    public bool CastShadows = true;
    public bool Passable;
    public string? Mesh;
    public BrickShape Shape;
    public BrickTextures Textures;
    public Dictionary<BrickEventKind, string[]>? Sounds;
    public List<BrickColliderDefinition>? Colliders;
}