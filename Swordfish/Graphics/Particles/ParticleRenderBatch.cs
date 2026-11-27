namespace Swordfish.Graphics.Particles;

/// <summary>Contiguous slice of <see cref="ParticleInstance"/> records sharing one texture, blend mode, and spatial partition.</summary>
internal readonly record struct ParticleRenderBatch(
    Texture? Texture,
    ParticleBlendMode BlendMode,
    int Offset,
    int Count,
    BoundingBox Bounds
);
