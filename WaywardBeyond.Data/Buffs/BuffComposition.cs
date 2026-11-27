using DryIoc;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;

namespace WaywardBeyond.Data.Buffs;

/// <summary>Composition entry for shared headless buff definitions.</summary>
public static class BuffComposition
{
    /// <summary>Registers the buff parser and database. First registration wins.</summary>
    public static void RegisterBuffs(this IContainer container)
    {
        container.RegisterMany<BuffDefinitionsParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.Register<BuffDatabase>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.RegisterMapping<IBuffDatabase, BuffDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.RegisterMapping<IAssetDatabase<BuffDefinition>, BuffDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.RegisterMapping<IAutoActivate, BuffDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
    }
}
