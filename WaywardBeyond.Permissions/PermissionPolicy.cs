using System.Collections.Generic;

namespace WaywardBeyond.Permissions;

/// <summary>
///     An immutable, startup-compiled permission policy.
///     Unlisted users share the compiled default set.
/// </summary>
public sealed class PermissionPolicy
{
    /// <summary>The group every user belongs to implicitly.</summary>
    public const string DEFAULT_ROLE = "default";

    private readonly Dictionary<string, PermissionSet> _users;
    private readonly PermissionSet _defaultSet;

    internal PermissionPolicy(
        Dictionary<string, PermissionSet> users,
        PermissionSet defaultSet,
        int groupCount,
        int userCount,
        PermissionDiagnostics diagnostics
    ) {
        _users = users;
        _defaultSet = defaultSet;
        GroupCount = groupCount;
        UserCount = userCount;
        Diagnostics = diagnostics;
    }

    /// <summary>Warnings and errors collected while loading and compiling.</summary>
    public PermissionDiagnostics Diagnostics { get; }

    /// <summary>The number of loaded groups.</summary>
    public int GroupCount { get; }

    /// <summary>The number of loaded users.</summary>
    public int UserCount { get; }

    /// <summary>Returns the compiled permission set for a user. Unlisted users share the default set.</summary>
    public PermissionSet GetPermissions(string userId)
    {
        if (!string.IsNullOrEmpty(userId) && _users.TryGetValue(userId, out PermissionSet? permissions))
        {
            return permissions;
        }

        return _defaultSet;
    }

    /// <summary>Returns whether the user has a permission.</summary>
    /// <remarks>Prefer <see cref="GetPermissions"/> when checking multiple permissions.</remarks>
    public bool HasPermission(string userId, string permission)
    {
        return GetPermissions(userId).HasPermission(permission);
    }

    /// <summary>Merges, resolves, and compiles the loaded files. Warnings and errors collect in <paramref name="diagnostics"/>.</summary>
    public static PermissionPolicy Create(IReadOnlyList<SourcedPermissionFile> files, PermissionDiagnostics? diagnostics = null)
    {
        return PermissionCompiler.Compile(files, diagnostics ?? new PermissionDiagnostics());
    }
}
