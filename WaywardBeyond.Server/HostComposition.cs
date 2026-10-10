using System;
using DryIoc;
using Shoal.Extensions.Swordfish;
using Swordfish.Library.Util;
using Swordfish.ECS;
using Swordfish.Settings;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Discovery;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Server;

/// <summary>
/// The shared host composition: everything a hosting embedding (the client's in-process host or the
/// headless dedicated launcher) registers to run the authoritative server. Both embeddings call
/// <see cref="RegisterNetworking"/> so the wire-up cannot drift. Embedding-specific choices stay
/// explicit: the client seeds the in-process <c>LocalConnection</c> loopback (<paramref name="seedLocalLoopback"/>),
/// the dedicated launcher does not, and the interaction content implementation is the embedding's own
/// content choice.
/// </summary>
public static class HostComposition
{
    private static void Require(Result registration)
    {
        if (!registration.Success)
        {
            throw new InvalidOperationException(registration.Message);
        }
    }

    public static void RegisterNetworking(IContainer container, bool seedLocalLoopback)
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);
        //  Built-in registrations fail fast: a colliding or invalid wire component is fatal at startup.
        Require(NetworkRegistry.Register<TransformComponent>(Uuid.FromValue(2), NetworkDirection.ServerOwned, new TransformCodec()));
        Require(NetworkRegistry.Register<PhysicsComponent>(Uuid.FromValue(3), NetworkDirection.ServerOwned, new PhysicsCodec()));
        Require(NetworkRegistry.Register<IdentifierComponent>(Uuid.FromValue(11), NetworkDirection.ServerOwned, new IdentifierCodec()));

        container.Register<INetworkSerializer, NsdMessageSerializer<WorldSnapshot>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<ClientHello>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<NewLevelRequest>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<NewLevelResponse>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<ListLevelsRequest>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<ListLevelsResponse>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<DeleteLevelRequest>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<DeleteLevelResponse>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<SaveLevelRequest>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<SaveLevelResponse>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<JoinRequest>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<JoinAccept>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<LevelEntityAdd>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<LevelStreamComplete>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<VoxelEditMessage>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<LeaveGameRequest>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<NotificationMessage>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<SkillStateUpdateMessage>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<ChatMessage>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<ServerHeartbeatMessage>>();
        container.Register<INetworkSerializer, NsdMessageSerializer<ClientHeartbeatMessage>>();

        container.Register<LocalConnection>(Reuse.Singleton);
        container.Register<PendingJoins>(Reuse.Singleton);
        if (seedLocalLoopback)
        {
            //  The in-process host's loopback rides the ordinary join route: it waits in the pending
            //  set until its JoinRequest binds it to the world it joins.
            container.Resolve<PendingJoins>().Add(container.Resolve<LocalConnection>().Server);
        }

        container.RegisterConfig<NetworkingConfig>(file: "network.toml");
        container.RegisterDelegate<DiscoveryConfig>(context => context.Resolve<NetworkingConfig>().Discovery, Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterDelegate<ProtocolConfig>(context => context.Resolve<NetworkingConfig>().Protocol, Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterDelegate<ServerConfig>(context => context.Resolve<NetworkingConfig>().Server, Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterDelegate<TransportConfig>(context => context.Resolve<NetworkingConfig>().Transport, Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        
        container.RegisterConfig<StorageSettings>(file: "storage.toml");

        container.Register<StoragePaths>(Reuse.Singleton);

        container.Register<LanHostInfo>(Reuse.Singleton);
    }
}