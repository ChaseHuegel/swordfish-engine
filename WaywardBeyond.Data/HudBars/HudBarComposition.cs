using DryIoc;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;

namespace WaywardBeyond.Data.HudBars;

/// <summary>Composition entry for shared headless HUD bar definitions.</summary>
public static class HudBarComposition
{
    /// <summary>Registers the HUD bar parser and database. First registration wins.</summary>
    public static void RegisterHudBars(this IContainer container)
    {
        if (!container.IsRegistered<HudBarDefinitionsParser>())
        {
            container.RegisterMany<HudBarDefinitionsParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        }

        if (container.IsRegistered<VirtualFileSystem>() && container.IsRegistered<IFileParseService>())
        {
            if (!container.IsRegistered<HudBarDatabase>())
            {
                container.Register<HudBarDatabase>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
                container.RegisterMapping<IHudBarDatabase, HudBarDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
                container.RegisterMapping<IAssetDatabase<HudBarDefinition>, HudBarDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
                container.RegisterMapping<IAutoActivate, HudBarDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
            }
        }
    }
}
