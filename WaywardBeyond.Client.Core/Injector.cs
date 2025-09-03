using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once UnusedType.Global
public class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterMany<Entry>();
        container.Register<IEntryPoint, PlayerInteractionSystem>();
        
        container.Register<Hotbar>(Reuse.Singleton);
        container.RegisterMapping<IEntitySystem, Hotbar>();
        
        container.Register<IEntitySystem, PlayerControllerSystem>();
        container.Register<IEntitySystem, FirstPersonCameraSystem>();
        container.Register<IEntitySystem, CleanupMeshRendererSystem>();
        container.Register<IEntitySystem, ThrusterSystem>();
        
        container.Register<BrickEntityBuilder>(Reuse.Singleton);
        container.RegisterDelegate<Shader>(context => context.Resolve<IFileParseService>().Parse<Shader>(AssetPaths.Shaders.At("lightedArray.glsl")), Reuse.Singleton);
        container.RegisterDelegate<TextureArray>(context => context.Resolve<IFileParseService>().Parse<TextureArray>(AssetPaths.Textures.At("block\\")), Reuse.Singleton);
        container.RegisterDelegate<DataStore>(context => context.Resolve<IECSContext>().World.DataStore, Reuse.Singleton);
    }
}