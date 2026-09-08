using DryIoc;
using Shoal.CommandLine;
using Shoal.DependencyInjection;
using WaywardBeyond.Shared.Config;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Shoal module entry point for the server. Loaded through the standard module discovery path rather
/// than a hard-wired registration from the client. In <see cref="NetworkMode.Host"/> (the default, e.g.
/// singleplayer) it registers the authoritative in-process <see cref="ServerContext"/> and opens a
/// <see cref="LanHost"/> LAN listener. In <see cref="NetworkMode.Client"/> it registers neither, so a
/// pure client never spins up an in-process server and simply joins a remote host over a socket.
/// </summary>
public sealed class ServerModule : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.Register<SessionManager>(Reuse.Singleton);

        NetworkMode mode = NetworkModeResolver.Resolve(container.Resolve<CommandLineArgs>());
        if (mode == NetworkMode.Client)
        {
            return;
        }

        ServerComposition.Register(container);
        container.RegisterMany<LanHost>(Reuse.Singleton);
    }
}