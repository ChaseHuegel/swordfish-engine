using WaywardBeyond.Bricks;
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