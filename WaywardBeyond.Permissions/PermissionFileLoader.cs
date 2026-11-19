using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Swordfish.Library.IO;

namespace WaywardBeyond.Permissions;

/// <summary>
///     Loads every permission TOML from the module asset root and the admin config root. A parse failure
///     is recorded as a diagnostic and skipped so one bad file never blocks startup.
/// </summary>
public sealed class PermissionFileLoader(
    VirtualFileSystem vfs,
    IFileParseService fileParseService,
    AppPaths appPaths)
{
    private const string ASSET_ROOT = "permissions/";
    private const string CONFIG_ROOT = "config/permissions/";

    /// <summary>Loads and parses every permission TOML under both roots. Parse failures collect in <paramref name="diagnostics"/>.</summary>
    public IReadOnlyList<SourcedPermissionFile> Load(PermissionDiagnostics diagnostics)
    {
        PathInfo[] assetPaths = vfs.GetFiles(ASSET_ROOT, SearchOption.AllDirectories)
            .Where(static path => path.HasExtension(".toml"))
            .ToArray();
        if (assetPaths.Length == 0)
        {
            diagnostics.Warn($"No permission files were found under \"{ASSET_ROOT}\".");
        }

        var paths = new List<PathInfo>(assetPaths);
        StringComparer pathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var seenFullPaths = new HashSet<string>(pathComparer);

        AddConfigDirectory(new PathInfo(CONFIG_ROOT), paths, seenFullPaths);
        AddConfigDirectory(appPaths.ConfigRoot.At("permissions"), paths, seenFullPaths);
        paths.Sort(static (left, right) => string.CompareOrdinal(left.Value, right.Value));

        var files = new List<SourcedPermissionFile>();
        foreach (PathInfo path in paths)
        {
            PermissionFile file;
            try
            {
                file = fileParseService.Parse<PermissionFile>(path);
            }
            catch (Exception ex)
            {
                diagnostics.Error($"Failed to parse permission file \"{path.Value}\": {ex.Message}");
                continue;
            }

            if (file == null)
            {
                diagnostics.Error($"No permission parser is registered for \"{path.Value}\".");
                continue;
            }

            files.Add(new SourcedPermissionFile(path.Value, file));
        }

        return files;
    }

    private static void AddConfigDirectory(PathInfo directory, List<PathInfo> destination, HashSet<string> seenFullPaths)
    {
        foreach (PathInfo path in directory.GetFiles(SearchOption.AllDirectories))
        {
            if (!path.HasExtension(".toml"))
            {
                continue;
            }

            if (seenFullPaths.Add(Path.GetFullPath(path.Value)))
            {
                destination.Add(path);
            }
        }
    }
}
