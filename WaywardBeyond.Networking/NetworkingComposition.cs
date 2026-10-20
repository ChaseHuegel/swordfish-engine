using System;
using DryIoc;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Registry;

namespace WaywardBeyond.Networking;

/// <summary>
/// The networking project's composition entry. It registers the shared component registry and the
/// built-in wire components. Host embeddings call it through <c>HostComposition</c>, so the client and
/// the dedicated server cannot drift.
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
    }
}
