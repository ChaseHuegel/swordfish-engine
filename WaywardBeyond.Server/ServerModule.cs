using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Modularity;

namespace WaywardBeyond.Server;

/// <summary>
/// Shoal module entry point for the server. Loaded through the standard module discovery path rather
/// than a hard-wired registration from the client. It registers the shared host wire-up, the
/// authoritative server composition, the <see cref="ServerWorldHost"/> entry point, and a
/// <see cref="LanHost"/> LAN listener. A dedicated server loads this module without the client module,
/// so it never starts a window, input, or client-world services.
/// </summary>
public sealed class ServerModule : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterHost();
        container.RegisterServer();

        container.Register<ServerWorldHost>(Reuse.Singleton);
        container.RegisterMapping<IEntryPoint, ServerWorldHost>();
        container.RegisterMapping<IEntryPoint, ServerRpcHost>();
        container.Register<LanHost>(Reuse.Singleton);
        container.RegisterMapping<IEntryPoint, LanHost>();
    }
}
