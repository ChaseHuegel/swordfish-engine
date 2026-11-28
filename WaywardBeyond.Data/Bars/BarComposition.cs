using DryIoc;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;

namespace WaywardBeyond.Data.Bars;

/// <summary>Composition entry for shared headless bar definitions.</summary>
public static class BarComposition
{
    /// <summary>Registers the bar TOML parser and database. First registration wins.</summary>
    public static void RegisterBars(this IContainer container)
    {
        container.RegisterMany<TomlParser<BarDefinitions>>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        if (container.IsRegistered<VirtualFileSystem>() && container.IsRegistered<IFileParseService>())
        {
            container.Register<BarDatabase>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
            container.RegisterMapping<IBarDatabase, BarDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
            container.RegisterMapping<IAssetDatabase<Bar>, BarDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
            container.RegisterMapping<IAutoActivate, BarDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        }
    }
}
