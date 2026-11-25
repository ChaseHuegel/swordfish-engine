#nullable enable
using System.Numerics;

namespace Reef;

/// <summary>Parses hexadecimal color slices shared across Reef packages.</summary>
internal static class HexColor
{
    /// <summary>Returns whether <paramref name="c"/> is an ASCII hexadecimal digit.</summary>
    public static bool IsHexDigit(char c)
    {
        return (c >= '0' && c <= '9')
            || (c >= 'a' && c <= 'f')
            || (c >= 'A' && c <= 'F');
    }

    /// <summary>Parses a 3-, 4-, 6-, or 8-digit hex slice into normalized sRGBA (0..1).</summary>
    public static bool TryParseSlice(string text, int start, int length, out Vector4 color)
    {
        color = default;
        if (text == null || start < 0 || length < 0 || start > text.Length - length)
        {
            return false;
        }

        byte r;
        byte g;
        byte b;
        byte a = 255;

        switch (length)
        {
            case 3:
            case 4:
                if (!TryParseExpandedNibble(text[start], out r)
                    || !TryParseExpandedNibble(text[start + 1], out g)
                    || !TryParseExpandedNibble(text[start + 2], out b))
                {
                    return false;
                }

                if (length == 4 && !TryParseExpandedNibble(text[start + 3], out a))
                {
                    return false;
                }

                break;

            case 6:
            case 8:
                if (!TryParsePair(text, start, out r)
                    || !TryParsePair(text, start + 2, out g)
                    || !TryParsePair(text, start + 4, out b))
                {
                    return false;
                }

                if (length == 8 && !TryParsePair(text, start + 6, out a))
                {
                    return false;
                }

                break;

            default:
                return false;
        }

        color = new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
        return true;
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

    private static bool TryParsePair(string text, int index, out byte component)
    {
        if (!TryParseNibble(text[index], out byte high) || !TryParseNibble(text[index + 1], out byte low))
        {
            component = 0;
            return false;
        }

        component = (byte)((high << 4) | low);
        return true;
    }
}
