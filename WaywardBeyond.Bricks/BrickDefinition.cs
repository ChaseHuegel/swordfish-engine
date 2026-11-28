using System.Collections.Generic;

namespace WaywardBeyond.Bricks;

/// <summary>Defines a brick asset.</summary>
public struct BrickDefinition()
{
    /// <summary>The unique ID of the brick.</summary>
    public string ID = string.Empty;

    /// <summary>The alpha transparency mode of the brick.</summary>
    public BrickAlpha Alpha = BrickAlpha.Opaque;

    /// <summary>Whether the brick casts shadows.</summary>
    public bool CastShadows = true;

    /// <summary>Whether entities can pass through the brick without colliding.</summary>
    public bool Passable;

    /// <summary>The optional custom mesh path for the brick.</summary>
    public string? Mesh;

    /// <summary>The geometry shape kind of the brick.</summary>
    public BrickShape Shape;

    /// <summary>The per-face texture assignments for the brick.</summary>
    public BrickTextures Textures;

    /// <summary>The optional hex tint color applied to the brick.</summary>
    public string? Color;

    /// <summary>The sound asset paths keyed by brick gameplay event.</summary>
    public Dictionary<BrickEventKind, string[]>? Sounds;

    /// <summary>The optional custom collider primitives declared for the brick.</summary>
    public List<BrickColliderDefinition>? Colliders;

    /// <summary>Optional custom numeric statistics declared on the brick, keyed by statistic id.</summary>
    public Dictionary<string, float>? Statistics;

    /// <summary>Optional brick-entity metadata declared under <c>[Bricks.Entity]</c>.</summary>
    public BrickEntityDefinition? Entity;
}
