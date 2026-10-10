using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Extensions.Swordfish;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Bodies;

/// <summary>Module injector for Wayward Beyond body assets.</summary>
// ReSharper disable once UnusedType.Global
public sealed class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterTomlParser<BodyDefinitions>();
        container.Register<BodyDatabase>(Reuse.Singleton);
        container.RegisterMapping<IAssetDatabase<BodyInfo>, BodyDatabase>();
        container.RegisterMapping<IBodyDatabase, BodyDatabase>();
    }
}