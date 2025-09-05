using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once UnusedType.Global
public class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterMany<Entry>();

        RegisterUI(container);
        RegisterInput(container);
        RegisterTomlParsers(container);
        RegisterEntitySystems(container);
        
        //  TODO this was thrown together for testing and needs cleaned up
        container.Register<BrickEntityBuilder>(Reuse.Singleton);
        container.RegisterDelegate<Shader>(context => context.Resolve<IFileParseService>().Parse<Shader>(AssetPaths.Shaders.At("lightedArray.glsl")), Reuse.Singleton);
        container.RegisterDelegate<TextureArray>(context => context.Resolve<IFileParseService>().Parse<TextureArray>(AssetPaths.Textures.At("block\\")), Reuse.Singleton);
        container.RegisterDelegate<DataStore>(context => context.Resolve<IECSContext>().World.DataStore, Reuse.Singleton);
    }

    private static void RegisterUI(IContainer container)
    {
        container.Register<Hotbar>(Reuse.Singleton);
        container.RegisterMapping<IEntitySystem, Hotbar>();
    }
    
    private static void RegisterInput(IContainer container)
    {
        container.Register<IEntryPoint, PlayerInteractionService>();
        
        container.Register<IEntitySystem, PlayerControllerSystem>();
        container.Register<IEntitySystem, FirstPersonCameraSystem>();
    }
    
    private static void RegisterTomlParsers(IContainer container)
    {
        container.RegisterMany<TomlParser<BrickDefinitions>>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterMany<TomlParser<ItemDefinitions>>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
    }

    private static void RegisterEntitySystems(IContainer container)
    {
        container.Register<IEntitySystem, CleanupMeshRendererSystem>();
        container.Register<IEntitySystem, ThrusterSystem>();
    }
}