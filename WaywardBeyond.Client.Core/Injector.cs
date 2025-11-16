using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Extensions.Swordfish;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Bricks.Decorators;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Graphics;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Player;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Client.Core.UI.Layers;
using WaywardBeyond.Client.Core.UI.Layers.Menu;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Client.Core.Voxels.Processing;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once UnusedType.Global
public class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        RegisterConfiguration(container);
        RegisterUI(container);
        RegisterInput(container);
        RegisterDatabases(container);
        RegisterParsers(container);
        RegisterEntitySystems(container);
        RegisterBrickDecorators(container);
        RegisterVoxels(container);
        
        container.Register<PlayerData>(Reuse.Singleton);
        
        container.Register<GameSaveService>(Reuse.Singleton);
        container.Register<GameSaveManager>(Reuse.Singleton);
        container.RegisterMapping<IAutoActivate, GameSaveManager>();
        
        container.Register<Entry>(Reuse.Singleton);
        container.RegisterMapping<IAutoActivate, Entry>();
    }

    private void RegisterConfiguration(IContainer container)
    {
        container.Register<SettingsManager>(Reuse.Singleton);
        container.RegisterMapping<IAutoActivate, SettingsManager>();
        
        container.RegisterConfig<ControlSettings>(file: "control.toml");
    }

    private static void RegisterUI(IContainer container)
    {
        container.Register<DefaultUIRenderer>(Reuse.Singleton);
        container.RegisterMapping<IAutoActivate, DefaultUIRenderer>();
        
        container.Register<IUILayer, CrosshairOverlay>(Reuse.Singleton);
        
        container.Register<DebugOverlayRenderer>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, DebugOverlayRenderer>();
        
        container.Register<MainMenu>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, MainMenu>();
        container.Register<IMenuPage<MenuPage>, HomePage>();
        container.Register<IMenuPage<MenuPage>, SettingsPage>();
        container.Register<IMenuPage<MenuPage>, SingleplayerPage>();
        
        container.Register<IUILayer, LoadScreen>(Reuse.Singleton);
        container.Register<IUILayer, VersionWatermark>(Reuse.Singleton);
        
        container.Register<Hotbar>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, Hotbar>();
        
        container.Register<ShapeSelector>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, ShapeSelector>();
        
        container.Register<OrientationSelector>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, OrientationSelector>();
        
        container.Register<ControlHints>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, ControlHints>();
        
        container.Register<NotificationService>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, NotificationService>();
    }
    
    private static void RegisterInput(IContainer container)
    {
        container.Register<PlayerInteractionService>(Reuse.Singleton);
        container.RegisterMapping<IEntryPoint, PlayerInteractionService>();
        container.RegisterMapping<IDebugOverlay, PlayerInteractionService>();

        container.Register<PlayerControllerSystem>(Reuse.Singleton);
        container.RegisterMapping<IEntitySystem, PlayerControllerSystem>();
        container.Register<IEntitySystem, FirstPersonCameraSystem>();
    }
    
    private static void RegisterDatabases(IContainer container)
    {
        container.Register<ItemDatabase>(Reuse.Singleton);
        container.RegisterMapping<IAssetDatabase<Item>, ItemDatabase>();
        container.Register<BrickDatabase>(Reuse.Singleton);
        container.RegisterMapping<IBrickDatabase, BrickDatabase>();
        container.RegisterMapping<IAssetDatabase<BrickInfo>, BrickDatabase>();
    }
    
    private static void RegisterParsers(IContainer container)
    {
        container.RegisterTomlParser<BrickDefinitions>();
        container.RegisterTomlParser<ItemDefinitions>();
        
        container.RegisterMany<PBRTextureArraysParser>(reuse: Reuse.Singleton);
    }

    private static void RegisterEntitySystems(IContainer container)
    {
        container.Register<IEntitySystem, PlayerViewModelSystem>();
        container.Register<IEntitySystem, CleanupMeshRendererSystem>();
        container.Register<IEntitySystem, ThrusterSystem>();
    }
    
    private void RegisterBrickDecorators(IContainer container)
    {
        container.Register<BrickGridService>();
        container.Register<IBrickDecorator, LightDecorator>();
        container.Register<IBrickDecorator, ThrusterDecorator>();
    }
    
    private void RegisterVoxels(IContainer container)
    {
        //  TODO this was thrown together for testing and needs cleaned up
        container.Register<BrickEntityBuilder>(Reuse.Singleton);
        container.RegisterDelegate<Shader>(context => context.Resolve<IFileParseService>().Parse<Shader>(AssetPaths.Shaders.At("lightedArray.glsl")), Reuse.Singleton);
        container.RegisterDelegate<TextureArray>(context => context.Resolve<IFileParseService>().Parse<TextureArray>(AssetPaths.Textures.At("block\\")), Reuse.Singleton);
        container.RegisterDelegate<PBRTextureArrays>(context => context.Resolve<IFileParseService>().Parse<PBRTextureArrays>(AssetPaths.Textures.At("block\\")), Reuse.Singleton);
        container.RegisterDelegate<DataStore>(context => context.Resolve<IECSContext>().World.DataStore, Reuse.Singleton);
        
        container.Register<VoxelObjectBuilder>(Reuse.Singleton);

        container.Register<DepthState>(Reuse.Scoped);
        container.Register<LightingState>(Reuse.Scoped);
        container.Register<MeshState>(Reuse.Scoped);
        container.Register<CollisionState>(Reuse.Scoped);
        
        container.Register<VoxelObjectProcessor.IPass, AmbientLightPass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.IPass, LightPropagationPass>(Reuse.Scoped);
        
        container.Register<VoxelObjectProcessor.IVoxelPass, LightSeedPrePass>(Reuse.Scoped);
        
        container.Register<VoxelObjectProcessor.ISamplePass, DepthPrePass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.ISamplePass, LightPropagationPrePass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.ISamplePass, MeshPostPass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.ISamplePass, CollisionPostPass>(Reuse.Scoped);
        
        container.Register<VoxelObjectProcessor>(Reuse.Scoped);
    }
}