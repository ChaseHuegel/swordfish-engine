namespace WaywardBeyond.Bricks;

/// <summary>Defines a paint asset (either a texture paint or a color swatch).</summary>
internal struct PaintDefinition()
{
    public string ID = string.Empty;
    public string? Name;
    public BrickAlpha Alpha = BrickAlpha.Opaque;
    public bool Connected;
    public string?[]? Textures;
    public string? Color;
}
