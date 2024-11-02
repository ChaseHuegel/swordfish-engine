namespace Shoal.Modularity;

internal static class ModuleManifestExtensions
{
    public static PathInfo GetRootPath(this ParsedFile<ModuleManifest> manifestFile)
    {
        return manifestFile.Value.RootPathOverride ?? manifestFile.Path.GetDirectory();
    }
}