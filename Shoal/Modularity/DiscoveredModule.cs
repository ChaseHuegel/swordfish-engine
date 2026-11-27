namespace Shoal.Modularity;

/// <summary>Pairs a parsed module manifest with the policy of the search root where it was discovered.</summary>
internal readonly record struct DiscoveredModule(ParsedFile<ModuleManifest> Manifest, ModuleRootPolicy Policy);
