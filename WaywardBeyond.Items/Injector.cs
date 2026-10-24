using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Extensions.Swordfish;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Items;

/// <summary>Module injector for Wayward Beyond item API and assets.</summary>
// ReSharper disable once UnusedType.Global
public sealed class Injector : IDryIocInjector
{
    /// <inheritdoc/>
    public void Inject(IContainer container)
    {
        container.RegisterTomlParser<ItemDefinitions>();
        container.Register<ItemDatabase>(Reuse.Singleton);
        container.RegisterMapping<IItemDatabase, ItemDatabase>();
        container.RegisterMapping<IAssetDatabase<Item>, ItemDatabase>();
    }
}
