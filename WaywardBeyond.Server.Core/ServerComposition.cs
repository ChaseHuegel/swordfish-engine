using System;
using System.Threading;
using DryIoc;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;

namespace WaywardBeyond.Server.Core;

/// <summary>Registers the authoritative server host into the application container.</summary>
public static class ServerComposition
{
    public static void Register(IContainer container)
    {
        //  The server resolves the NATS-backed KeyValueStore lazily, only when it first loads a level, by
        //  which point the client's PersistentNatsProcess has started. Injecting it eagerly into
        //  ServerContext would race the NATS lifecycle, whose boot order vs the server entry point is
        //  unspecified. It is resolved exactly once and reused: KeyValueStore wraps a live NatsClient, so
        //  re-resolving it on every operation would spin up a new NATS connection per tick and, on the
        //  hot server thread, repeatedly dispatch through DryIoc's interface resolution (which faults on
        //  repeat IConfiguration dispatch). Memoizing keeps a single, stable instance for the session.
        container.RegisterDelegate<Func<KeyValueStore>>(context =>
        {
            var lazy = new Lazy<KeyValueStore>(
                () => context.Resolve<KeyValueStore>(),
                LazyThreadSafetyMode.ExecutionAndPublication
            );
            return () => lazy.Value;
        });

        container.RegisterMany<ServerContext>(Reuse.Singleton);

        //  Server-side interaction mod hooks: a single shared registry that server mods register their
        //  interaction handlers into, resolved by the authoritative ServerInteractionSystem.
        container.Register<IInteractionHandlerRegistry, InteractionHandlerRegistry>(Reuse.Singleton);
    }
}