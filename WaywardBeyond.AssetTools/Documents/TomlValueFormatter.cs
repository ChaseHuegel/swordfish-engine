using System.Globalization;
using System.Text;
using Tomlyn.Helpers;
using Tomlyn.Model;

namespace WaywardBeyond.AssetTools.Documents;

/// <summary>
///     Formats keys, scalars, and arrays into valid TOML literal tokens.
/// </summary>
internal static class TomlValueFormatter
{
    /// <summary>
    ///     Formats a single key segment as a bare key when legal, otherwise as a basic quoted string.
    /// </summary>
    public static string FormatKeySegment(string segment)
    {
        if (IsBareKey(segment))
        {
            return segment;
        }

        return FormatBasicString(segment);
    }

    /// <summary>
    ///     Formats a sequence of key segments joined by dots.
    /// </summary>
    public static string FormatDottedKey(IReadOnlyList<string> segments)
    {
        if (segments.Count == 1)
        {
            return FormatKeySegment(segments[0]);
        }

        var builder = new StringBuilder();
        for (int i = 0; i < segments.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('.');
            }

            builder.Append(FormatKeySegment(segments[i]));
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Formats a string value, preserving single-quoted literal style when safe and requested.
    /// </summary>
    public static string FormatString(string value, string? existingRawToken = null)
    {
        if (existingRawToken != null &&
            existingRawToken.Length >= 2 &&
            existingRawToken[0] == '\'' &&
            !existingRawToken.StartsWith("'''", StringComparison.Ordinal) &&
            CanUseLiteralString(value))
        {
            return string.Concat("'", value, "'");
        }

        return FormatBasicString(value);
    }

    /// <summary>
    ///     Formats a double-quoted TOML basic string with full escape handling.
    /// </summary>
    public static string FormatBasicString(string value)
    {
        return TomlFormatHelper.ToString(value, TomlPropertyDisplayKind.Default);
    }

    /// <summary>
    ///     Formats a signed 64-bit integer into TOML syntax.
    /// </summary>
    public static string FormatInteger(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Formats a floating-point value into TOML syntax.
    /// </summary>
    public static string FormatFloat(double value)
    {
        return TomlFormatHelper.ToString(value);
    }

    /// <summary>
    ///     Formats a boolean value into TOML syntax.
    /// </summary>
    public static string FormatBoolean(bool value)
    {
        return value ? "true" : "false";
    }

    /// <summary>
    ///     Formats a string array in either inline or multi-line layout.
    /// </summary>
    public static string FormatStringArray(
        IReadOnlyList<string> values,
        bool multiline,
        string lineEnding,
        string itemIndent,
        string closeIndent,
        bool trailingComma)
    {
        if (values.Count == 0)
        {
            return "[]";
        }

        var builder = new StringBuilder();
        if (!multiline)
        {
            builder.Append('[');
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(FormatBasicString(values[i]));
            }

            builder.Append(']');
            return builder.ToString();
        }

        builder.Append('[');
        builder.Append(lineEnding);
        for (int i = 0; i < values.Count; i++)
        {
            builder.Append(itemIndent);
            builder.Append(FormatBasicString(values[i]));
            if (i < values.Count - 1 || trailingComma)
            {
                builder.Append(',');
            }

            builder.Append(lineEnding);
        }

        builder.Append(closeIndent);
        builder.Append(']');
        return builder.ToString();
    }

    /// <summary>
    ///     Returns true when <paramref name="segment"/> is a valid TOML bare key.
    /// </summary>
    public static bool IsBareKey(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return false;
        }

        for (int i = 0; i < segment.Length; i++)
        {
            char c = segment[i];
            bool valid = (c >= 'A' && c <= 'Z') ||
                         (c >= 'a' && c <= 'z') ||
                         (c >= '0' && c <= '9') ||
                         c == '_' ||
                         c == '-';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanUseLiteralString(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '\'' || c == '\r' || c == '\n' || (c < 0x20 && c != '\t') || c == 0x7F)
            {
                return false;
            }
        }

        return true;
    }
}
