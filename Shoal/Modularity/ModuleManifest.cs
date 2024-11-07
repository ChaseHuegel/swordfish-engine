
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global

namespace Shoal.Modularity;

// ReSharper disable once ClassNeverInstantiated.Global
internal class ModuleManifest(string id, string name) : TomlConfiguration<ModuleManifest>
{
    [TomlProperty("ID")]
    public string? ID { get; private set; } = id;

    [TomlProperty("Name")]
    public string? Name { get; private set; } = name;

    [TomlProperty("Description")]
    public string? Description { get; init; }

    [TomlProperty("Author")]
    public string? Author { get; init; }

    [TomlProperty("Website")]
    public string? Website { get; init; }

    [TomlProperty("Source")]
    public string? Source { get; init; }

    [TomlProperty("RootPathOverride")]
    public PathInfo? RootPathOverride { get; init; }

    [TomlProperty("ScriptsPath")]
    public PathInfo? ScriptsPath { get; init; }

    [TomlProperty("AssembliesPath")]
    public PathInfo? AssembliesPath { get; init; }

    [TomlProperty("Assemblies")]
    public string[]? Assemblies { get; init; }
}