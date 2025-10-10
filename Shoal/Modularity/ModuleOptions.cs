namespace Shoal.Modularity;

// ReSharper disable once ClassNeverInstantiated.Global
internal class ModuleOptions(bool allowScriptCompilation, string[] loadOrder) : Toml<ModuleOptions>
{
    [TomlProperty("AllowScriptCompilation")]
    public bool AllowScriptCompilation { get; private set; } = allowScriptCompilation;

    [TomlProperty("LoadOrder")]
    public string[]? LoadOrder { get; private set; } = loadOrder;
}