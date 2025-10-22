using System.Linq;
using Swordfish.Bricks;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Bricks;

internal sealed class BrickInfo
{
    public readonly string ID;
    public readonly ushort DataID;
    public readonly bool Transparent;
    public readonly bool Passable;
    public readonly Mesh? Mesh;
    public readonly BrickShape Shape;
    public readonly BrickTextures Textures;
    public readonly string[] Tags;

    public readonly bool Shapeable;
    public readonly bool LightSource;
    public readonly int Brightness;

    private readonly bool _hasOrientableTag;
    private readonly Brick _defaultBrick;

    public BrickInfo(in string id,
        in ushort dataID,
        in bool transparent,
        in bool passable,
        in Mesh? mesh,
        in BrickShape shape,
        in BrickTextures textures,
        in string[]? tags)
    {
        ID = id;
        DataID = dataID;
        Transparent = transparent;
        Passable = passable;
        Mesh = mesh;
        Shape = shape;
        Textures = textures;
        Tags = tags ?? [];
        Shapeable = shape == BrickShape.Any;
        LightSource = tags?.Contains("light") ?? false;
        Brightness = LightSource ? 15 : 0;
        _hasOrientableTag = tags?.Contains("orientable") ?? false;
        _defaultBrick = new Brick(dataID, new BrickData(shape == BrickShape.Any ? BrickShape.Block : shape, Brightness));
    }

    /// <summary>
    ///     Returns a data representation of this <see cref="BrickInfo"/>.
    /// </summary>
    public Brick ToBrick()
    {
        return _defaultBrick;
    }
    
    /// <summary>
    ///     Returns a data representation of this <see cref="BrickInfo"/>
    ///     with a desired shape and optional orientation.
    /// </summary>
    public Brick ToBrick(BrickShape shape, BrickOrientation orientation = default)
    {
        Brick brick = ToBrick();
        if (Shapeable)
        {
            brick.Data = new BrickData(shape, Brightness);
        }
        
        if (IsOrientable(shape))
        {
            brick.Orientation = orientation;
        }

        return brick;
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