using System.Collections.Generic;

namespace WaywardBeyond.Shared.Permissions;

/// <summary>
///     An immutable, startup-compiled permission policy. Every user id declared in the loaded files is
///     compiled once; all unlisted users share the compiled default set, so a lookup allocates nothing
///     and takes at most one map hop plus a set check.
/// </summary>
public sealed class PermissionPolicy : IPermissionPolicy
{
    /// <summary>The group every user belongs to implicitly.</summary>
    public const string DEFAULT_ROLE = "default";

    private readonly Dictionary<string, PermissionSet> _users;
    private readonly PermissionSet _defaultSet;
    private readonly string[] _groupNames;
    private readonly string[] _userNames;

    internal PermissionPolicy(
        Dictionary<string, PermissionSet> users,
        PermissionSet defaultSet,
        string[] groupNames,
        string[] userNames,
        PermissionDiagnostics diagnostics
    ) {
        _users = users;
        _defaultSet = defaultSet;
        _groupNames = groupNames;
        _userNames = userNames;
        Diagnostics = diagnostics;
    }

    /// <summary>Warnings and errors collected while loading and compiling.</summary>
    public PermissionDiagnostics Diagnostics { get; }

    public IReadOnlyList<string> GroupNames => _groupNames;

    public IReadOnlyList<string> UserNames => _userNames;

    public int GroupCount => _groupNames.Length;

    public int UserCount => _userNames.Length;

    /// <summary>The compiled set shared by every unlisted user.</summary>
    public PermissionSet DefaultSet => _defaultSet;

    public PermissionSet GetPermissions(string userId)
    {
        if (!string.IsNullOrEmpty(userId) && _users.TryGetValue(userId, out PermissionSet? permissions))
        {
            return permissions;
        }

        return _defaultSet;
    }

    public bool HasPermission(string userId, string permission)
    {
        return GetPermissions(userId).HasPermission(permission);
    }

    public IReadOnlyCollection<string> GetRoles(string userId)
    {
        return GetPermissions(userId).Roles;
    }

    public IReadOnlyCollection<string> GetEffectivePermissions(string userId)
    {
        return GetPermissions(userId).DeclaredPermissions;
    }

    public static PermissionPolicy Create(IReadOnlyList<SourcedPermissionFile> files)
    {
        return PermissionCompiler.Compile(files, new PermissionDiagnostics());
    }

    public static PermissionPolicy Create(IReadOnlyList<SourcedPermissionFile> files, PermissionDiagnostics diagnostics)
    {
        return PermissionCompiler.Compile(files, diagnostics);
    }
}
