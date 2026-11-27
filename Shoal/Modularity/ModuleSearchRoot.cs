namespace Shoal.Modularity;

/// <summary>Describes a directory scanned for module manifests and the policy enforced on modules found there.</summary>
public readonly record struct ModuleSearchRoot(PathInfo Path, ModuleRootPolicy Policy = ModuleRootPolicy.Full);
