namespace WaywardBeyond.Bricks;

/// <summary>TOML schema for a brick entity's point light declared under <c>[Bricks.Entity.Light]</c>.</summary>
public struct BrickLightDefinition()
{
    /// <summary>Optional fixed light radius; omitted derives radius from voxel light level.</summary>
    public float? Radius;

    /// <summary>Emitter size in world units.</summary>
    public float Size = 2.4f;

    /// <summary>Optional hex sRGB tint color (defaults to white).</summary>
    public string? Color;

    /// <summary>Linear intensity multiplier applied to the light color.</summary>
    public float Intensity = 1.0f;

    /// <summary>Optional shadow-casting override; omitted casts shadows when voxel light level is at least 14.</summary>
    public bool? CastShadows;

    /// <summary>Local offset from the brick center, rotated by the brick's orientation.</summary>
    public BrickColliderDefinition.Float3 Offset;

    /// <summary>Whether painted brick faces tint the emitted light color.</summary>
    public bool TintFromPaint;
}
