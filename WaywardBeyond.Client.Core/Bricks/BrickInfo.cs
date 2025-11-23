using System.Collections.Generic;
using System.Linq;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Models;

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
    public readonly HashSet<string> Tags;
    
    public readonly bool Shapeable;
    public readonly bool LightSource;
    public readonly int Brightness;
    public readonly bool Entity;
    
    private readonly bool _hasOrientableTag;
    private readonly Voxel _defaultVoxel;
    
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
        Tags = new HashSet<string>(tags ?? []);
        Shapeable = shape == BrickShape.Any;
        LightSource = tags?.Contains("light") ?? false;
        Brightness = LightSource ? 15 : 0;
        Entity = tags?.Contains("entity") ?? false;
        _hasOrientableTag = tags?.Contains("orientable") ?? false;
        _defaultVoxel = new Voxel(dataID, new ShapeLight(shape == BrickShape.Any ? BrickShape.Block : shape, Brightness), _Orientation: 0);
    }
    
    /// <summary>
    ///     Returns a data representation of this <see cref="BrickInfo"/>.
    /// </summary>
    public Voxel ToVoxel()
    {
        return _defaultVoxel;
    }
    
    /// <summary>
    ///     Returns a data representation of this <see cref="BrickInfo"/>
    ///     with a desired shape and optional orientation.
    /// </summary>
    public Voxel ToVoxel(BrickShape shape, Orientation orientation = default)
    {
        Voxel voxel = ToVoxel();
        if (Shapeable)
        {
            voxel.ShapeLight = new ShapeLight(shape, Brightness);
        }
        
        if (IsOrientable(shape))
        {
            voxel.Orientation = orientation;
        }

        return voxel;
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