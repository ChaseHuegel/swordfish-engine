using DryIoc;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;

namespace WaywardBeyond.Data.Buffs;

/// <summary>Composition entry for shared headless buff definitions.</summary>
public static class BuffComposition
{
    /// <summary>Registers the buff TOML parser and database. First registration wins.</summary>
    public static void RegisterBuffs(this IContainer container)
    {
        if (!container.IsRegistered<TomlParser<BuffDefinitions>>())
        {
            container.RegisterMany<TomlParser<BuffDefinitions>>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        }

        if (container.IsRegistered<VirtualFileSystem>() && container.IsRegistered<IFileParseService>())
        {
            if (!container.IsRegistered<BuffDatabase>())
            {
                container.Register<BuffDatabase>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
                container.RegisterMapping<IBuffDatabase, BuffDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
                container.RegisterMapping<IAssetDatabase<Buff>, BuffDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
                container.RegisterMapping<IAutoActivate, BuffDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
            }
        }
    }
}
