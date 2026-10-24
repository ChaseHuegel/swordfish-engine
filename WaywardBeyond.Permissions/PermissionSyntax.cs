using System;
using Swordfish.Library.Util;

namespace WaywardBeyond.Permissions;

/// <summary>
///     The syntax of dotted permission nodes: an optional deny prefix (<c>-</c>), a dotted key, and an
///     optional trailing wildcard (<c>*</c> or <c>prefix.*</c>). Middle-segment wildcards are rejected.
/// </summary>
internal static class PermissionSyntax
{
    internal const char SEPARATOR = '.';
    internal const char DENY_PREFIX = '-';
    internal const string WILDCARD = "*";
    internal const string WILDCARD_SUFFIX = ".*";

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

    /// <summary>Parses a permission node. Invalid input returns a failed result.</summary>
    public static Result<PermissionNode> Parse(string? raw)
    {
        if (raw == null)
        {
            return Result<PermissionNode>.FromFailure(message: null);
        }

        string value = raw.Trim();
        if (value.Length == 0)
        {
            return Result<PermissionNode>.FromFailure(message: null);
        }

        bool isDeny = value[0] == DENY_PREFIX;
        if (isDeny)
        {
            value = value[1..];
            if (value.Length == 0)
            {
                return Result<PermissionNode>.FromFailure(message: null);
            }
        }

        if (value == WILDCARD)
        {
            return Result<PermissionNode>.FromSuccess(new PermissionNode(isDeny, isGlobal: true, isWildcard: false, string.Empty));
        }

        if (value.EndsWith(WILDCARD_SUFFIX, StringComparison.Ordinal))
        {
            string prefix = value[..^WILDCARD_SUFFIX.Length];
            if (!IsValidSegments(prefix))
            {
                return Result<PermissionNode>.FromFailure(message: null);
            }

            return Result<PermissionNode>.FromSuccess(new PermissionNode(isDeny, isGlobal: false, isWildcard: true, prefix));
        }

        if (!IsValidSegments(value))
        {
            return Result<PermissionNode>.FromFailure(message: null);
        }

        return Result<PermissionNode>.FromSuccess(new PermissionNode(isDeny, isGlobal: false, isWildcard: false, value));
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
