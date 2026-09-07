using System;
using DryIoc;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Server.Core;

/// <summary>Registers the authoritative server host into the application container.</summary>
public static class ServerComposition
{
    public static void Register(IContainer container)
    {
        //  The server resolves the NATS-backed KeyValueStore lazily, only when it first loads a level, by
        //  which point the client's PersistentNatsProcess has started. Injecting it eagerly into
        //  ServerContext would race the NATS lifecycle, whose boot order vs the server entry point is
        //  unspecified.
        container.RegisterDelegate<Func<KeyValueStore>>(context => () => context.Resolve<KeyValueStore>());

        container.RegisterMany<ServerContext>(Reuse.Singleton);
    }
}