using System;
using System.Numerics;
using Swordfish.Library.Types.Shapes;

namespace WaywardBeyond.Bricks;

/// <summary>Precomputed collision shapes for built-in <see cref="BrickShape"/> presets and custom bricks.</summary>
public static class BrickColliders
{
    private static readonly BrickColliderShape[] _block =
    [
        new(new Box3(Vector3.One), Vector3.Zero, Quaternion.Identity),
    ];

    private static readonly BrickColliderShape[] _slab =
    [
        new(new Box3(new Vector3(1f, 0.5f, 1f)), new Vector3(0f, -0.25f, 0f), Quaternion.Identity),
    ];

    private static readonly BrickColliderShape[] _plate =
    [
        new(new Box3(new Vector3(1f, 0.125f, 1f)), new Vector3(0f, -0.4375f, 0f), Quaternion.Identity),
    ];

    private static readonly BrickColliderShape[] _column =
    [
        new(new Box3(new Vector3(0.5f, 1f, 0.5f)), Vector3.Zero, Quaternion.Identity),
    ];

    private static readonly BrickColliderShape[] _stair =
    [
        new(new Box3(new Vector3(1f, 0.5f, 1f)), new Vector3(0f, -0.25f, 0f), Quaternion.Identity),
        new(new Box3(new Vector3(1f, 0.5f, 0.5f)), new Vector3(0f, 0.25f, -0.25f), Quaternion.Identity),
    ];

    private static readonly BrickColliderShape[] _slope =
    [
        new(
            new ConvexHull([
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
            ]),
            Vector3.Zero,
            Quaternion.Identity
        ),
    ];

    /// <summary>Returns the local-space collider primitives for a voxel of the given shape and brick.</summary>
    public static ReadOnlySpan<BrickColliderShape> ForShape(BrickShape shape, Brick? brick = null)
    {
        if (brick != null && !brick.Colliders.IsEmpty && (shape == BrickShape.Custom || !brick.Shapeable))
        {
            return brick.Colliders;
        }

        return shape switch
        {
            BrickShape.Slab => _slab,
            BrickShape.Plate => _plate,
            BrickShape.Column => _column,
            BrickShape.Stair => _stair,
            BrickShape.Slope => _slope,
            _ => _block,
        };
    }

    /// <summary>Returns whether a voxel with this shape and brick completely fills its unit cell for interior culling.</summary>
    public static bool IsFullBlock(BrickShape shape, Brick? brick = null)
    {
        return shape == BrickShape.Block && (brick == null || brick.Colliders.IsEmpty || brick.Shapeable);
    }
}
