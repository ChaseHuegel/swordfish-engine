using System.Numerics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Systems;

/// <summary>Published placement target that drives the textured brick preview.</summary>
internal sealed class BrickPreviewState
{
    /// <summary>True when the preview should render at the published target.</summary>
    public bool Visible { get; private set; }

    /// <summary>The brick that will be placed.</summary>
    public Brick? Brick { get; private set; }

    /// <summary>The effective shape that will be placed.</summary>
    public BrickShape Shape { get; private set; }

    /// <summary>The voxel-local orientation that will be baked into the preview mesh.</summary>
    public Orientation Orientation { get; private set; }

    /// <summary>The world-space center of the target cell.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>The target structure's world orientation.</summary>
    public Quaternion StructureOrientation { get; private set; } = Quaternion.Identity;

    /// <summary>Publishes a visible placement target.</summary>
    public void Show(
        Brick brick,
        BrickShape shape,
        Orientation orientation,
        Vector3 position,
        Quaternion structureOrientation
    ) {
        Visible = true;
        Brick = brick;
        Shape = shape;
        Orientation = orientation;
        Position = position;
        StructureOrientation = structureOrientation;
    }

    /// <summary>Clears the visible placement target.</summary>
    public void Hide()
    {
        Visible = false;
        Brick = null;
    }
}
