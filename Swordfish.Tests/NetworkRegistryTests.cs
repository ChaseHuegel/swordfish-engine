using System;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Networking.Registry;
using Xunit;

namespace Swordfish.Tests;

public class NetworkRegistryTests
{
    private struct ComponentA : IDataComponent;
    private struct ComponentB : IDataComponent;
    private struct ComponentC : IDataComponent;
    private struct ComponentD : IDataComponent;
    private struct ComponentE : IDataComponent;

    private sealed class FakeCodec<T> : IPayloadCodec where T : struct, IDataComponent
    {
        public Type ComponentType => typeof(T);
        public byte[] Serialize(DataStore store, int entity) => [];
        public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload) { }
    }

    [Fact]
    public void DuplicateRegistrationReportsTheReason()
    {
        var codec = new FakeCodec<ComponentE>();
        Result first = NetworkRegistry.Register<ComponentE>(Uuid.FromValue(0x1011), NetworkDirection.ClientOwned, codec);
        Assert.True(first.Success, first.Message);

        Result duplicate = NetworkRegistry.Register<ComponentE>(Uuid.FromValue(0x1012), NetworkDirection.ServerOwned, codec);
        Assert.False(duplicate.Success);
        Assert.Contains("already registered", duplicate.Message);

        Result duplicateUuid = NetworkRegistry.Register<ComponentB>(Uuid.FromValue(0x1011), NetworkDirection.ClientOwned, codec);
        Assert.False(duplicateUuid.Success);
        Assert.Contains("uuid", duplicateUuid.Message);

        Result nullUuid = NetworkRegistry.Register<ComponentB>(Uuid.Null, NetworkDirection.ClientOwned, codec);
        Assert.False(nullUuid.Success);
        Assert.Contains("Null", nullUuid.Message);
    }

    [Fact]
    public void NonNsdComponentFailsAtRegistration()
    {
        //  A component registered with NsdComponentCodec<T> but without generated nsd methods must fail
        //  at registration, not on first use: the codec constructor itself validates and throws.
        Assert.ThrowsAny<Exception>(() => NetworkRegistry.Register<ComponentA>(
            Uuid.FromValue(0x100A),
            NetworkDirection.ServerOwned,
            new NsdComponentCodec<ComponentA>()
        ));
    }

    [Fact]
    public void RegisterAndLookupByTypeAndUuid()
    {
        var codec = new FakeCodec<ComponentA>();
        Assert.True(NetworkRegistry.Register<ComponentA>(Uuid.FromValue(0x1001), NetworkDirection.ClientOwned, codec));

        Assert.True(NetworkRegistry.TryGetInfo(typeof(ComponentA), out NetworkComponentInfo byType));
        Assert.Equal(Uuid.FromValue(0x1001), byType.Uuid);
        Assert.Equal(NetworkDirection.ClientOwned, byType.Direction);
        Assert.Equal(typeof(ComponentA), byType.Type);
        Assert.Same(codec, byType.Codec);

        Assert.True(NetworkRegistry.TryGetInfo(Uuid.FromValue(0x1001), out NetworkComponentInfo byUuid));
        Assert.Equal(typeof(ComponentA), byUuid.Type);
        Assert.Same(codec, byUuid.Codec);
    }

    [Fact]
    public void RegisterRejectsDuplicatesAndNullUuid()
    {
        var codec = new FakeCodec<ComponentB>();
        Assert.True(NetworkRegistry.Register<ComponentB>(Uuid.FromValue(0x1002), NetworkDirection.ServerOwned, codec));

        Assert.False(NetworkRegistry.Register<ComponentB>(Uuid.FromValue(0x1003), NetworkDirection.ClientOwned, codec));
        Assert.False(NetworkRegistry.Register<ComponentB>(Uuid.Null, NetworkDirection.ServerOwned, codec));
    }

    [Fact]
    public void GetComponentsFiltersByDirection()
    {
        Assert.True(NetworkRegistry.Register<ComponentC>(Uuid.FromValue(0x1004), NetworkDirection.ClientOwned, new FakeCodec<ComponentC>()));
        Assert.True(NetworkRegistry.Register<ComponentD>(Uuid.FromValue(0x1005), NetworkDirection.ServerOwned, new FakeCodec<ComponentD>()));

        System.Collections.Generic.List<NetworkComponentInfo> clientOwned = new(NetworkRegistry.GetComponents(NetworkDirection.ClientOwned));
        System.Collections.Generic.List<NetworkComponentInfo> serverOwned = new(NetworkRegistry.GetComponents(NetworkDirection.ServerOwned));

        Assert.Single(clientOwned, info => info.Type == typeof(ComponentC));
        Assert.DoesNotContain(clientOwned, info => info.Type == typeof(ComponentD));
        Assert.Single(serverOwned, info => info.Type == typeof(ComponentD));
        Assert.DoesNotContain(serverOwned, info => info.Type == typeof(ComponentC));
    }
}