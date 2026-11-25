#nullable enable
using System;
using System.Numerics;

namespace Swordfish.Library.Util;

/// <summary>Parses hexadecimal sRGB and sRGBA color strings.</summary>
public static class HexColor
{
    /// <summary>Returns whether <paramref name="c"/> is an ASCII hexadecimal digit.</summary>
    public static bool IsHexDigit(char c)
    {
        return (c >= '0' && c <= '9')
            || (c >= 'a' && c <= 'f')
            || (c >= 'A' && c <= 'F');
    }

    /// <summary>Parses a 6-digit hex RGB string with an optional leading '#' into normalized sRGB (0..1).</summary>
    public static bool TryParseRgb(string? value, out Vector3 color)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            color = default;
            return false;
        }

        return TryParseRgb(value.AsSpan(), out color);
    }

    /// <summary>Parses a 6-digit hex RGB span with an optional leading '#' into normalized sRGB (0..1).</summary>
    public static bool TryParseRgb(ReadOnlySpan<char> value, out Vector3 color)
    {
        ReadOnlySpan<char> span = NormalizeSlice(value);
        if (span.Length != 6
            || !TryParsePair(span, 0, out byte r)
            || !TryParsePair(span, 2, out byte g)
            || !TryParsePair(span, 4, out byte b))
        {
            color = default;
            return false;
        }

        color = new Vector3(r / 255f, g / 255f, b / 255f);
        return true;
    }

    /// <summary>Parses a 3-, 4-, 6-, or 8-digit hex string with an optional leading '#' into normalized sRGBA (0..1).</summary>
    public static bool TryParseRgba(string? value, out Vector4 color)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            color = default;
            return false;
        }

        return TryParseRgba(value.AsSpan(), out color);
    }

    /// <summary>Parses a 3-, 4-, 6-, or 8-digit hex span with an optional leading '#' into normalized sRGBA (0..1).</summary>
    public static bool TryParseRgba(ReadOnlySpan<char> value, out Vector4 color)
    {
        if (!TryParseRgbaBytes(value, out byte r, out byte g, out byte b, out byte a))
        {
            color = default;
            return false;
        }

        color = new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
        return true;
    }

    /// <summary>Parses a 3-, 4-, 6-, or 8-digit hex span with an optional leading '#' into RGBA bytes.</summary>
    public static bool TryParseRgbaBytes(ReadOnlySpan<char> value, out byte r, out byte g, out byte b, out byte a)
    {
        r = 0;
        g = 0;
        b = 0;
        a = 0;

        ReadOnlySpan<char> span = NormalizeSlice(value);
        switch (span.Length)
        {
            case 3:
            case 4:
                if (!TryParseExpandedNibble(span[0], out byte rShort)
                    || !TryParseExpandedNibble(span[1], out byte gShort)
                    || !TryParseExpandedNibble(span[2], out byte bShort))
                {
                    return false;
                }

                byte aShort = 255;
                if (span.Length == 4 && !TryParseExpandedNibble(span[3], out aShort))
                {
                    return false;
                }

                r = rShort;
                g = gShort;
                b = bShort;
                a = aShort;
                return true;

            case 6:
            case 8:
                if (!TryParsePair(span, 0, out byte rLong)
                    || !TryParsePair(span, 2, out byte gLong)
                    || !TryParsePair(span, 4, out byte bLong))
                {
                    return false;
                }

                byte aLong = 255;
                if (span.Length == 8 && !TryParsePair(span, 6, out aLong))
                {
                    return false;
                }

                r = rLong;
                g = gLong;
                b = bLong;
                a = aLong;
                return true;

            default:
                return false;
        }
    }

    private static ReadOnlySpan<char> NormalizeSlice(ReadOnlySpan<char> value)
    {
        ReadOnlySpan<char> span = value.Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span.Slice(1);
        }

        return span;
    }

    private static bool TryParseNibble(char c, out byte nibble)
    {
        if (c >= '0' && c <= '9')
        {
            nibble = (byte)(c - '0');
            return true;
        }

        if (c >= 'a' && c <= 'f')
        {
            nibble = (byte)(10 + (c - 'a'));
            return true;
        }

        if (c >= 'A' && c <= 'F')
        {
            nibble = (byte)(10 + (c - 'A'));
            return true;
        }

        nibble = 0;
        return false;
    }

    private static bool TryParseExpandedNibble(char c, out byte component)
    {
        if (!TryParseNibble(c, out byte nibble))
        {
            component = 0;
            return false;
        }

        component = (byte)((nibble << 4) | nibble);
        return true;
    }

    private static bool TryParsePair(ReadOnlySpan<char> span, int index, out byte component)
    {
        if (!TryParseNibble(span[index], out byte high) || !TryParseNibble(span[index + 1], out byte low))
        {
            component = 0;
            return false;
        }

        component = (byte)((high << 4) | low);
        return true;
    }
}
