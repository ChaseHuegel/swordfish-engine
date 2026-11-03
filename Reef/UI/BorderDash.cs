using System;

namespace Reef.UI;

/// <summary>Resolves border dash patterns and enumerates the dashes of one side.</summary>
public static class BorderDash
{
    /// <summary>Gets the dash and gap lengths for a side of the given thickness.</summary>
    /// <remarks>A zero dash length draws a solid side.</remarks>
    public static void Resolve(in BorderStyle border, int thickness, out int dash, out int gap)
    {
        if (border.Dash > 0)
        {
            dash = border.Dash;
            gap = border.Gap > 0 ? border.Gap : border.Dash;
            return;
        }

        switch (border.LineStyle)
        {
            case BorderLineStyle.Dashed:
                dash = Math.Max(1, thickness * 2);
                gap = dash;
                return;
            case BorderLineStyle.Dotted:
                dash = Math.Max(1, thickness);
                gap = dash;
                return;
            default:
                dash = 0;
                gap = 0;
                return;
        }
    }

    /// <summary>Gets the number of dashes that start inside a side of the given length.</summary>
    public static int Count(int length, int dash, int gap)
    {
        if (dash <= 0 || length <= 0)
        {
            return 0;
        }

        int pitch = dash + Math.Max(0, gap);
        return (length + pitch - 1) / pitch;
    }
}

/// <summary>Walks the dashes of one border side along an axis.</summary>
public struct DashSegments
{
    private readonly int _length;
    private readonly int _dash;
    private readonly int _pitch;
    private readonly int _count;
    private int _index;

    /// <summary>Creates the walker for a side of the given length.</summary>
    public DashSegments(int length, int dash, int gap)
    {
        _length = length;
        _dash = dash;
        _pitch = dash + Math.Max(0, gap);
        _count = BorderDash.Count(length, dash, gap);
        _index = -1;
        Offset = 0;
        SegmentLength = 0;
    }

    /// <summary>Gets the offset of the current dash from the start of the side.</summary>
    public int Offset { get; private set; }

    /// <summary>Gets the length of the current dash, truncated at the end of the side.</summary>
    public int SegmentLength { get; private set; }

    /// <summary>Advances to the next dash.</summary>
    public bool MoveNext()
    {
        _index++;
        if (_index >= _count)
        {
            return false;
        }

        Offset = _index * _pitch;
        SegmentLength = Math.Min(_dash, _length - Offset);
        return SegmentLength > 0;
    }
}
