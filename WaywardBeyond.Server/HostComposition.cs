using DryIoc;
using Shoal.Extensions.Swordfish;
using Swordfish.Library.Serialization.Toml.Mappers;
using Swordfish.Settings;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Discovery;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Server;

/// <summary>
/// The shared host composition: everything a hosting embedding (the client's in-process host or the
/// headless dedicated server) registers to run the authoritative server. The server module calls
/// <see cref="RegisterHost"/> exactly once in both embeddings, so the wire-up cannot drift.
/// Embedding-specific choices stay explicit: the client module supplies its own interaction content;
/// the dedicated server uses the headless defaults.
/// </summary>
public static class HostComposition
{
    /// <summary>Registers the host wire-up shared by every embedding.</summary>
    public static void RegisterHost(this IContainer container)
    {
        container.RegisterNetworking();
        container.RegisterNetworkingConfig();

        container.Register<LoopbackRpcServerTransport>(Reuse.Singleton);

        //  Physics config and its vector mapper are engine-owned. The client host registers both from
        //  EngineContainer, so re-registration keeps that config and skips the mapper it already added.
        container.RegisterConfig<PhysicsSettings>(file: "physics.toml");
        if (!container.IsRegistered<Vector3DataBindingTomlMapper>())
        {
            container.RegisterTomlMapper<Vector3DataBindingTomlMapper>();
        }

        container.RegisterConfig<StorageConfig>(file: "storage.toml");

        container.Register<StoragePaths>(Reuse.Singleton);

        container.Register<LanHostInfo>(Reuse.Singleton);
    }
}