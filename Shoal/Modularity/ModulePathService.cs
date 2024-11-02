namespace Shoal.Modularity;

internal class ModulePathService(ParsedFile<ModuleManifest> manifestFile) : PathService(manifestFile.GetRootPath().OriginalString), IModulePathService;