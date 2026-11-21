using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>The resolved per-face paint operation when <see cref="InteractionResolution.Action"/> is <see cref="InteractionAction.Paint"/>.</summary>
public readonly struct PaintResolution(BrickFaceMask faceMask, PaintKind mode, ushort paintId, string? canonicalPaintId)
{
    public readonly BrickFaceMask FaceMask = faceMask;
    public readonly PaintKind Mode = mode;
    public readonly ushort PaintId = paintId;
    public readonly string? CanonicalPaintId = canonicalPaintId;
}

/// <summary>The resolved outcome of an interaction.</summary>
/// <remarks><see cref="InteractionAction.None"/> is a valid, expected outcome.</remarks>
public readonly struct InteractionResolution
{
    public readonly InteractionAction Action;
    public readonly int Entity;
    public readonly Int3 Coordinate;
    public readonly Voxel Voxel;
    public readonly PaintResolution Paint;

    public InteractionResolution(InteractionAction action, int entity, Int3 coordinate, Voxel voxel)
    {
        Action = action;
        Entity = entity;
        Coordinate = coordinate;
        Voxel = voxel;
        Paint = default;
    }

    public InteractionResolution(int entity, Int3 coordinate, in PaintResolution paint)
    {
        Action = InteractionAction.Paint;
        Entity = entity;
        Coordinate = coordinate;
        Voxel = default;
        Paint = paint;
    }

    public static readonly InteractionResolution None = new(action: InteractionAction.None, entity: 0, coordinate: default, voxel: default);
}
