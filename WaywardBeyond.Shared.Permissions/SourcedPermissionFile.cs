namespace WaywardBeyond.Shared.Permissions;

/// <summary>A parsed permission file and the path it came from, for diagnostics.</summary>
public readonly struct SourcedPermissionFile(string source, PermissionFile file)
{
    public readonly string Source = source;

    public readonly PermissionFile File = file;
}
