using System;
using DryIoc;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Serialization;

namespace WaywardBeyond.Networking;

/// <summary>
/// The networking project's composition entry. It registers the shared wire catalogue: the component
/// registry, the built-in wire components, and the transport message serializers. Host embeddings call
/// it through <c>HostComposition</c>, so the client and the dedicated server cannot drift.
/// </summary>
public static class NetworkingComposition
{
    private static void Require(Result registration)
    {
        if (!registration.Success)
        {
            throw new InvalidOperationException(registration.Message);
        }
    }

    public static void RegisterNetworking(this IContainer container)
    {
        container.Register<NetworkRegistry>(Reuse.Singleton);

        NetworkRegistry registry = container.Resolve<NetworkRegistry>();
        registry.Initialize([typeof(InputComponent).Assembly]);
        //  Built-in registrations fail fast: a colliding or invalid wire component is fatal at startup.
        Require(registry.Register<TransformComponent>(Uuid.FromValue(2), NetworkDirection.ServerOwned, new TransformCodec()));
        Require(registry.Register<PhysicsComponent>(Uuid.FromValue(3), NetworkDirection.ServerOwned, new PhysicsCodec()));
        Require(registry.Register<IdentifierComponent>(Uuid.FromValue(11), NetworkDirection.ServerOwned, new IdentifierCodec()));

        container.RegisterTransportMessage<WorldSnapshot>();
        container.RegisterTransportMessage<ClientHello>();
        container.RegisterTransportMessage<NewLevelRequest>();
        container.RegisterTransportMessage<NewLevelResponse>();
        container.RegisterTransportMessage<ListLevelsRequest>();
        container.RegisterTransportMessage<ListLevelsResponse>();
        container.RegisterTransportMessage<DeleteLevelRequest>();
        container.RegisterTransportMessage<DeleteLevelResponse>();
        container.RegisterTransportMessage<SaveLevelRequest>();
        container.RegisterTransportMessage<SaveLevelResponse>();
        container.RegisterTransportMessage<JoinRequest>();
        container.RegisterTransportMessage<JoinAccept>();
        container.RegisterTransportMessage<LevelEntityAdd>();
        container.RegisterTransportMessage<LevelStreamComplete>();
        container.RegisterTransportMessage<VoxelEditMessage>();
        container.RegisterTransportMessage<LeaveGameRequest>();
        container.RegisterTransportMessage<NotificationMessage>();
        container.RegisterTransportMessage<SkillStateUpdateMessage>();
        container.RegisterTransportMessage<ChatMessage>();
        container.RegisterTransportMessage<ServerHeartbeatMessage>();
        container.RegisterTransportMessage<ClientHeartbeatMessage>();
    }

    /// <summary>
    ///     Registers one nsd message as a transport message. Add a call here when a message rides a
    ///     transport; payload messages (component payloads, join-stream payloads) do not register.
    /// </summary>
    public static void RegisterTransportMessage<T>(this IContainer container)
    {
        container.Register<INetworkSerializer, NsdMessageSerializer<T>>();
    }
}
