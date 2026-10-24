using Swordfish.ECS;
using Swordfish.Library.Util;
using Xunit;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Registry;

namespace Swordfish.Tests;

public class NetworkComponentCodecTests
{
    private static NetworkRegistry CreateRegistry()
    {
        var registry = new NetworkRegistry();
        Assert.True(registry.Register<InputComponent>(Uuid.FromValue(1), NetworkDirection.ClientOwned, new NsdComponentCodec<InputComponent>()).Success);
        Assert.True(registry.Register<BodyViewComponent>(Uuid.FromValue(10), NetworkDirection.ServerOwned, new NsdComponentCodec<BodyViewComponent>()).Success);
        Assert.True(registry.Register<InteractionEvent>(Uuid.FromValue(15), NetworkDirection.ClientOwned, new NsdComponentCodec<InteractionEvent>()).Success);
        registry.Freeze();
        return registry;
    }

    [Fact]
    public void NsdComponentCodecRoundTripsInputComponent()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        var input = new InputComponent
        {
            MovementX = 1f,
            MovementY = 2f,
            MovementZ = 3f,
            LookPitch = 1.1f,
            LookYaw = 2.2f,
            LookRoll = 3.3f,
            SequenceNumber = 9,
            ServerTickAtSample = 7,
        };
        store.AddOrUpdate(entity, input);

        var codec = new NsdComponentCodec<InputComponent>();
        Result<byte[]> serialized = codec.Serialize(store, entity);

        Assert.True(serialized.Success);
        Assert.NotEmpty(serialized.Value);

        var output = new DataStore();
        int outputEntity = output.Alloc();
        Assert.True(codec.Apply(output, outputEntity, serialized.Value).Success);

        Assert.True(output.TryGet(outputEntity, out InputComponent result));
        Assert.Equal(input.MovementX, result.MovementX);
        Assert.Equal(input.MovementY, result.MovementY);
        Assert.Equal(input.MovementZ, result.MovementZ);
        Assert.Equal(input.LookPitch, result.LookPitch);
        Assert.Equal(input.LookYaw, result.LookYaw);
        Assert.Equal(input.LookRoll, result.LookRoll);
        Assert.Equal(input.SequenceNumber, result.SequenceNumber);
        Assert.Equal(input.ServerTickAtSample, result.ServerTickAtSample);
    }

    [Fact]
    public void RegistryRegistersAndFreezesComponents()
    {
        NetworkRegistry registry = CreateRegistry();

        Result<NetworkComponentInfo> lookup = registry.GetInfo<InputComponent>();
        Assert.True(lookup.Success);
        Assert.Equal(NetworkDirection.ClientOwned, lookup.Value.Direction);
        Assert.IsType<NsdComponentCodec<InputComponent>>(lookup.Value.Codec);

        Assert.True(registry.GetInfo<BodyViewComponent>().Success);
        Assert.Equal(2, registry.GetComponents(NetworkDirection.ClientOwned).Length);
        Assert.Single(registry.GetComponents(NetworkDirection.ServerOwned));

        Assert.False(registry.Register<InputComponent>(Uuid.FromValue(99), NetworkDirection.ClientOwned, new NsdComponentCodec<InputComponent>()).Success);
    }

    [Fact]
    public void RegistryRejectsDuplicateTypeAndUuid()
    {
        var registry = new NetworkRegistry();
        Assert.True(registry.Register<InputComponent>(Uuid.FromValue(1), NetworkDirection.ClientOwned, new NsdComponentCodec<InputComponent>()).Success);
        Assert.False(registry.Register<InputComponent>(Uuid.FromValue(2), NetworkDirection.ClientOwned, new NsdComponentCodec<InputComponent>()).Success);
        Assert.False(registry.Register<BodyViewComponent>(Uuid.FromValue(1), NetworkDirection.ServerOwned, new NsdComponentCodec<BodyViewComponent>()).Success);
    }

    [Fact]
    public void BodyViewComponentCodecRoundTrips()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new BodyViewComponent { Body = "wb:human_m" });

        var codec = new NsdComponentCodec<BodyViewComponent>();
        Result<byte[]> serialized = codec.Serialize(store, entity);
        Assert.True(serialized.Success);
        Assert.NotEmpty(serialized.Value);

        var output = new DataStore();
        int outputEntity = output.Alloc();
        Assert.True(codec.Apply(output, outputEntity, serialized.Value).Success);

        Assert.True(output.TryGet(outputEntity, out BodyViewComponent result));
        Assert.Equal("wb:human_m", result.Body);
    }

    [Fact]
    public void InteractionEventCodecRoundTripsHint()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new InteractionEvent
        {
            Entity = 0x1234,
            SequenceNumber = 11,
            ServerTickAtSample = 9,
            Kind = InteractionKind.SecondaryPressed,
            Brick = new BrickInteraction { TargetX = -2, TargetY = 5, TargetZ = 1, HintShape = 3, HintOrientation = 4 },
        });

        var codec = new NsdComponentCodec<InteractionEvent>();
        Result<byte[]> serialized = codec.Serialize(store, entity);
        Assert.True(serialized.Success);
        Assert.NotEmpty(serialized.Value);

        var output = new DataStore();
        int outputEntity = output.Alloc();
        Assert.True(codec.Apply(output, outputEntity, serialized.Value).Success);

        Assert.True(output.TryGet(outputEntity, out InteractionEvent result));
        Assert.Equal(0x1234UL, result.Entity);
        Assert.Equal(11u, result.SequenceNumber);
        Assert.Equal(9u, result.ServerTickAtSample);
        Assert.Equal(InteractionKind.SecondaryPressed, result.Kind);
        Assert.NotNull(result.Brick);
        Assert.Equal(-2, result.Brick.Value.TargetX);
        Assert.Equal(5, result.Brick.Value.TargetY);
        Assert.Equal(1, result.Brick.Value.TargetZ);
    }

    [Fact]
    public void CodecFailsWhenTheEntityHasNoComponent()
    {
        var store = new DataStore();
        int entity = store.Alloc();

        var codec = new NsdComponentCodec<InputComponent>();
        Assert.False(codec.Serialize(store, entity).Success);
    }
}
