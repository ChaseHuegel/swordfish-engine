using System.Collections.Generic;
using System.Linq;

namespace WaywardBeyond.Shared.Bricks;

/// <summary>
/// The headless, render-free view of a loaded brick definition. Carries the voxel id, shape, textures,
/// tags, and the custom mesh id (a string handle; the client resolves it to a renderable mesh). It does
/// not reference a graphics mesh, so it is usable by the server and headless consumers.
/// </summary>
public sealed class BrickInfo
{
    public readonly string ID;
    public readonly ushort DataID;
    public readonly bool Transparent;
    public readonly bool Passable;
    public readonly string? MeshID;
    public readonly BrickShape Shape;
    public readonly BrickTextures Textures;
    public readonly HashSet<string> Tags;

    public readonly bool Shapeable;
    public readonly bool LightSource;
    public readonly int Brightness;
    public readonly bool Entity;

    private readonly bool _hasOrientableTag;

    public BrickInfo(
        in string id,
        in ushort dataID,
        in bool transparent,
        in bool passable,
        in string? meshID,
        in BrickShape shape,
        in BrickTextures textures,
        in string[]? tags)
    {
        ID = id;
        DataID = dataID;
        Transparent = transparent;
        Passable = passable;
        MeshID = meshID;
        Shape = shape;
        Textures = textures;
        Tags = new HashSet<string>(tags ?? []);
        Shapeable = shape == BrickShape.Any;
        LightSource = tags?.Contains("light") ?? false;
        Brightness = LightSource ? 15 : 0;
        Entity = tags?.Contains("entity") ?? false;
        _hasOrientableTag = tags?.Contains("orientable") ?? false;
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
        bool isOrientableShape = isShapeableBrick && !isBlockShape;

        return isOrientableShape;
    }
}