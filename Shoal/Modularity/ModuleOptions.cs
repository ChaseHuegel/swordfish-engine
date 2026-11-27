namespace Shoal.Modularity;

/// <summary>Represents the parsed <c>modules.toml</c> configuration controlling load order and script compilation.</summary>
// ReSharper disable once ClassNeverInstantiated.Global
internal class ModuleOptions(bool allowScriptCompilation, string[]? loadOrder) : Toml<ModuleOptions>
{
    /// <summary>Initializes an empty module options instance for TOML deserialization.</summary>
    public ModuleOptions()
        : this(false, null)
    {
    }

    /// <summary>Gets whether C# script compilation is permitted for modules.</summary>
    [TomlProperty("AllowScriptCompilation")]
    public bool AllowScriptCompilation { get; private set; } = allowScriptCompilation;

    /// <summary>Gets the ordered list of module IDs to load.</summary>
    [TomlProperty("LoadOrder")]
    public string[]? LoadOrder { get; private set; } = loadOrder;

    /// <summary>Merges install and per-user module options deterministically, preserving install order and policy.</summary>
    internal static ModuleOptions? Merge(ModuleOptions? installOptions, ModuleOptions? userOptions)
    {
        if (installOptions == null && userOptions == null)
        {
            return null;
        }

        if (userOptions == null)
        {
            return installOptions;
        }

        //  Per-user config cannot escalate script compilation above the install policy.
        bool allowScriptCompilation = installOptions?.AllowScriptCompilation ?? false;

        List<string> mergedOrder = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        AppendLoadOrder(mergedOrder, seen, installOptions?.LoadOrder);
        AppendLoadOrder(mergedOrder, seen, userOptions.LoadOrder);

        return new ModuleOptions(allowScriptCompilation, [.. mergedOrder]);
    }

    private static void AppendLoadOrder(List<string> target, HashSet<string> seen, string[]? entries)
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry) || !seen.Add(entry))
            {
                continue;
            }

            target.Add(entry);
        }
    }
}
