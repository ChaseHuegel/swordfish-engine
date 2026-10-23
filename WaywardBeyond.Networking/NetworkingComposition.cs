using System;
using DryIoc;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Registry;

namespace WaywardBeyond.Networking;

/// <summary>Registers the component registry and the built-in wire components.</summary>
public static class NetworkingComposition
{
    /// <summary>Registers the networking services on <paramref name="container"/>.</summary>
    public static void RegisterNetworking(this IContainer container)
    {
        container.Register<NetworkRegistry>(Reuse.Singleton);

        NetworkRegistry registry = container.Resolve<NetworkRegistry>();

        Require(registry.Register<InputComponent>(Uuid.FromValue(1), NetworkDirection.ClientOwned, new NsdComponentCodec<InputComponent>()));
        Require(registry.Register<TransformComponent>(Uuid.FromValue(2), NetworkDirection.ServerOwned, new TransformCodec()));
        Require(registry.Register<PhysicsComponent>(Uuid.FromValue(3), NetworkDirection.ServerOwned, new PhysicsCodec()));
        Require(registry.Register<BodyViewComponent>(Uuid.FromValue(10), NetworkDirection.ServerOwned, new NsdComponentCodec<BodyViewComponent>()));
        Require(registry.Register<IdentifierComponent>(Uuid.FromValue(11), NetworkDirection.ServerOwned, new IdentifierCodec()));
        Require(registry.Register<EquipmentComponent>(Uuid.FromValue(12), NetworkDirection.ClientOwned, new NsdComponentCodec<EquipmentComponent>()));
        Require(registry.Register<InventoryComponent>(Uuid.FromValue(13), NetworkDirection.ServerOwned, new NsdComponentCodec<InventoryComponent>()));
        Require(registry.Register<GameModeComponent>(Uuid.FromValue(14), NetworkDirection.ServerOwned, new NsdComponentCodec<GameModeComponent>()));
        Require(registry.Register<InteractionEvent>(Uuid.FromValue(15), NetworkDirection.ClientOwned, new NsdComponentCodec<InteractionEvent>()));
        Require(registry.Register<InventoryEvent>(Uuid.FromValue(16), NetworkDirection.ClientOwned, new NsdComponentCodec<InventoryEvent>()));

        registry.Freeze();
    }

    private static void Require(Result registration)
    {
        if (!registration.Success)
        {
            throw new InvalidOperationException(registration.Message);
        }
    }
}
