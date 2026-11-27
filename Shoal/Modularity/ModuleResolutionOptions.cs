namespace Shoal.Modularity;

/// <summary>Configures module search roots and per-user load-order resolution for Shoal.</summary>
public sealed class ModuleResolutionOptions
{
    private const string FILE_MODULE_OPTIONS = "modules.toml";

    /// <summary>Gets the primary module search root. Defaults to <see cref="Paths.Modules"/> with full policy.</summary>
    public ModuleSearchRoot PrimaryRoot { get; init; } = new(Paths.Modules, ModuleRootPolicy.Full);

    /// <summary>Gets the primary module options file path. Defaults to <c>config/modules.toml</c>.</summary>
    public PathInfo PrimaryConfigPath { get; init; } = Paths.Config.At(FILE_MODULE_OPTIONS);

    /// <summary>Gets the optional policy for <see cref="AppPaths.ModulesRoot"/>, or <c>null</c> to skip automatic user modules discovery.</summary>
    public ModuleRootPolicy? UserDataModulesPolicy { get; init; } = ModuleRootPolicy.ContentOnly;

    /// <summary>Gets whether a per-user <c>modules.toml</c> under <see cref="AppPaths.ConfigRoot"/> merges with the primary load order.</summary>
    public bool MergeUserLoadOrder { get; init; } = true;

    /// <summary>Gets additional module search roots scanned after the primary root in declared order.</summary>
    public IReadOnlyList<ModuleSearchRoot> AdditionalRoots { get; init; } = [];

    /// <summary>Resolves the ordered, deduplicated list of module search roots for the given application paths.</summary>
    internal IReadOnlyList<ModuleSearchRoot> GetSearchRoots(in AppPaths appPaths)
    {
        StringComparer pathComparer = GetPathComparer();
        List<ModuleSearchRoot> resolved = [];
        HashSet<string> seenPaths = new(pathComparer);

        TryAddRoot(resolved, seenPaths, PrimaryRoot);

        if (UserDataModulesPolicy.HasValue)
        {
            PathInfo userModulesRoot = appPaths.ModulesRoot;
            string? userModulesCanonical = CanonicalizePath(in userModulesRoot);
            if (userModulesCanonical != null && !IsListedInAdditionalRoots(userModulesCanonical, pathComparer))
            {
                var userRoot = new ModuleSearchRoot(userModulesRoot, UserDataModulesPolicy.Value);
                TryAddRoot(resolved, seenPaths, in userRoot);
            }
        }

        if (AdditionalRoots != null)
        {
            for (int i = 0; i < AdditionalRoots.Count; i++)
            {
                ModuleSearchRoot additionalRoot = AdditionalRoots[i];
                TryAddRoot(resolved, seenPaths, in additionalRoot);
            }
        }

        return resolved;
    }

    /// <summary>Returns the per-user <c>modules.toml</c> path when user merging is enabled and the path differs from <see cref="PrimaryConfigPath"/>.</summary>
    internal PathInfo? GetUserConfigPath(in AppPaths appPaths)
    {
        if (!MergeUserLoadOrder)
        {
            return null;
        }

        PathInfo primaryConfigPath = PrimaryConfigPath;
        PathInfo userConfigPath = appPaths.ConfigRoot.At(FILE_MODULE_OPTIONS);
        string? primaryCanonical = CanonicalizePath(in primaryConfigPath);
        string? userCanonical = CanonicalizePath(in userConfigPath);
        if (userCanonical == null)
        {
            return null;
        }

        if (primaryCanonical != null && GetPathComparer().Equals(primaryCanonical, userCanonical))
        {
            return null;
        }

        return userConfigPath;
    }

    private bool IsListedInAdditionalRoots(string canonicalPath, StringComparer pathComparer)
    {
        if (AdditionalRoots == null)
        {
            return false;
        }

        for (int i = 0; i < AdditionalRoots.Count; i++)
        {
            PathInfo candidatePath = AdditionalRoots[i].Path;
            string? candidate = CanonicalizePath(in candidatePath);
            if (candidate != null && pathComparer.Equals(canonicalPath, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static void TryAddRoot(List<ModuleSearchRoot> target, HashSet<string> seenPaths, in ModuleSearchRoot root)
    {
        PathInfo rootPath = root.Path;
        string? canonical = CanonicalizePath(in rootPath);
        if (canonical == null || !seenPaths.Add(canonical))
        {
            return;
        }

        target.Add(root);
    }

    private static string? CanonicalizePath(in PathInfo path)
    {
        if (string.IsNullOrWhiteSpace(path.Value))
        {
            return null;
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Value));
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }
}
