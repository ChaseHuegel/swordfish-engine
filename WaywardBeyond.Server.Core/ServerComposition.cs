using DryIoc;
using WaywardBeyond.Shared.Gameplay;

namespace WaywardBeyond.Server.Core;

/// <summary>Registers the authoritative server host into the application container.</summary>
public static class ServerComposition
{
    public static void Register(IContainer container)
    {
        //  The NATS-backed KeyValueStore and its lazy resolution live in the shared host wire-up
        //  (HostComposition.RegisterNetworking), so every hosting embedding shares the memoization.

        container.RegisterMany<ServerContext>(Reuse.Singleton);

        //  Server-side interaction mod hooks: a single shared registry that server mods register their
        //  interaction handlers into, resolved by the authoritative ServerInteractionSystem.
        container.Register<IInteractionHandlerRegistry, InteractionHandlerRegistry>(Reuse.Singleton);
    }
}