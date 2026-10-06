using System;

namespace WaywardBeyond.Shared.Permissions;

/// <summary>
///     Parses dotted permission nodes. A node is an optional deny prefix (<c>-</c>), a dotted key, and an
///     optional trailing wildcard (<c>*</c> or <c>prefix.*</c>). Middle-segment wildcards are rejected.
/// </summary>
internal static class PermissionKey
{
    public const char SEPARATOR = '.';
    public const char DENY_PREFIX = '-';
    public const string WILDCARD = "*";
    public const string WILDCARD_SUFFIX = ".*";

    /// <summary>True when the value is a usable group, role, or user name.</summary>
    public static bool IsValidName(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsWhiteSpace(c) || c == '*')
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryParse(string? raw, out PermissionNode node)
    {
        node = default;
        if (raw == null)
        {
            return false;
        }

        string value = raw.Trim();
        if (value.Length == 0)
        {
            return false;
        }

        bool isDeny = value[0] == DENY_PREFIX;
        if (isDeny)
        {
            value = value[1..];
            if (value.Length == 0)
            {
                return false;
            }
        }

        if (value == WILDCARD)
        {
            node = new PermissionNode(isDeny, isGlobal: true, isWildcard: false, string.Empty);
            return true;
        }

        if (value.EndsWith(WILDCARD_SUFFIX, StringComparison.Ordinal))
        {
            string prefix = value[..^WILDCARD_SUFFIX.Length];
            if (!IsValidSegments(prefix))
            {
                return false;
            }

            node = new PermissionNode(isDeny, isGlobal: false, isWildcard: true, prefix);
            return true;
        }

        if (!IsValidSegments(value))
        {
            return false;
        }

        node = new PermissionNode(isDeny, isGlobal: false, isWildcard: false, value);
        return true;
    }

    private static bool IsValidSegments(string value)
    {
        if (value.Length == 0 || value[0] == SEPARATOR || value[^1] == SEPARATOR)
        {
            return false;
        }

        var segmentLength = 0;
        for (var i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsWhiteSpace(c) || c == '*')
            {
                return false;
            }

            if (c == SEPARATOR)
            {
                if (segmentLength == 0)
                {
                    return false;
                }

                segmentLength = 0;
                continue;
            }

            segmentLength++;
        }

        return segmentLength > 0;
    }
}
