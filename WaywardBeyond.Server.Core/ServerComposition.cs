using DryIoc;

namespace WaywardBeyond.Server.Core;

/// <summary>Registers the authoritative server host into the application container.</summary>
public static class ServerComposition
{
    public static void Register(IContainer container)
    {
        container.RegisterMany<ServerContext>(Reuse.Singleton);
    }
}