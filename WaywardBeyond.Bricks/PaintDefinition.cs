namespace WaywardBeyond.Bricks;

/// <summary>Defines a paint asset (either a texture paint or a color swatch).</summary>
public struct PaintDefinition()
{
    /// <summary>The unique ID of the paint.</summary>
    public string ID = string.Empty;

    /// <summary>The localization key of the paint display name.</summary>
    public string? Name;

    /// <summary>The alpha transparency mode of the paint.</summary>
    public BrickAlpha Alpha = BrickAlpha.Opaque;

    /// <summary>Whether the paint uses connected textures across adjacent faces.</summary>
    public bool Connected;

    /// <summary>The texture variant layer names for a texture paint.</summary>
    public string?[]? Textures;

    /// <summary>The hex color value for a color swatch paint.</summary>
    public string? Color;
}
