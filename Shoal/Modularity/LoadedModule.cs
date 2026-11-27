using Swordfish.Library.IO;

namespace Shoal.Modularity;

/// <summary>Describes one module that the engine loaded this session.</summary>
/// <param name="ID">The module identifier from its manifest.</param>
/// <param name="Name">The display name from its manifest.</param>
/// <param name="Root">The module root directory after applying <c>RootPathOverride</c>.</param>
public readonly record struct LoadedModule(string ID, string Name, PathInfo Root);
