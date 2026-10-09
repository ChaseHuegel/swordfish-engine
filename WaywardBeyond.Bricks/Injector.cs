using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Extensions.Swordfish;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Bricks;

/// <summary>
/// Shoal module entry point for the shared brick definitions. Registers the headless brick database so
/// the client, the server, and headless consumers all resolve brick definitions, brick ids, and the
/// brick palette from one module without a render-coupled asset pipeline.
/// </summary>
public sealed class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterTomlParser<BrickDefinitions>();
        container.Register<BrickDatabase>(Reuse.Singleton);
        container.RegisterMapping<IBrickIdMap, BrickDatabase>();
        container.RegisterMapping<IBrickDatabase, BrickDatabase>();
        container.RegisterMapping<IAssetDatabase<BrickInfo>, BrickDatabase>();
    }
}