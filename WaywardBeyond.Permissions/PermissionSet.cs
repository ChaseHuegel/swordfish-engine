using System;
using System.Collections.Generic;

namespace WaywardBeyond.Permissions;

/// <summary>A compiled, immutable permission set for one user or the default baseline. Lookups allocate nothing.</summary>
public sealed class PermissionSet
{
    /// <summary>A set that grants every permission.</summary>
    public static readonly PermissionSet AllowAll = new([], [], true);

    private readonly bool? _global;
    private readonly Dictionary<string, bool>.AlternateLookup<ReadOnlySpan<char>> _exactLookup;
    private readonly Dictionary<string, bool>.AlternateLookup<ReadOnlySpan<char>> _wildcardsLookup;

    internal PermissionSet(
        Dictionary<string, bool> exact,
        Dictionary<string, bool> wildcards,
        bool? global
    ) {
        _global = global;
        _exactLookup = exact.GetAlternateLookup<ReadOnlySpan<char>>();
        _wildcardsLookup = wildcards.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    ///     Returns true when this set grants the permission. The exact node wins, then the longest
    ///     matching wildcard prefix, then the global wildcard.
    /// </summary>
    public bool HasPermission(string permission)
    {
        if (string.IsNullOrEmpty(permission))
        {
            return _global ?? false;
        }

        ReadOnlySpan<char> key = permission.AsSpan().Trim();
        if (key.IsEmpty)
        {
            return _global ?? false;
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

        return _global ?? false;
    }
}
