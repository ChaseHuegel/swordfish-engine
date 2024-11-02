namespace Shoal.Modularity;

internal class ModulePathService(ParsedFile<ModuleManifest> manifestFile) : RootedPathService(manifestFile.GetRootPath().OriginalString), IModulePathService;