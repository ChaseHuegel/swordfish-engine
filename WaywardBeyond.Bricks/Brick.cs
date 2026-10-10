using System;
using System.Collections.Generic;

namespace WaywardBeyond.Bricks;

/// <summary>An asset representing a brick's type information.</summary>
public sealed class Brick
{
    /// <summary>The minimum ID value.</summary>
    public const ushort MinID = 1;
    
    /// <inheritdoc cref="BrickDefinition.ID"/>
    public readonly string ID;

    /// <summary>The voxel id for this brick. Bound once at database load.</summary>
    public ushort DataID { get; internal set; }

    /// <summary>All brick tags are case-insensitive.</summary>
    public readonly IReadOnlySet<string> Tags;

    public readonly bool Transparent;
    public readonly bool Passable;
    public readonly string? MeshID;
    public readonly BrickShape Shape;
    public readonly BrickTextures Textures;

    public readonly bool Shapeable;
    public readonly bool LightSource;
    public readonly int Brightness;
    public readonly bool Entity;

    private readonly bool _hasOrientableTag;

    public Brick(
        in string id,
        in bool transparent,
        in bool passable,
        in string? meshID,
        in BrickShape shape,
        in BrickTextures textures,
        in string[]? tags
    ) {
        ID = id;
        Transparent = transparent;
        Passable = passable;
        MeshID = meshID;
        Shape = shape;
        Textures = textures;
        Tags = new HashSet<string>(tags ?? [], StringComparer.OrdinalIgnoreCase);
        Shapeable = shape == BrickShape.Any;
        LightSource = Tags.Contains("light");
        Brightness = LightSource ? 15 : 0;
        Entity = Tags.Contains("entity");
        _hasOrientableTag = Tags.Contains("orientable");
    }

    /// <summary>Returns whether the provided shape is orientable for this brick.</summary>
    public bool IsOrientable(BrickShape shape)
    {
        if (_hasOrientableTag)
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