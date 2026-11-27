using DryIoc;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Data.Blueprints;

/// <summary>Composition entry for shared headless bundled blueprint loading.</summary>
public static class BlueprintComposition
{
    /// <summary>Registers the <c>.wbp</c> parser and bundled blueprint database. First registration wins.</summary>
    public static void RegisterBundledBlueprints(this IContainer container)
    {
        container.RegisterMany<BlueprintDataParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.Register<BundledBlueprintDatabase>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.RegisterMapping<IBundledBlueprintDatabase, BundledBlueprintDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.RegisterMapping<IAssetDatabase<BlueprintData>, BundledBlueprintDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.RegisterMapping<IAutoActivate, BundledBlueprintDatabase>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
    }
}
