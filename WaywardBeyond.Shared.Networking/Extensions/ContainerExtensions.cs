using DryIoc;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Networking.Extensions;

public static class ContainerExtensions
{
    public static void RegisterNetworkComponent<T>(this IContainer container)
        where T : struct, IDataComponent
    {
        NetworkRegistry.Register<T>();
    }
}
