using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// The headless, shared representation of a held item that places a brick. Carries exactly the facts
/// needed to author a placed voxel server-side (or predict one client-side) without depending on the
/// render-coupled client <c>BrickInfo</c>: the brick's canonical name, shape, whether it is shapeable,
/// whether it accepts an orientation, and its light output. <see cref="ToVoxel"/> resolves the canonical
/// name through a caller-supplied <see cref="IBrickIdMap"/>, so each side authors with its own voxel id
/// space.
/// </summary>
public readonly struct PlaceableBrick
{
    public readonly string Name;
    public readonly BrickShape Shape;
    public readonly bool Shapeable;
    public readonly bool HasOrientableTag;
    public readonly int Brightness;

    public PlaceableBrick(string name, BrickShape shape, bool shapeable, bool hasOrientableTag, int brightness)
    {
        Name = name;
        Shape = shape;
        Shapeable = shapeable;
        HasOrientableTag = hasOrientableTag;
        Brightness = brightness;
    }

    /// <summary>Returns whether the provided shape is orientable for this brick.</summary>
    public bool IsOrientable(BrickShape shape)
    {
        if (HasOrientableTag)
        {
            return true;
        }

        bool isBlockShape = shape == BrickShape.Block;
        bool isShapeableBrick = Shape == BrickShape.Any;

        //  Shapeable bricks are implicitly orientable, unless the desired shape is a block.
        return isShapeableBrick && !isBlockShape;
    }

    /// <summary>
    /// Returns the data representation of this brick with a desired shape and optional orientation,
    /// resolving the canonical <see cref="Name"/> through <paramref name="map"/> so the authored voxel
    /// uses the caller's own id space.
    /// </summary>
    public Voxel ToVoxel(BrickShape shape, IBrickIdMap map, Orientation orientation = default)
    {
        var voxel = new Voxel(map.Id(Name), new ShapeLight(Shape == BrickShape.Any ? BrickShape.Block : Shape, Brightness), _Orientation: 0);

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
}