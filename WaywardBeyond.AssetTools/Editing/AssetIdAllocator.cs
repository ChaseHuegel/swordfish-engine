using System.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Allocates deterministic conflict-free asset identifiers using incrementing <c>_2</c>, <c>_3</c>, ... suffixes.
/// </summary>
public static class AssetIdAllocator
{
    /// <summary>
    ///     Allocates the lowest unused <c>_N</c> (<c>N &gt;= 2</c>) identifier for <paramref name="baseId"/> within <paramref name="workspace"/>.
    /// </summary>
    public static Result<string> AllocateNextId(
        WorkspaceIndex workspace,
        AssetKind kind,
        string baseId,
        IEnumerable<string>? additionalTakenIds = null)
    {
        if (workspace == null)
        {
            return Result<string>.FromFailure("Workspace is required.");
        }

        if (string.IsNullOrWhiteSpace(baseId))
        {
            return Result<string>.FromFailure("Base asset identifier is required.");
        }

        string normalizedInput = NormalizeInputId(kind, baseId);
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return Result<string>.FromFailure("Base asset identifier is required.");
        }

        string stem = ExtractBaseStem(normalizedInput);
        HashSet<string> takenIds = CollectExistingIds(workspace, kind);
        takenIds.Add(normalizedInput);

        if (additionalTakenIds != null)
        {
            foreach (string extra in additionalTakenIds)
            {
                if (!string.IsNullOrWhiteSpace(extra))
                {
                    takenIds.Add(NormalizeInputId(kind, extra));
                }
            }
        }

        for (int suffix = 2; suffix < int.MaxValue; suffix++)
        {
            string candidate = string.Concat(stem, "_", suffix.ToString(CultureInfo.InvariantCulture));
            if (!takenIds.Contains(candidate))
            {
                return Result<string>.FromSuccess(candidate);
            }
        }

        return Result<string>.FromFailure($"Could not allocate a unique identifier for '{baseId}'.");
    }

    /// <summary>
    ///     Extracts the base stem by stripping an existing trailing <c>_&lt;int&gt;</c> (<c>&gt;= 2</c>) suffix when present.
    /// </summary>
    public static string ExtractBaseStem(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        string trimmed = id.Trim();
        int underscoreIndex = trimmed.LastIndexOf('_');
        if (underscoreIndex <= 0 || underscoreIndex >= trimmed.Length - 1)
        {
            return trimmed;
        }

        char beforeUnderscore = trimmed[underscoreIndex - 1];
        if (beforeUnderscore == ':' || beforeUnderscore == '/' || beforeUnderscore == '\\')
        {
            return trimmed;
        }

        ReadOnlySpan<char> suffixSpan = trimmed.AsSpan(underscoreIndex + 1);
        if (suffixSpan[0] == '0')
        {
            return trimmed;
        }

        for (int i = 0; i < suffixSpan.Length; i++)
        {
            if (suffixSpan[i] < '0' || suffixSpan[i] > '9')
            {
                return trimmed;
            }
        }

        if (int.TryParse(suffixSpan, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) && parsed >= 2)
        {
            return trimmed.Substring(0, underscoreIndex);
        }

        return trimmed;
    }

    private static string NormalizeInputId(AssetKind kind, string id)
    {
        string trimmed = id.Trim();
        if (kind == AssetKind.Material || kind == AssetKind.Blueprint)
        {
            return trimmed.Replace('\\', '/').Trim('/');
        }

        return trimmed;
    }

    private static HashSet<string> CollectExistingIds(WorkspaceIndex workspace, AssetKind kind)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach ((AssetKind Kind, string Id) key in workspace.WinningAssets.Keys)
        {
            if (key.Kind == kind && !string.IsNullOrWhiteSpace(key.Id))
            {
                ids.Add(key.Id);
            }
        }

        for (int i = 0; i < workspace.AllAssets.Count; i++)
        {
            IndexedAsset asset = workspace.AllAssets[i];
            if (asset.Kind == kind && !string.IsNullOrWhiteSpace(asset.Id))
            {
                ids.Add(asset.Id);
            }
        }

        if (kind == AssetKind.Blueprint)
        {
            CollectBlueprintIds(workspace, ids);
        }
        else if (kind == AssetKind.Material)
        {
            CollectMaterialFileIds(workspace, ids);
        }
        else if (kind == AssetKind.Tag)
        {
            for (int i = 0; i < workspace.TagEntries.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(workspace.TagEntries[i].TagName))
                {
                    ids.Add(workspace.TagEntries[i].TagName);
                }
            }
        }
        else if (kind == AssetKind.Localization)
        {
            for (int i = 0; i < workspace.LocalizationEntries.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(workspace.LocalizationEntries[i].Key))
                {
                    ids.Add(workspace.LocalizationEntries[i].Key);
                }
            }
        }

        return ids;
    }

    private static void CollectBlueprintIds(WorkspaceIndex workspace, HashSet<string> ids)
    {
        for (int i = 0; i < workspace.BlueprintFiles.Count; i++)
        {
            string? relativeId = TryExtractRelativeAssetId(workspace, workspace.BlueprintFiles[i], "blueprints", ".wbp");
            if (!string.IsNullOrWhiteSpace(relativeId))
            {
                ids.Add(relativeId);
            }
        }
    }

    private static void CollectMaterialFileIds(WorkspaceIndex workspace, HashSet<string> ids)
    {
        foreach (string path in workspace.DocumentsByPath.Keys)
        {
            string? relativeId = TryExtractRelativeAssetId(workspace, path, "materials", ".toml");
            if (!string.IsNullOrWhiteSpace(relativeId))
            {
                ids.Add(relativeId);
            }
        }
    }

    private static string? TryExtractRelativeAssetId(
        WorkspaceIndex workspace,
        string filePath,
        string subDirectory,
        string extension)
    {
        string fullFile = Path.GetFullPath(filePath);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        for (int i = workspace.Roots.Count - 1; i >= 0; i--)
        {
            WorkspaceRoot root = workspace.Roots[i];
            if (!root.IsEnabled || string.IsNullOrWhiteSpace(root.AssetsDirectory))
            {
                continue;
            }

            string folder = Path.GetFullPath(Path.Combine(root.AssetsDirectory, subDirectory))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string prefix = folder + Path.DirectorySeparatorChar;
            if (!fullFile.StartsWith(prefix, comparison))
            {
                continue;
            }

            string relative = Path.GetRelativePath(folder, fullFile).Replace('\\', '/');
            if (relative.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                relative = relative.Substring(0, relative.Length - extension.Length);
            }

            return relative;
        }

        return null;
    }
}
