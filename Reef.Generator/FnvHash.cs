using System;

namespace Reef.Generator;

internal static class FnvHash
{
    public static string HashPath(string path)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;

        for (var i = 0; i < path.Length; i++)
        {
            hash ^= path[i];
            hash *= prime;
        }

        return hash.ToString("x8");
    }

    public static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "_";
        }

        var builder = new System.Text.StringBuilder(value.Length + 1);
        for (var i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool valid = char.IsLetterOrDigit(c) || c == '_';
            if (!valid)
            {
                builder.Append('_');
                continue;
            }

            if (i == 0 && char.IsDigit(c))
            {
                builder.Append('_');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    public static string CombinePaths(params string?[] parts)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < parts.Length; i++)
        {
            string? part = parts[i];
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            if (builder.Length > 0 && builder[builder.Length - 1] != '_')
            {
                builder.Append('_');
            }

            builder.Append(part);
        }

        return builder.ToString();
    }

    public static string EscapeString(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 2);
        builder.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        builder.Append('"');
        return builder.ToString();
    }

    public static string FormatFloat(float value)
    {
        if (Math.Abs(value) < 0.0000001f)
        {
            return "0f";
        }

        return value.ToString("0.#######", System.Globalization.CultureInfo.InvariantCulture) + "f";
    }

    public static string FormatVector(System.Numerics.Vector4 value)
    {
        return "new global::System.Numerics.Vector4("
               + FormatFloat(value.X) + ", "
               + FormatFloat(value.Y) + ", "
               + FormatFloat(value.Z) + ", "
               + FormatFloat(value.W) + ")";
    }
}
