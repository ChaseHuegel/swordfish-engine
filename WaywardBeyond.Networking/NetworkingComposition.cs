using System;
using DryIoc;
using Krill;
using Swordfish.ECS;
using Swordfish.Graphics.Particles;
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
        Require(registry.Register<InventoryComponent>(
            Uuid.FromValue(13),
            NetworkDirection.ServerOwned,
            new InventoryCodec(),
            ReplicationAudience.ViewersOnly
        ));
        Require(registry.Register<GameModeComponent>(Uuid.FromValue(14), NetworkDirection.ServerOwned, new NsdComponentCodec<GameModeComponent>()));
        Require(registry.Register<InteractionEvent>(Uuid.FromValue(15), NetworkDirection.ClientOwned, new NsdComponentCodec<InteractionEvent>()));
        Require(registry.Register<InventoryEvent>(Uuid.FromValue(16), NetworkDirection.ClientOwned, new NsdComponentCodec<InventoryEvent>()));

        Require(registry.Register<LaserBeamComponent>(
            Uuid.FromValue(17),
            NetworkDirection.ServerOwned,
            new NsdComponentCodec<LaserBeamComponent>(),
            ReplicationAudience.OthersOnly
        ));
        Require(registry.Register<ParticleEmitterComponent>(Uuid.FromValue(18), NetworkDirection.ServerOwned, new ParticleEmitterCodec()));
        Require(registry.Register<ParticleEmitterBindingComponent>(Uuid.FromValue(19), NetworkDirection.ServerOwned, new NsdComponentCodec<ParticleEmitterBindingComponent>()));
        Require(registry.Register<StatisticsComponent>(
            Uuid.FromValue(20),
            NetworkDirection.ServerOwned,
            new NsdComponentCodec<StatisticsComponent>(),
            ReplicationAudience.OwnerOnly
        ));
        Require(registry.Register<MachineStateComponent>(
            Uuid.FromValue(21),
            NetworkDirection.ServerOwned,
            new NsdComponentCodec<MachineStateComponent>(),
            ReplicationAudience.ViewersOnly
        ));

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
