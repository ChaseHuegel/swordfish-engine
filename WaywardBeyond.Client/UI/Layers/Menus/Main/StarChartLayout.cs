using System;
using System.Collections.Generic;
using System.Globalization;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>The facts that place one save on the star chart.</summary>
internal readonly record struct StarChartSource(string Guid, long Seed);

/// <summary>The computed position of one save on the star chart.</summary>
internal readonly record struct StarNodePlacement(
    string Guid,
    int X,
    int Y,
    int ClassIndex,
    bool Selected,
    bool Passthrough,
    int CardX,
    int CardY,
    int LabelX,
    int LabelY
);

/// <summary>One background star speck.</summary>
internal readonly record struct StarSpeck(int X, int Y);

/// <summary>Computed star chart geometry for one field size.</summary>
internal readonly record struct StarChartGeometry(
    IReadOnlyList<StarNodePlacement> Nodes,
    IReadOnlyList<StarSpeck> Specks
)
{
    /// <summary>Gets empty geometry, used for the first frame and a collapsed pane.</summary>
    public static readonly StarChartGeometry Empty = new(
        Array.Empty<StarNodePlacement>(),
        Array.Empty<StarSpeck>()
    );
}

/// <summary>Deterministic star chart geometry. It has no UI dependency.</summary>
internal static class StarChartLayout
{
    public const int NODE_INSET_X = 80;
    public const int NODE_INSET_Y = 28;
    public const int NODE_MIN_DISTANCE = 28;
    public const int MAX_SEPARATION_ATTEMPTS = 8;
    public const int HIT_SIZE = 24;
    public const int LABEL_WIDTH = 160;
    public const int LABEL_HEIGHT = 16;
    public const int LABEL_OFFSET_Y = 14;
    public const int GLYPH_SIZE = 16;
    public const int BRACKET_SIZE = 8;
    public const int BRACKET_OFFSET_LEFT = -14;
    public const int BRACKET_OFFSET_RIGHT = 10;
    public const int BRACKET_OFFSET_Y = -8;
    public const int CARD_WIDTH = 310;
    public const int CARD_HEIGHT = 132;
    public const int CARD_GAP = 36;
    public const int MAX_SPECKS = 120;
    public const int STAR_CLASS_COUNT = 4;

    private const double NORMALIZED_X_MIN = 0.14;
    private const double NORMALIZED_Y_MIN = 0.18;
    private const double NORMALIZED_X_SPAN = 0.72;
    private const double NORMALIZED_Y_SPAN = 0.66;
    private const double CARD_FLIP_Y_RATIO = 0.64;
    private const double UINT32_RANGE = 4294967296.0;
    private const double UINT64_RANGE = 18446744073709551616.0;

    /// <summary>Computes node and speck positions for a save set and field size.</summary>
    public static StarChartGeometry Compute(
        IReadOnlyList<StarChartSource> saves,
        string? selectedGuid,
        int width,
        int height
    )
    {
        if (saves.Count == 0 || width <= 0 || height <= 0)
        {
            return StarChartGeometry.Empty;
        }

        var ordered = new List<StarChartSource>(saves);
        ordered.Sort(static (a, b) => string.CompareOrdinal(a.Guid, b.Guid));

        int count = ordered.Count;
        var xs = new int[count];
        var ys = new int[count];
        var classes = new int[count];
        var selected = new bool[count];

        for (var i = 0; i < count; i++)
        {
            StarChartSource save = ordered[i];
            Place(save, width, height, xs, ys, i, out int x, out int y);
            xs[i] = x;
            ys[i] = y;
            classes[i] = ClassOf(save.Seed);
            selected[i] = selectedGuid != null && string.Equals(save.Guid, selectedGuid, StringComparison.Ordinal);
        }

        var guids = new string[count];
        var labelXs = new int[count];
        var labelYs = new int[count];
        for (var i = 0; i < count; i++)
        {
            guids[i] = ordered[i].Guid;

            //  Labels stay centered under their node. Overlap is accepted and made
            //  readable by the label scrim.
            labelXs[i] = xs[i] - (LABEL_WIDTH / 2);
            labelYs[i] = ys[i] + LABEL_OFFSET_Y;
        }

        var nodes = new List<StarNodePlacement>(count);
        for (var i = 0; i < count; i++)
        {
            bool passthrough = selected[i] && IsOverlapped(xs, ys, i, count);
            nodes.Add(
                new StarNodePlacement(
                    guids[i],
                    xs[i],
                    ys[i],
                    classes[i],
                    selected[i],
                    passthrough,
                    CardX(xs[i], width),
                    CardY(ys[i], height),
                    labelXs[i],
                    labelYs[i]
                )
            );
        }

        return new StarChartGeometry(nodes, BuildSpecks(width, height));
    }

    /// <summary>Gets whether the star class index for a seed is in range.</summary>
    public static int ClassOf(long seed)
    {
        ulong hash = FNV1a.ComputeHash64("class:" + seed.ToString(CultureInfo.InvariantCulture));
        return (int)(hash % STAR_CLASS_COUNT);
    }

    /// <summary>Gets the unclamped, unseparated center of a seed as a fraction of the field.</summary>
    public static double NormalizedX(long seed)
    {
        ulong hash = FNV1a.ComputeHash64(seed.ToString(CultureInfo.InvariantCulture));
        return NORMALIZED_X_MIN + ((hash >> 32) / UINT32_RANGE) * NORMALIZED_X_SPAN;
    }

