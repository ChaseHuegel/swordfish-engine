using DryIoc;
using Shoal.DependencyInjection;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Systems;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once UnusedType.Global
public class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterMany<Entry>();
        container.Register<IEntitySystem, PlayerControllerSystem>();
        container.Register<IEntitySystem, PlayerInteractionSystem>();
        container.Register<IEntitySystem, FirstPersonCameraSystem>();
    }
}