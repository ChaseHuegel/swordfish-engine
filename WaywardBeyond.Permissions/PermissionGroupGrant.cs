using System.Collections.Generic;

namespace WaywardBeyond.Permissions;

/// <summary>A group's declared parents and permission nodes as written in one file.</summary>
public sealed class PermissionGroupGrant
{
    public List<string> Inherits { get; set; } = [];

    public List<string> Permissions { get; set; } = [];
}
