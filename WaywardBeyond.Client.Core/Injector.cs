using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Player;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once UnusedType.Global
public class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        RegisterUI(container);
        RegisterInput(container);
        RegisterDatabases(container);
        RegisterTomlParsers(container);
        RegisterEntitySystems(container);
        
        container.Register<PlayerData>(Reuse.Singleton);
        
        //  TODO this was thrown together for testing and needs cleaned up
        container.Register<BrickEntityBuilder>(Reuse.Singleton);
        container.RegisterDelegate<Shader>(context => context.Resolve<IFileParseService>().Parse<Shader>(AssetPaths.Shaders.At("lightedArray.glsl")), Reuse.Singleton);
        container.RegisterDelegate<TextureArray>(context => context.Resolve<IFileParseService>().Parse<TextureArray>(AssetPaths.Textures.At("block\\")), Reuse.Singleton);
        container.RegisterDelegate<DataStore>(context => context.Resolve<IECSContext>().World.DataStore, Reuse.Singleton);
        
        container.RegisterMany<Entry>();
    }

    private static void RegisterUI(IContainer container)
    {
        container.Register<DefaultUIRenderer>(Reuse.Singleton);
        container.RegisterMapping<IAutoActivate, DefaultUIRenderer>();
        
        container.Register<DebugOverlayRenderer>(Reuse.Singleton);
        container.RegisterMapping<IAutoActivate, DebugOverlayRenderer>();
        
        container.Register<Hotbar>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, Hotbar>();
        
        container.Register<ShapeSelector>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, ShapeSelector>();
        
        container.Register<OrientationSelector>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, OrientationSelector>();
        
        container.Register<ControlHints>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, ControlHints>();
        
        container.Register<CrosshairOverlay>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, CrosshairOverlay>();
    }
    
    private static void RegisterInput(IContainer container)
    {
        container.Register<IEntryPoint, PlayerInteractionService>();
        
        container.Register<PlayerControllerSystem>(Reuse.Singleton);
        container.RegisterMapping<IEntitySystem, PlayerControllerSystem>();
        container.Register<IEntitySystem, FirstPersonCameraSystem>();
    }
    
    private static void RegisterDatabases(IContainer container)
    {
        container.Register<ItemDatabase>(Reuse.Singleton);
        container.RegisterMapping<IAssetDatabase<Item>, ItemDatabase>();
        container.Register<BrickDatabase>(Reuse.Singleton);
        container.RegisterMapping<IAssetDatabase<BrickInfo>, BrickDatabase>();
    }
    
    private static void RegisterTomlParsers(IContainer container)
    {
        container.RegisterMany<TomlParser<BrickDefinitions>>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterMany<TomlParser<ItemDefinitions>>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
    }

    private static void RegisterEntitySystems(IContainer container)
    {
        container.Register<IEntitySystem, PlayerViewModelSystem>();
        container.Register<IEntitySystem, CleanupMeshRendererSystem>();
        container.Register<IEntitySystem, ThrusterSystem>();
    }
}