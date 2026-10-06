using System.Collections.Generic;

namespace WaywardBeyond.Shared.Permissions;

/// <summary>A user's declared roles and permission nodes as written in one file.</summary>
public sealed class PermissionUserGrant
{
    public List<string> Roles { get; set; } = [];

    public List<string> Permissions { get; set; } = [];
}
