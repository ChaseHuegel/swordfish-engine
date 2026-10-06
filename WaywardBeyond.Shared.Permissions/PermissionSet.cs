using System;
using System.Collections.Generic;

namespace WaywardBeyond.Shared.Permissions;

/// <summary>
///     A compiled, immutable permission set for one user or for the default baseline. Lookups are
///     allocation-free: the query is walked as a span against the exact map, then the longest matching
///     trailing wildcard prefix, then the global wildcard. Deny wins at equal specificity.
/// </summary>
public sealed class PermissionSet
{
    /// <summary>A set that grants everything. The local host uses it.</summary>
    public static readonly PermissionSet AllowAll = new(
        allowAll: true,
        exact: [],
        wildcards: [],
        hasGlobal: false,
        global: false,
        roles: [],
        declared: [PermissionKey.WILDCARD]
    );

    private readonly bool _allowAll;
    private readonly Dictionary<string, bool>.AlternateLookup<ReadOnlySpan<char>> _exactLookup;
    private readonly Dictionary<string, bool>.AlternateLookup<ReadOnlySpan<char>> _wildcardsLookup;
    private readonly bool _hasGlobal;
    private readonly bool _global;
    private readonly string[] _roles;
    private readonly string[] _declared;

    internal PermissionSet(
        bool allowAll,
        Dictionary<string, bool> exact,
        Dictionary<string, bool> wildcards,
        bool hasGlobal,
        bool global,
        string[] roles,
        string[] declared
    ) {
        _allowAll = allowAll;
        _hasGlobal = hasGlobal;
        _global = global;
        _roles = roles;
        _declared = declared;
        _exactLookup = exact.GetAlternateLookup<ReadOnlySpan<char>>();
        _wildcardsLookup = wildcards.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>The roles that contributed to this set, including inherited and default roles.</summary>
    public IReadOnlyList<string> Roles => _roles;

    /// <summary>The declared nodes that contributed to this set, for diagnostics.</summary>
    public IReadOnlyList<string> DeclaredPermissions => _declared;

    public bool HasPermission(string permission)
    {
        if (_allowAll)
        {
            return true;
        }

        if (string.IsNullOrEmpty(permission))
        {
            return _hasGlobal && _global;
        }

        ReadOnlySpan<char> key = permission.AsSpan().Trim();
        if (key.IsEmpty)
        {
            return _hasGlobal && _global;
        }

        if (_exactLookup.TryGetValue(key, out bool exact))
        {
            return exact;
        }

        while (true)
        {
            int separator = key.LastIndexOf(PermissionKey.SEPARATOR);
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

        return _hasGlobal && _global;
    }
}
