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
    IFileParseService fileParseService)
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
        paths.AddRange(new PathInfo(CONFIG_ROOT).GetFiles(SearchOption.AllDirectories)
            .Where(static path => path.HasExtension(".toml")));
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
}
