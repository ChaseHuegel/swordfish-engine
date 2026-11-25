using System.Numerics;

namespace WaywardBeyond.Bricks;

/// <summary>An asset representing a paintable face texture or color swatch.</summary>
public sealed class Paint
{
    /// <summary>The minimum registered paint ID value. ID 0 means no paint override.</summary>
    public const ushort MIN_ID = 1;

    /// <inheritdoc cref="PaintDefinition.ID"/>
    public readonly string ID;

    /// <summary>The transient numeric ID for this paint. Bound once at database load.</summary>
    public ushort DataID { get; private set; }

    /// <summary>Whether this paint overrides face texture or face color tint.</summary>
    public readonly PaintKind Kind;

    /// <summary>Localization key or display name for this paint.</summary>
    public readonly string Name;

    /// <summary>Alpha bucket for texture paints (matches <see cref="Brick.Alpha"/>).</summary>
    public readonly BrickAlpha Alpha;

    /// <summary>Whether this texture paint uses 4-bit connected-texture adjacency.</summary>
    public readonly bool Connected;

    /// <summary>Deterministic texture variant list for texture paints.</summary>
    public readonly string?[]? Textures;

    /// <summary>Swatch color in normalized sRGB (0..1) for color paints.</summary>
    public readonly Vector3 Color;

    /// <summary>Swatch color in linear RGB (0..1) for PBR shading.</summary>
    public readonly Vector3 LinearColor;

    /// <summary>Creates a texture paint asset.</summary>
    public static Paint CreateTexture(
        string id,
        string? name,
        BrickAlpha alpha,
        bool connected,
        string?[] textures
    ) {
        return new Paint(id, name, PaintKind.Texture, alpha, connected, textures, Vector3.One);
    }

    /// <summary>Creates a color swatch paint asset from normalized sRGB components (0..1).</summary>
    public static Paint CreateColor(
        string id,
        string? name,
        Vector3 color
    ) {
        return new Paint(id, name, PaintKind.Color, BrickAlpha.Opaque, connected: false, textures: null, color);
    }

    private Paint(
        string id,
        string? name,
        PaintKind kind,
        BrickAlpha alpha,
        bool connected,
        string?[]? textures,
        Vector3 color
    ) {
        ID = id;
        Name = string.IsNullOrWhiteSpace(name) ? id : name;
        Kind = kind;
        Alpha = alpha;
        Connected = connected;
        Textures = textures;
        Color = color;
        LinearColor = Srgb.ToLinear(color);
    }

    /// <summary>Binds the numeric ID assigned by the sorted registry. Called once at database load.</summary>
    internal void BindDataID(ushort dataID)
    {
        DataID = dataID;
    }
}
