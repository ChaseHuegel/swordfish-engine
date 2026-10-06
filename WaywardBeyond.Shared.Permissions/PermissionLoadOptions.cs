using Swordfish.Library.IO;

namespace WaywardBeyond.Shared.Permissions;

/// <summary>
///     The two roots the loader scans. Module assets ship defaults, and the config root is the admin
///     override. Both are scanned recursively for TOML files.
/// </summary>
public sealed class PermissionLoadOptions
{
    public PathInfo AssetRoot { get; init; } = new("permissions/");

    public PathInfo ConfigRoot { get; init; } = new("config/permissions/");
}
