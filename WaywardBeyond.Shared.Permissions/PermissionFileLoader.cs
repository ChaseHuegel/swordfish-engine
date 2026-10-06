using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Swordfish.Library.IO;

namespace WaywardBeyond.Shared.Permissions;

/// <summary>
///     Loads every permission TOML from the module asset root and the admin config root in a
///     deterministic order. A parse failure is logged and skipped so one bad file never blocks startup.
/// </summary>
public sealed class PermissionFileLoader
{
    private readonly VirtualFileSystem _vfs;
    private readonly IFileParseService _fileParseService;
    private readonly ILogger<PermissionFileLoader> _logger;
    private readonly PermissionLoadOptions _options;

    public PermissionFileLoader(
        in VirtualFileSystem vfs,
        in IFileParseService fileParseService,
        in ILogger<PermissionFileLoader> logger,
        PermissionLoadOptions? options = null
    ) {
        _vfs = vfs;
        _fileParseService = fileParseService;
        _logger = logger;
        _options = options ?? new PermissionLoadOptions();
    }

    public IReadOnlyList<SourcedPermissionFile> Load(PermissionDiagnostics diagnostics)
    {
        var paths = new List<PathInfo>();
        paths.AddRange(_vfs.GetFiles(_options.AssetRoot, SearchOption.AllDirectories)
            .Where(static path => path.HasExtension(".toml")));
        paths.AddRange(AsDirectory(_options.ConfigRoot).GetFiles("*.toml", SearchOption.AllDirectories));
        paths.Sort(static (left, right) => string.CompareOrdinal(left.Value, right.Value));

        var files = new List<SourcedPermissionFile>();
        foreach (PathInfo path in paths)
        {
            PermissionFile file;
            try
            {
                file = _fileParseService.Parse<PermissionFile>(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse permission file \"{file}\".", path.Value);
                diagnostics.Error($"Failed to parse permission file \"{path.Value}\".");
                continue;
            }

            if (file == null)
            {
                _logger.LogError("No permission parser is registered for \"{file}\".", path.Value);
                diagnostics.Error($"No permission parser is registered for \"{path.Value}\".");
                continue;
            }

            files.Add(new SourcedPermissionFile(path.Value, file));
        }

        return files;
    }

    private static PathInfo AsDirectory(in PathInfo path)
    {
        if (path.Value.EndsWith('/') || path.Value.EndsWith(Path.DirectorySeparatorChar))
        {
            return path;
        }

        return new PathInfo(path.Scheme, path.Value + '/');
    }
}