    /// <summary>Gets the unclamped, unseparated center of a seed as a fraction of the field.</summary>
    public static double NormalizedY(long seed)
    {
        ulong hash = FNV1a.ComputeHash64(seed.ToString(CultureInfo.InvariantCulture));
        return NORMALIZED_Y_MIN + ((hash & 0xFFFFFFFFUL) / UINT32_RANGE) * NORMALIZED_Y_SPAN;
    }

    /// <summary>Gets the pixel offset that one separation attempt applies.</summary>
    public static void SeparationStep(string guid, int attempt, out int dx, out int dy)
    {
        ulong hash = FNV1a.ComputeHash64(guid + ":" + attempt.ToString(CultureInfo.InvariantCulture));
        double angle = (hash / UINT64_RANGE) * Math.Tau;
        dx = (int)Math.Round(NODE_MIN_DISTANCE * Math.Cos(angle));
        dy = (int)Math.Round(NODE_MIN_DISTANCE * Math.Sin(angle));
    }

    /// <summary>Gets whether two square hit boxes overlap.</summary>
    public static bool Overlaps(int ax, int ay, int bx, int by)
    {
        return Math.Abs(ax - bx) < HIT_SIZE && Math.Abs(ay - by) < HIT_SIZE;
    }

    /// <inheritdoc cref="Overlaps(int, int, int, int)"/>
    public static bool IsOverlapped(IReadOnlyList<int> xs, IReadOnlyList<int> ys, int index, int count)
    {
        for (var other = 0; other < count; other++)
        {
            if (other == index)
            {
                continue;
            }

            if (Overlaps(xs[index], ys[index], xs[other], ys[other]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the clamped left edge of the hover card for a node.</summary>
    public static int CardX(int nodeX, int width)
    {
        int left = nodeX + CARD_GAP;
        if (width - nodeX < CARD_WIDTH + CARD_GAP)
        {
            left = nodeX - CARD_GAP - CARD_WIDTH;
        }

        return Math.Clamp(left, 0, Math.Max(0, width - CARD_WIDTH));
    }

    /// <summary>Gets the clamped top edge of the hover card for a node.</summary>
    public static int CardY(int nodeY, int height)
    {
        int top = nodeY - CARD_GAP;
        if (nodeY > CARD_FLIP_Y_RATIO * height)
        {
            top = nodeY - CARD_HEIGHT + CARD_GAP;
        }

        return Math.Clamp(top, 0, Math.Max(0, height - CARD_HEIGHT));
    }

    /// <summary>Gets the horizontal clamp range for a node center.</summary>
    public static int ClampX(int x, int width)
    {
        int max = Math.Max(NODE_INSET_X, width - NODE_INSET_X);
        return Math.Clamp(x, Math.Min(NODE_INSET_X, max), max);
    }

    /// <summary>Gets the vertical clamp range for a node center.</summary>
    public static int ClampY(int y, int height)
    {
        int max = Math.Max(NODE_INSET_Y, height - NODE_INSET_Y);
        return Math.Clamp(y, Math.Min(NODE_INSET_Y, max), max);
    }

    private static void Place(
        in StarChartSource save,
        int width,
        int height,
        IReadOnlyList<int> xs,
        IReadOnlyList<int> ys,
        int index,
        out int x,
        out int y
    )
    {
        x = ClampX((int)Math.Round(NormalizedX(save.Seed) * width), width);
        y = ClampY((int)Math.Round(NormalizedY(save.Seed) * height), height);

        for (var attempt = 0; attempt < MAX_SEPARATION_ATTEMPTS; attempt++)
        {
            if (!IsNearEarlier(xs, ys, index, x, y))
            {
                return;
            }

            SeparationStep(save.Guid, attempt, out int dx, out int dy);
            x = ClampX(x + dx, width);
            y = ClampY(y + dy, height);        }
    }

    private static bool IsNearEarlier(IReadOnlyList<int> xs, IReadOnlyList<int> ys, int index, int x, int y)
    {
        for (var earlier = 0; earlier < index; earlier++)
        {
            double dx = x - xs[earlier];
            double dy = y - ys[earlier];
            if ((dx * dx) + (dy * dy) < NODE_MIN_DISTANCE * NODE_MIN_DISTANCE)
            {
                return true;
            }
        }

        return false;
    }

    private static StarSpeck[] BuildSpecks(int width, int height)
    {
        if (width <= 1 || height <= 1)
        {
            return Array.Empty<StarSpeck>();
        }

        int count = Math.Min(MAX_SPECKS, Math.Max(0, (width * height) / 2400));
        var specks = new StarSpeck[count];
        ulong state = FNV1a.ComputeHash64(
            width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture)
        );

        for (var i = 0; i < count; i++)
        {
            state = NextState(state);
            int x = (int)(state % (ulong)width);
            state = NextState(state);
            int y = (int)(state % (ulong)height);
            specks[i] = new StarSpeck(x, y);
        }

        return specks;
    }

    private static ulong NextState(ulong state)
    {
        state ^= state << 13;
        state ^= state >> 7;
        state ^= state << 17;
        return state;
    }
}
