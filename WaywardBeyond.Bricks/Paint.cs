using System;
using System.Collections.Generic;
using System.Globalization;
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

    /// <summary>All paint tags are case-insensitive.</summary>
    public readonly IReadOnlySet<string> Tags;

    /// <summary>Creates a texture paint asset.</summary>
    public static Paint CreateTexture(
        string id,
        string? name,
        BrickAlpha alpha,
        bool connected,
        string?[] textures,
        string[]? tags = null
    ) {
        return new Paint(id, name, PaintKind.Texture, alpha, connected, textures, Vector3.One, tags);
    }

    /// <summary>Creates a color swatch paint asset from normalized sRGB components (0..1).</summary>
    public static Paint CreateColor(
        string id,
        string? name,
        Vector3 color,
        string[]? tags = null
    ) {
        return new Paint(id, name, PaintKind.Color, BrickAlpha.Opaque, connected: false, textures: null, color, tags);
    }

    private Paint(
        string id,
        string? name,
        PaintKind kind,
        BrickAlpha alpha,
        bool connected,
        string?[]? textures,
        Vector3 color,
        string[]? tags
    ) {
        ID = id;
        Name = string.IsNullOrWhiteSpace(name) ? id : name;
        Kind = kind;
        Alpha = alpha;
        Connected = connected;
        Textures = textures;
        Color = color;
        LinearColor = new Vector3(SrgbToLinear(color.X), SrgbToLinear(color.Y), SrgbToLinear(color.Z));
        Tags = new HashSet<string>(tags ?? [], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Binds the numeric ID assigned by the sorted registry. Called once at database load.</summary>
    internal void BindDataID(ushort dataID)
    {
        DataID = dataID;
    }

    /// <summary>Parses a 6-digit hex color string (with optional leading '#') into normalized sRGB (0..1).</summary>
    public static bool TryParseHexColor(string? value, out Vector3 color)
    {
        color = Vector3.One;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ReadOnlySpan<char> span = value.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        if (span.Length != 6)
        {
            return false;
        }

        if (!byte.TryParse(span[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) ||
            !byte.TryParse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) ||
            !byte.TryParse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return false;
        }

        color = new Vector3(r / 255f, g / 255f, b / 255f);
        return true;
    }

    private static float SrgbToLinear(float c)
    {
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }
}
