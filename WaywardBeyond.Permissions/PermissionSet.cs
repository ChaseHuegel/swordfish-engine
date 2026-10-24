using System;
using System.Collections.Generic;

namespace WaywardBeyond.Permissions;

/// <summary>
///     A compiled, immutable permission set. A set can carry a fallback set that answers the queries
///     this set has no opinion on, so a user's own entries win over the group baseline.
///     Lookups allocate nothing.
/// </summary>
public sealed class PermissionSet
{
    /// <summary>A set that grants every permission.</summary>
    public static readonly PermissionSet AllowAll = new([], [], true);

    private readonly bool? _global;
    private readonly PermissionSet? _fallback;
    private readonly Dictionary<string, bool>.AlternateLookup<ReadOnlySpan<char>> _exactLookup;
    private readonly Dictionary<string, bool>.AlternateLookup<ReadOnlySpan<char>> _wildcardsLookup;

    internal PermissionSet(
        Dictionary<string, bool> exact,
        Dictionary<string, bool> wildcards,
        bool? global,
        PermissionSet? fallback = null
    ) {
        _global = global;
        _fallback = fallback;
        _exactLookup = exact.GetAlternateLookup<ReadOnlySpan<char>>();
        _wildcardsLookup = wildcards.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    ///     Returns true when this set grants the permission. The exact node wins, then the longest
    ///     matching wildcard prefix, then the global wildcard. A query this set cannot answer falls
    ///     through to the fallback set.
    /// </summary>
    public bool HasPermission(string permission)
    {
        if (string.IsNullOrEmpty(permission))
        {
            return ImplicitDecision(permission);
        }

        ReadOnlySpan<char> key = permission.AsSpan().Trim();
        if (key.IsEmpty)
        {
            return ImplicitDecision(permission);
        }

        if (_exactLookup.TryGetValue(key, out bool exact))
        {
            return exact;
        }

        while (true)
        {
            int separator = key.LastIndexOf(PermissionSyntax.SEPARATOR);
            if (separator <= 0)
            {
                break;
            }

            key = key[..separator];
            if (_wildcardsLookup.TryGetValue(key, out bool wildcard))
            {
                return wildcard;
            }
        }

        return ImplicitDecision(permission);
    }

    private bool ImplicitDecision(string permission)
    {
        return _global ?? _fallback?.HasPermission(permission) ?? false;
    }
}
