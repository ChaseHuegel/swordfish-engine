using System;
using System.Collections.Generic;
using System.Numerics;

namespace WaywardBeyond.Bricks;

/// <summary>An asset representing a brick's type information.</summary>
public sealed class Brick
{
    /// <summary>The minimum ID value.</summary>
    public const ushort MIN_ID = 1;

    private static readonly IReadOnlyDictionary<BrickEventKind, string[]> _noSounds =
        new Dictionary<BrickEventKind, string[]>();

    private static readonly IReadOnlyDictionary<string, float> _noStatistics =
        new Dictionary<string, float>(StringComparer.Ordinal);
    
    /// <inheritdoc cref="BrickDefinition.ID"/>
    public readonly string ID;

    /// <summary>The voxel id for this brick. Bound once at database load.</summary>
    public ushort DataID { get; private set; }

    /// <summary>The sound tags declared per <see cref="BrickEventKind"/>, or empty when the brick is silent.</summary>
    public readonly IReadOnlyDictionary<BrickEventKind, string[]> Sounds;

    /// <summary>Custom numeric statistics declared on this brick, or empty when none are declared.</summary>
    public readonly IReadOnlyDictionary<string, float> Statistics;

    public readonly BrickAlpha Alpha;
    public readonly bool CastShadows;
    public readonly bool Passable;
    public readonly string? MeshID;
    public readonly BrickShape Shape;
    public readonly BrickTextures Textures;

    /// <summary>Default albedo color in normalized sRGB (0..1) for unpainted faces. Defaults to white.</summary>
    public readonly Vector3 Color;

    /// <summary>Default albedo color in linear RGB (0..1) for unpainted faces.</summary>
    public readonly Vector3 LinearColor;
    private readonly BrickColliderShape[] _colliders;

    /// <summary>Optional custom collider primitives declared on this brick, or empty for shape presets.</summary>
    public ReadOnlySpan<BrickColliderShape> Colliders => _colliders;

    public readonly bool Shapeable;
    public readonly bool LightSource;
    public readonly int Brightness;
    public readonly bool Entity;
    public readonly bool Paintable;

    private readonly bool _orientable;

    public Brick(
        in string id,
        in BrickAlpha alpha,
        in bool passable,
        in string? meshID,
        in BrickShape shape,
        in BrickTextures textures,
        in bool castShadows = true,
        in BrickColliderShape[]? colliders = null,
        in Dictionary<BrickEventKind, string[]>? sounds = null,
        in bool lightSource = false,
        in bool entity = false,
        in bool paintable = false,
        in bool orientable = false,
        in Vector3? color = null,
        in IReadOnlyDictionary<string, float>? statistics = null
    ) {
        ID = id;
        Alpha = alpha;
        CastShadows = castShadows;
        Passable = passable;
        MeshID = meshID;
        Shape = shape;
        Textures = textures;
        Color = color ?? Vector3.One;
        LinearColor = Srgb.ToLinear(Color);
        _colliders = colliders ?? [];
        Sounds = sounds ?? _noSounds;
        Statistics = statistics ?? _noStatistics;
        Shapeable = shape == BrickShape.Any;
        LightSource = lightSource;
        Brightness = LightSource ? 15 : 0;
        Entity = entity;
        Paintable = paintable;
        _orientable = orientable;
    }

    /// <summary>Gets the declared statistic value for <paramref name="statisticId"/>, or <paramref name="defaultValue"/>.</summary>
    public float GetStatistic(string statisticId, float defaultValue = 0f)
    {
        return TryGetStatistic(statisticId, out float value) ? value : defaultValue;
    }

    /// <summary>Attempts to read the declared statistic value for <paramref name="statisticId"/>.</summary>
    public bool TryGetStatistic(string statisticId, out float value)
    {
        if (string.IsNullOrEmpty(statisticId))
        {
            value = 0f;
            return false;
        }

        return Statistics.TryGetValue(statisticId, out value);
    }

    /// <summary>Returns whether the provided shape is orientable for this brick.</summary>
    public bool IsOrientable(BrickShape shape)
    {
        if (_orientable)
        {
            return true;
        }

        bool isBlockShape = shape == BrickShape.Block;
        bool isShapeableBrick = Shape == BrickShape.Any;

        //  Shapeable bricks are implicitly orientable, unless the desired shape is a block.
        return isShapeableBrick && !isBlockShape;
    }

    /// <summary>Binds the voxel id assigned by the sorted registry. Called once at database load.</summary>
    internal void BindDataID(ushort dataID)
    {
        DataID = dataID;
    }
}
