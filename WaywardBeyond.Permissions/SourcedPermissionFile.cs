namespace WaywardBeyond.Permissions;

/// <summary>A parsed permission file and the path it came from, for diagnostics.</summary>
public readonly struct SourcedPermissionFile(string source, PermissionFile file)
{
    /// <summary>The path the file was loaded from.</summary>
    public readonly string Source = source;

    /// <summary>The parsed file.</summary>
    public readonly PermissionFile File = file;
}
