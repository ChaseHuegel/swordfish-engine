using System;

namespace Reef.UI;

/// <summary>Viewport-aware placement for an overlay anchored to an element, rectangle, or point.</summary>
public readonly record struct OverlayPlacement
{
    /// <summary>Target element ID resolved in the current frame, or null when anchored to a rectangle or point.</summary>
    public string? TargetID { get; init; }

    /// <summary>Explicit viewport-space anchor rectangle used when <see cref="TargetID"/> is null.</summary>
    public IntRect TargetRect { get; init; }

    /// <summary>Preferred edge of the anchor on which the overlay opens.</summary>
    public OverlaySide Side { get; init; }

    /// <summary>Cross-axis alignment relative to the anchor edge.</summary>
    public OverlayAlign Align { get; init; }

    /// <summary>Pixel separation between the anchor edge and the overlay.</summary>
    public int Gap { get; init; }

    /// <summary>Minimum pixel margin kept from each viewport edge.</summary>
    public int ViewportMargin { get; init; }

    /// <summary>Whether the overlay flips to the opposite side or alignment when it overflows the viewport.</summary>
    public bool Flip { get; init; }

    /// <summary>Whether the overlay clamps inside the viewport margin as a final safeguard.</summary>
    public bool Clamp { get; init; }

    /// <summary>Anchors an overlay beside an element resolved by ID in the current frame.</summary>
    public static OverlayPlacement ForElement(
        string targetID,
        OverlaySide side = OverlaySide.Right,
        OverlayAlign align = OverlayAlign.Start,
        int gap = 8,
        int viewportMargin = 8,
        bool flip = true,
        bool clamp = true
    )
    {
        return new OverlayPlacement
        {
            TargetID = targetID,
            Side = side,
            Align = align,
            Gap = gap,
            ViewportMargin = viewportMargin,
            Flip = flip,
            Clamp = clamp,
        };
    }

    /// <summary>Anchors an overlay beside a viewport-space rectangle.</summary>
    public static OverlayPlacement ForRect(
        IntRect targetRect,
        OverlaySide side = OverlaySide.Right,
        OverlayAlign align = OverlayAlign.Start,
        int gap = 8,
        int viewportMargin = 8,
        bool flip = true,
        bool clamp = true
    )
    {
        return new OverlayPlacement
        {
            TargetRect = targetRect,
            Side = side,
            Align = align,
            Gap = gap,
            ViewportMargin = viewportMargin,
            Flip = flip,
            Clamp = clamp,
        };
    }

    /// <summary>Anchors an overlay beside a viewport-space point such as the cursor.</summary>
    public static OverlayPlacement AtPoint(
        IntVector2 point,
        OverlaySide side = OverlaySide.Right,
        OverlayAlign align = OverlayAlign.Start,
        int gap = 12,
        int viewportMargin = 8,
        bool flip = true,
        bool clamp = true
    )
    {
        return new OverlayPlacement
        {
            TargetRect = new IntRect(point, default),
            Side = side,
            Align = align,
            Gap = gap,
            ViewportMargin = viewportMargin,
            Flip = flip,
            Clamp = clamp,
        };
    }

    /// <summary>Resolves the viewport-space top-left origin for an overlay of <paramref name="overlaySize"/>.</summary>
    public static IntVector2 Resolve(
        IntRect anchor,
        IntVector2 overlaySize,
        IntVector2 viewportSize,
        in OverlayPlacement placement
    )
    {
        int margin = Math.Max(0, placement.ViewportMargin);
        int minX = margin;
        int maxRight = viewportSize.X - margin;
        int minY = margin;
        int maxBottom = viewportSize.Y - margin;

        int ow = overlaySize.X;
        int oh = overlaySize.Y;
        int gap = placement.Gap;

        int x;
        int y;

        if (placement.Side is OverlaySide.Right or OverlaySide.Left)
        {
            x = ResolvePrimaryAxis(
                preferPositive: placement.Side == OverlaySide.Right,
                anchorStart: anchor.Left,
                anchorEnd: anchor.Right,
                overlayExtent: ow,
                gap: gap,
                minBound: minX,
                maxBound: maxRight,
                flip: placement.Flip
            );
            y = ResolveCrossAxis(
                align: placement.Align,
                anchorStart: anchor.Top,
                anchorEnd: anchor.Bottom,
                anchorExtent: anchor.Size.Y,
                overlayExtent: oh,
                minBound: minY,
                maxBound: maxBottom,
                flip: placement.Flip
            );
        }
        else
        {
            y = ResolvePrimaryAxis(
                preferPositive: placement.Side == OverlaySide.Bottom,
                anchorStart: anchor.Top,
                anchorEnd: anchor.Bottom,
                overlayExtent: oh,
                gap: gap,
                minBound: minY,
                maxBound: maxBottom,
                flip: placement.Flip
            );
            x = ResolveCrossAxis(
                align: placement.Align,
                anchorStart: anchor.Left,
                anchorEnd: anchor.Right,
                anchorExtent: anchor.Size.X,
                overlayExtent: ow,
                minBound: minX,
                maxBound: maxRight,
                flip: placement.Flip
            );
        }

        if (placement.Clamp)
        {
            int clampMaxX = Math.Max(minX, maxRight - ow);
            int clampMaxY = Math.Max(minY, maxBottom - oh);
            x = Math.Clamp(x, minX, clampMaxX);
            y = Math.Clamp(y, minY, clampMaxY);
        }

        return new IntVector2(x, y);
    }

    private static int ResolvePrimaryAxis(
        bool preferPositive,
        int anchorStart,
        int anchorEnd,
        int overlayExtent,
        int gap,
        int minBound,
        int maxBound,
        bool flip
    )
    {
        int positivePos = anchorEnd + gap;
        int negativePos = anchorStart - overlayExtent - gap;

        if (preferPositive)
        {
            if (!flip || positivePos + overlayExtent <= maxBound)
            {
                return positivePos;
            }

            if (negativePos >= minBound)
            {
                return negativePos;
            }

            int negativeSpace = anchorStart - gap - minBound;
            int positiveSpace = maxBound - (anchorEnd + gap);
            return negativeSpace > positiveSpace ? negativePos : positivePos;
        }

        if (!flip || negativePos >= minBound)
        {
            return negativePos;
        }

        if (positivePos + overlayExtent <= maxBound)
        {
            return positivePos;
        }

        int posSpace = maxBound - (anchorEnd + gap);
        int negSpace = anchorStart - gap - minBound;
        return posSpace > negSpace ? positivePos : negativePos;
    }

    private static int ResolveCrossAxis(
        OverlayAlign align,
        int anchorStart,
        int anchorEnd,
        int anchorExtent,
        int overlayExtent,
        int minBound,
        int maxBound,
        bool flip
    )
    {
        int startPos = anchorStart;
        int endPos = anchorEnd - overlayExtent;

        return align switch
        {
            OverlayAlign.Start => flip && startPos + overlayExtent > maxBound && endPos >= minBound
                ? endPos
                : startPos,
            OverlayAlign.End => flip && endPos < minBound && startPos + overlayExtent <= maxBound
                ? startPos
                : endPos,
            _ => anchorStart + (anchorExtent - overlayExtent) / 2,
        };
    }
}
