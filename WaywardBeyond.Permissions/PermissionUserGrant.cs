using System.Collections.Generic;

namespace WaywardBeyond.Permissions;

/// <summary>A user's declared roles and permission nodes as written in one file.</summary>
public sealed class PermissionUserGrant
{
    /// <summary>The names of the roles this user belongs to.</summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>The permission nodes this user grants or denies. They override the group baseline.</summary>
    public List<string> Permissions { get; set; } = [];
}
