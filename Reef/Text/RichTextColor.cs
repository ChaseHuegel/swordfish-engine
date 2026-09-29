using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Reef.Text;

/// <summary>
///     Parses the color value that follows a "#" control character.
/// </summary>
public static class RichTextColor
{
    private const int MAX_NAME_LENGTH = 20;
    private static readonly Dictionary<string, uint> _palette = new(StringComparer.OrdinalIgnoreCase)
    {
        { "aliceblue", 0xF0F8FF }, { "antiquewhite", 0xFAEBD7 }, { "aqua", 0x00FFFF }, { "aquamarine", 0x7FFFD4 },
        { "azure", 0xF0FFFF }, { "beige", 0xF5F5DC }, { "bisque", 0xFFE4C4 }, { "black", 0x000000 },
        { "blanchedalmond", 0xFFEBCD }, { "blue", 0x0000FF }, { "blueviolet", 0x8A2BE2 }, { "brown", 0xA52A2A },
        { "burlywood", 0xDEB887 }, { "cadetblue", 0x5F9EA0 }, { "chartreuse", 0x7FFF00 }, { "chocolate", 0xD2691E },
        { "coral", 0xFF7F50 }, { "cornflowerblue", 0x6495ED }, { "cornsilk", 0xFFF8DC }, { "crimson", 0xDC143C },
        { "cyan", 0x00FFFF }, { "darkblue", 0x00008B }, { "darkcyan", 0x008B8B }, { "darkgoldenrod", 0xB8860B },
        { "darkgray", 0xA9A9A9 }, { "darkgreen", 0x006400 }, { "darkgrey", 0xA9A9A9 }, { "darkkhaki", 0xBDB76B },
        { "darkmagenta", 0x8B008B }, { "darkolivegreen", 0x556B2F }, { "darkorange", 0xFF8C00 }, { "darkorchid", 0x9932CC },
        { "darkred", 0x8B0000 }, { "darksalmon", 0xE9967A }, { "darkseagreen", 0x8FBC8F }, { "darkslateblue", 0x483D8B },
        { "darkslategray", 0x2F4F4F }, { "darkslategrey", 0x2F4F4F }, { "darkturquoise", 0x00CED1 }, { "darkviolet", 0x9400D3 },
        { "deeppink", 0xFF1493 }, { "deepskyblue", 0x00BFFF }, { "dimgray", 0x696969 }, { "dimgrey", 0x696969 },
        { "dodgerblue", 0x1E90FF }, { "firebrick", 0xB22222 }, { "floralwhite", 0xFFFAF0 }, { "forestgreen", 0x228B22 },
        { "fuchsia", 0xFF00FF }, { "gainsboro", 0xDCDCDC }, { "ghostwhite", 0xF8F8FF }, { "gold", 0xFFD700 },
        { "goldenrod", 0xDAA520 }, { "gray", 0x808080 }, { "green", 0x008000 }, { "greenyellow", 0xADFF2F },
        { "grey", 0x808080 }, { "honeydew", 0xF0FFF0 }, { "hotpink", 0xFF69B4 }, { "indianred", 0xCD5C5C },
        { "indigo", 0x4B0082 }, { "ivory", 0xFFFFF0 }, { "khaki", 0xF0E68C }, { "lavender", 0xE6E6FA },
        { "lavenderblush", 0xFFF0F5 }, { "lawngreen", 0x7CFC00 }, { "lemonchiffon", 0xFFFACD }, { "lightblue", 0xADD8E6 },
        { "lightcoral", 0xF08080 }, { "lightcyan", 0xE0FFFF }, { "lightgoldenrodyellow", 0xFAFAD2 }, { "lightgray", 0xD3D3D3 },
        { "lightgreen", 0x90EE90 }, { "lightgrey", 0xD3D3D3 }, { "lightpink", 0xFFB6C1 }, { "lightsalmon", 0xFFA07A },
        { "lightseagreen", 0x20B2AA }, { "lightskyblue", 0x87CEFA }, { "lightslategray", 0x778899 }, { "lightslategrey", 0x778899 },
        { "lightsteelblue", 0xB0C4DE }, { "lightyellow", 0xFFFFE0 }, { "lime", 0x00FF00 }, { "limegreen", 0x32CD32 },
        { "linen", 0xFAF0E6 }, { "magenta", 0xFF00FF }, { "maroon", 0x800000 }, { "mediumaquamarine", 0x66CDAA },
        { "mediumblue", 0x0000CD }, { "mediumorchid", 0xBA55D3 }, { "mediumpurple", 0x9370DB }, { "mediumseagreen", 0x3CB371 },
        { "mediumslateblue", 0x7B68EE }, { "mediumspringgreen", 0x00FA9A }, { "mediumturquoise", 0x48D1CC }, { "mediumvioletred", 0xC71585 },
        { "midnightblue", 0x191970 }, { "mintcream", 0xF5FFFA }, { "mistyrose", 0xFFE4E1 }, { "moccasin", 0xFFE4B5 },
        { "navajowhite", 0xFFDEAD }, { "navy", 0x000080 }, { "oldlace", 0xFDF5E6 }, { "olive", 0x808000 },
        { "olivedrab", 0x6B8E23 }, { "orange", 0xFFA500 }, { "orangered", 0xFF4500 }, { "orchid", 0xDA70D6 },
        { "palegoldenrod", 0xEEE8AA }, { "palegreen", 0x98FB98 }, { "paleturquoise", 0xAFEEEE }, { "palevioletred", 0xDB7093 },
        { "papayawhip", 0xFFEFD5 }, { "peachpuff", 0xFFDAB9 }, { "peru", 0xCD853F }, { "pink", 0xFFC0CB },
        { "plum", 0xDDA0DD }, { "powderblue", 0xB0E0E6 }, { "purple", 0x800080 }, { "rebeccapurple", 0x663399 },
        { "red", 0xFF0000 }, { "rosybrown", 0xBC8F8F }, { "royalblue", 0x4169E1 }, { "saddlebrown", 0x8B4513 },
        { "salmon", 0xFA8072 }, { "sandybrown", 0xF4A460 }, { "seagreen", 0x2E8B57 }, { "seashell", 0xFFF5EE },
        { "sienna", 0xA0522D }, { "silver", 0xC0C0C0 }, { "skyblue", 0x87CEEB }, { "slateblue", 0x6A5ACD },
        { "slategray", 0x708090 }, { "slategrey", 0x708090 }, { "snow", 0xFFFAFA }, { "springgreen", 0x00FF7F },
        { "steelblue", 0x4682B4 }, { "tan", 0xD2B48C }, { "teal", 0x008080 }, { "thistle", 0xD8BFD8 },
        { "tomato", 0xFF6347 }, { "turquoise", 0x40E0D0 }, { "violet", 0xEE82EE }, { "wheat", 0xF5DEB3 },
        { "white", 0xFFFFFF }, { "whitesmoke", 0xF5F5F5 }, { "yellow", 0xFFFF00 }, { "yellowgreen", 0x9ACD32 },
    };

