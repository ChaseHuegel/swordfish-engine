using System.Collections.Generic;

namespace WaywardBeyond.Permissions;

/// <summary>The TOML shape of one permission file. Same-named groups and users merge across files.</summary>
public sealed class PermissionFile
{
    public Dictionary<string, PermissionGroupGrant> Groups { get; set; } = [];

    public Dictionary<string, PermissionUserGrant> Users { get; set; } = [];
}
