using DryIoc;
using Shoal.Extensions.Swordfish;

namespace WaywardBeyond.Config;

/// <summary>
/// The config project's composition entry. It registers shared config files and their sub-config
/// views so every embedding resolves the same objects from the same owner.
/// </summary>
public static class ConfigComposition
{
    /// <summary>
    ///     Registers <c>network.toml</c> and the sub-config views projected from it. First registration
    ///     wins, so a host embedding can call this before or after other modules without duplicating
    ///     services.
    /// </summary>
    public static void RegisterNetworkingConfig(this IContainer container)
    {
        container.RegisterConfig<NetworkingConfig>(file: "network.toml");
        container.Resolve<NetworkingConfig>().Validate();

        container.RegisterDelegate<DiscoveryConfig>(context => context.Resolve<NetworkingConfig>().Discovery, Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.RegisterDelegate<ProtocolConfig>(context => context.Resolve<NetworkingConfig>().Protocol, Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.RegisterDelegate<ServerConfig>(context => context.Resolve<NetworkingConfig>().Server, Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.RegisterDelegate<TransportConfig>(context => context.Resolve<NetworkingConfig>().Transport, Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
    }
}
