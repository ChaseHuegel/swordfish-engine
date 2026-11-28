using DryIoc;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;

namespace WaywardBeyond.Data.HudBars;

/// <summary>Composition entry for shared headless HUD bar definitions.</summary>
public static class HudBarComposition
{
    /// <summary>Registers the HUD bar TOML parser and database. First registration wins.</summary>
    public static void RegisterHudBars(this IContainer container)
    {
        if (!container.IsRegistered<TomlParser<HudBarDefinitions>>())
        {
            container.RegisterMany<TomlParser<HudBarDefinitions>>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        }

        if (container.IsRegistered<VirtualFileSystem>() && container.IsRegistered<IFileParseService>())
        {
            if (!container.IsRegistered<HudBarDatabase>())
            {
                container.Register<HudBarDatabase>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
                container.RegisterMapping<IHudBarDatabase, HudBarDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
                container.RegisterMapping<IAssetDatabase<HudBar>, HudBarDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
                container.RegisterMapping<IAutoActivate, HudBarDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
            }
        }
    }
}
