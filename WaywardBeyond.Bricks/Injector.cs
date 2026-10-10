using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Extensions.Swordfish;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Bricks;

/// <summary>Module injector for Wayward Beyond brick API and assets.</summary>
// ReSharper disable once UnusedType.Global
public sealed class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterTomlParser<BrickDefinitions>();
        container.Register<BrickDatabase>(Reuse.Singleton);
        container.RegisterMapping<IBrickRegistry, BrickDatabase>();
        container.RegisterMapping<IBrickDatabase, BrickDatabase>();
        container.RegisterMapping<IAssetDatabase<Brick>, BrickDatabase>();
    }
}