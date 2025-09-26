using System.Linq;
using Swordfish.Bricks;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Bricks;

internal sealed class BrickInfo(
    in string id,
    in ushort dataID,
    in bool transparent,
    in bool passable,
    in Mesh? mesh,
    in BrickShape shape,
    in BrickTextures textures,
    in string[]? tags
) {
    public readonly string ID = id;
    public readonly ushort DataID = dataID;
    public readonly bool Transparent = transparent;
    public readonly bool Passable = passable;
    public readonly Mesh? Mesh = mesh;
    public readonly BrickShape Shape = shape;
    public readonly BrickTextures Textures = textures;
    public readonly string[] Tags = tags ?? [];

    public readonly bool Shapeable = shape == BrickShape.Any;
    
    private readonly Brick _defaultBrick = new(dataID, shape == BrickShape.Any ? (byte)BrickShape.Block : (byte)shape);
    
    private readonly bool _hasOrientableTag = tags?.Contains("orientable") ?? false;

    /// <summary>
    ///     Returns a data representation of this <see cref="BrickInfo"/>.
    /// </summary>
    public Brick ToBrick()
    {
        return _defaultBrick;
    }
    
    /// <summary>
    ///     Returns whether the provided shape is orientable for this brick.
    /// </summary>
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