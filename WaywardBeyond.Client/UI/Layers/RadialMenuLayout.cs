using System;
using System.Numerics;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>
/// Computes concentric multi-ring positions for a radial menu, expanding outward by ring as item count grows
/// and clamping to explicit and canvas-derived maximum radii.
/// </summary>
internal static class RadialMenuLayout
{
    /// <summary>Computes centered (X, Y) offsets and hit-wrapper size for <paramref name="count"/> radial nodes.</summary>
    public static RadialMenuLayoutResult Compute(
        int count,
        int canvasWidth,
        int canvasHeight,
        RadialMenuLayoutOptions options = default
    ) {
        if (options.NodeSize <= 0f)
        {
            options = RadialMenuLayoutOptions.Default;
        }

        if (count <= 0)
        {
            return new RadialMenuLayoutResult([], options.DefaultHitSize, ringCount: 0);
        }

        float ringStep = MathF.Max(1f, options.NodePitch);
        float startRadius = options.InnerRadius;

        float canvasMaxRadius = canvasWidth > 0 && canvasHeight > 0
            ? MathF.Max(startRadius, MathF.Min(canvasWidth, canvasHeight) * 0.5f - options.NodeSize * 0.5f - options.CanvasMargin)
            : startRadius + ringStep * 16f;

        float effectiveMaxRadius = options.MaxRadius.HasValue
            ? MathF.Min(options.MaxRadius.Value, canvasMaxRadius)
            : canvasMaxRadius;
        effectiveMaxRadius = MathF.Max(startRadius, effectiveMaxRadius);

        int maxRings = Math.Clamp(1 + (int)MathF.Floor(MathF.Max(0f, effectiveMaxRadius - startRadius) / ringStep), 1, 64);
        Span<int> ringCaps = maxRings <= 32 ? stackalloc int[maxRings] : new int[maxRings];

        int usedRings = 0;
        int totalCap = 0;
        for (var r = 0; r < maxRings; r++)
        {
            float radius = startRadius + r * ringStep;
            int cap = ComputeRingCapacity(radius, ringStep);
            ringCaps[r] = cap;
            totalCap += cap;
            usedRings = r + 1;

            if (totalCap >= count)
            {
                break;
            }
        }

        Span<int> ringCounts = usedRings <= 32 ? stackalloc int[usedRings] : new int[usedRings];
        DistributeItems(count, ringCaps[..usedRings], totalCap, ringCounts);

        var positions = new Vector2[count];
        float minSpacing = usedRings > 1 ? ringStep : float.MaxValue;
        var cursor = 0;

        for (var r = 0; r < usedRings; r++)
        {
            int itemsInRing = ringCounts[r];
            if (itemsInRing <= 0)
            {
                continue;
            }

            float radius = startRadius + r * ringStep;
            float angleStep = MathF.Tau / itemsInRing;
            //  Alternate half-step phase offset on odd rings so adjacent rings stagger cleanly while
            //  preserving left-right bilateral symmetry.
            float startAngle = (r & 1) == 1 ? angleStep * 0.5f : 0f;

            if (itemsInRing > 1)
            {
                float chord = 2f * radius * MathF.Sin(MathF.PI / itemsInRing);
                if (chord < minSpacing)
                {
                    minSpacing = chord;
                }
            }

            for (var i = 0; i < itemsInRing; i++)
            {
                float angle = startAngle + angleStep * i;
                float x = -radius * MathF.Sin(angle);
                float y = radius * MathF.Cos(angle);
                positions[cursor++] = new Vector2(x, y);
            }
        }

        int hitSize = minSpacing == float.MaxValue
            ? options.DefaultHitSize
            : Math.Clamp((int)MathF.Floor(minSpacing), (int)options.NodeSize, options.DefaultHitSize);

        return new RadialMenuLayoutResult(positions, hitSize, usedRings);
    }

    private static int ComputeRingCapacity(float radius, float pitch)
    {
        float ratio = pitch / (2f * radius);
        if (ratio >= 1f)
        {
            return 1;
        }

        return Math.Max(1, (int)MathF.Floor(MathF.PI / MathF.Asin(ratio)));
    }

    private static void DistributeItems(int totalItems, ReadOnlySpan<int> caps, int totalCap, Span<int> counts)
    {
        bool fitsWithoutOverlap = totalItems <= totalCap;
        var assigned = 0;

        for (var r = 0; r < counts.Length; r++)
        {
            int share = Math.Max(1, (int)MathF.Floor((float)totalItems * caps[r] / totalCap));
            if (fitsWithoutOverlap && share > caps[r])
            {
                share = caps[r];
            }

            counts[r] = share;
            assigned += share;
        }

        while (assigned < totalItems)
        {
            int bestRing = -1;
            float bestRatio = float.MaxValue;
            for (var r = 0; r < counts.Length; r++)
            {
                if (fitsWithoutOverlap && counts[r] >= caps[r])
                {
                    continue;
                }

                float fillRatio = (float)counts[r] / caps[r];
                if (fillRatio < bestRatio)
                {
                    bestRatio = fillRatio;
                    bestRing = r;
                }
            }

            if (bestRing < 0)
            {
                bestRing = counts.Length - 1;
            }

            counts[bestRing]++;
            assigned++;
        }

        while (assigned > totalItems)
        {
            int bestRing = -1;
            float bestRatio = float.MinValue;
            for (var r = 0; r < counts.Length; r++)
            {
                if (counts[r] <= 1)
                {
                    continue;
                }

                float fillRatio = (float)counts[r] / caps[r];
                if (fillRatio > bestRatio)
                {
                    bestRatio = fillRatio;
                    bestRing = r;
                }
            }

            if (bestRing < 0)
            {
                break;
            }

            counts[bestRing]--;
            assigned--;
        }

        //  Prefer even counts on inner rings for cleaner geometric symmetry (e.g. 6, 8, 12) by
        //  shifting 1 item to the next ring when capacity allows and ring order stays monotonic.
        for (var r = 0; r < counts.Length - 1; r++)
        {
            if ((counts[r] & 1) == 0)
            {
                continue;
            }

            if (counts[r] + 1 <= caps[r] && counts[r + 1] - 1 >= counts[r] + 1)
            {
                counts[r]++;
                counts[r + 1]--;
            }
            else if (counts[r] - 1 >= 4 && (!fitsWithoutOverlap || counts[r + 1] + 1 <= caps[r + 1]))
            {
                counts[r]--;
                counts[r + 1]++;
            }
        }
    }
}
