namespace Shoal.Modularity;

internal static class ModuleManifestExtensions
{
    public static IPath GetRootPath(this ParsedFile<ModuleManifest> manifestFile)
    {
        return manifestFile.Value.RootPathOverride ?? manifestFile.Path.GetDirectory();
    }
}