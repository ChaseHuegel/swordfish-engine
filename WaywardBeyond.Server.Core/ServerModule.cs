using DryIoc;
using Shoal.DependencyInjection;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Shoal module entry point for the server. Loaded through the standard module discovery path rather
/// than a hard-wired registration from the client, so a dedicated headless host can host the same
/// <see cref="ServerContext"/> unchanged. The shared container means this module resolves the client's
/// <c>ServerConnectionHub</c> (wired to the in-process transport) and NATS-backed <c>KeyValueStore</c>.
/// </summary>
public sealed class ServerModule : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.Register<SessionManager>(Reuse.Singleton);
        ServerComposition.Register(container);
    }
}