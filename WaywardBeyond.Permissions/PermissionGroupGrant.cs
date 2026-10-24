using System.Collections.Generic;

namespace WaywardBeyond.Permissions;

/// <summary>A group's declared parents and permission nodes as written in one file.</summary>
public sealed class PermissionGroupGrant
{
    /// <summary>The names of the groups this group inherits from.</summary>
    public List<string> Inherits { get; set; } = [];

    /// <summary>The permission nodes this group grants or denies.</summary>
    public List<string> Permissions { get; set; } = [];
}
