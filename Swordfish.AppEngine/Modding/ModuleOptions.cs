namespace Swordfish.AppEngine.Modding;

// ReSharper disable once ClassNeverInstantiated.Global
internal class ModuleOptions(bool allowScriptCompilation, string[] loadOrder) : TomlConfiguration<ModuleOptions>
{
    [TomlProperty("AllowScriptCompilation")]
    public bool AllowScriptCompilation { get; private set; } = allowScriptCompilation;

    [TomlProperty("LoadOrder")]
    public string[]? LoadOrder { get; private set; } = loadOrder;
}