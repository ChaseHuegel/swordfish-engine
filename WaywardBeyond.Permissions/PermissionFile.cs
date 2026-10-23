using System.Collections.Generic;

namespace WaywardBeyond.Permissions;

/// <summary>The TOML shape of one permission file. Same-named groups and users merge across files.</summary>
public sealed class PermissionFile
{
    /// <summary>The groups declared by name.</summary>
    public Dictionary<string, PermissionGroupGrant> Groups { get; set; } = [];

    /// <summary>The users declared by user id.</summary>
    public Dictionary<string, PermissionUserGrant> Users { get; set; } = [];
}
