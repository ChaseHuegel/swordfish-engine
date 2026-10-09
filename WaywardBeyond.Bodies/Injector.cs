using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Extensions.Swordfish;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Bodies;

/// <summary>
/// Shoal module entry point for the shared body definitions. Registers the headless body database so the
/// client, the server, and headless consumers all resolve body models and their state/direction texture
/// sets from one module without a render-coupled asset pipeline.
/// </summary>
public sealed class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterTomlParser<BodyModels>();
        container.Register<BodyDatabase>(Reuse.Singleton);
        container.RegisterMapping<IAssetDatabase<BodyInfo>, BodyDatabase>();
        container.RegisterMapping<IBodyDatabase, BodyDatabase>();
    }
}