    /// <summary>
    ///     Parses a value token beginning at valueStart. A null result is a reset to the element color.
    /// </summary>
    public static bool TryParseValue(string text, int valueStart, int valueLength, out Vector4? color, out int consumed)
    {
        color = null;
        consumed = -1;
        int end = valueStart + valueLength;

        if (TryParseHex(text, valueStart, end, out Vector4 hexColor, out int hexConsumed))
        {
            consumed = hexConsumed;
            color = hexColor;
        }

        if (TryParseName(text, valueStart, end, out Vector4 nameColor, out int nameConsumed) && nameConsumed > consumed)
        {
            consumed = nameConsumed;
            color = nameColor;
        }

        if (TryParseReset(text, valueStart, end, out int resetConsumed) && resetConsumed > consumed)
        {
            consumed = resetConsumed;
            color = null;
        }

        return consumed > 0;
    }

    private static bool TryParseHex(string text, int valueStart, int end, out Vector4 color, out int consumed)
    {
        color = default;
        consumed = 0;

        //  8-digit hex (with alpha) is preferred over 6-digit hex.
        foreach (int digitCount in new[] { 8, 6 })
        {
            if (end - valueStart < digitCount)
            {
                continue;
            }

            var valid = true;
            for (var i = 0; i < digitCount; i++)
            {
                if (!IsHexDigit(text[valueStart + i]))
                {
                    valid = false;
                    break;
                }
            }

            if (!valid)
            {
                continue;
            }

            //  The character after the hex run must not be a hex digit.
            int after = valueStart + digitCount;
            if (after < end && IsHexDigit(text[after]))
            {
                continue;
            }

            bool hasAlpha = digitCount == 8;
            uint r = ParseHexByte(text, valueStart);
            uint g = ParseHexByte(text, valueStart + 2);
            uint b = ParseHexByte(text, valueStart + 4);
            float a = hasAlpha ? ParseHexByte(text, valueStart + 6) / 255f : 1f;

            color = new Vector4(r / 255f, g / 255f, b / 255f, a);
            consumed = digitCount;
            return true;
        }

        return false;
    }

    private static bool TryParseName(string text, int valueStart, int end, out Vector4 color, out int consumed)
    {
        color = default;
        consumed = 0;

        var runEnd = valueStart;
        while (runEnd < end && char.IsLetter(text[runEnd]))
        {
            runEnd++;
        }

        int runLength = runEnd - valueStart;
        int maxLength = Math.Min(runLength, MAX_NAME_LENGTH);
        for (var length = maxLength; length >= 1; length--)
        {
            string candidate = text.Substring(valueStart, length);
            if (_palette.TryGetValue(candidate, out uint rgb))
            {
                color = ToVector4(rgb);
                consumed = length;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseReset(string text, int valueStart, int end, out int consumed)
    {
        consumed = 0;
        int remaining = end - valueStart;

        if (remaining >= 5 && text.AsSpan(valueStart, 5).Equals("RESET", StringComparison.OrdinalIgnoreCase))
        {
            consumed = 5;
            return true;
        }

        if (remaining >= 1 && text[valueStart] == 'R')
        {
            consumed = 1;
            return true;
        }

        return false;
    }

    private static bool IsHexDigit(char c)
    {
        return c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';
    }

    private static uint ParseHexByte(string text, int index)
    {
        return uint.Parse(text.AsSpan(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static Vector4 ToVector4(uint rgb)
    {
        return new Vector4(
            ((rgb >> 16) & 0xff) / 255f,
            ((rgb >> 8) & 0xff) / 255f,
            (rgb & 0xff) / 255f,
            1f
        );
    }
}