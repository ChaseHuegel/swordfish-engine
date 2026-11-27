using Shoal.Modularity;
using Swordfish.Library.IO;

namespace WaywardBeyond.Config;

/// <summary>Creates Shoal module resolution options for Wayward Beyond hosts.</summary>
public static class PackModuleResolution
{
    /// <summary>Subdirectory under the application data root that holds per-user content packs.</summary>
    public const string PACKS_DIRECTORY = "packs";

    /// <summary>Gets the per-user packs directory for the given application paths.</summary>
    public static PathInfo GetPacksRoot(AppPaths appPaths)
    {
        return appPaths.DataRoot.At(PACKS_DIRECTORY);
    }

    /// <summary>Creates module resolution options that register the content-only user packs directory.</summary>
    public static ModuleResolutionOptions CreateOptions(AppPaths appPaths)
    {
        return new ModuleResolutionOptions
        {
            AdditionalRoots =
            [
                new ModuleSearchRoot(GetPacksRoot(appPaths), ModuleRootPolicy.ContentOnly),
            ],
        };
    }
}
