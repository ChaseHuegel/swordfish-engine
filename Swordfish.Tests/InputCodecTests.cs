using Swordfish.ECS;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using Xunit;

namespace Swordfish.Tests;

public class NetworkComponentCodecTests
{
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
        byte[] payload = codec.Serialize(store, entity);

        Assert.NotEmpty(payload);

        var output = new DataStore();
        int outputEntity = output.Alloc();
        codec.Apply(output, outputEntity, payload);

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
    public void InitializeRegistersAttributedComponents()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);

        Assert.True(NetworkRegistry.TryGetInfo<InputComponent>(out NetworkComponentInfo info));
        Assert.Equal(NetworkDirection.ClientOwned, info.Direction);
        Assert.IsType<NsdComponentCodec<InputComponent>>(info.Codec);

        //  The server-authored appearance index auto-registers from the same assembly scan.
        Assert.True(NetworkRegistry.TryGetInfo<BodyViewComponent>(out NetworkComponentInfo bodyInfo));
        Assert.Equal(NetworkDirection.ServerOwned, bodyInfo.Direction);
        Assert.IsType<NsdComponentCodec<BodyViewComponent>>(bodyInfo.Codec);
    }

    [Fact]
    public void BodyViewComponentCodecRoundTrips()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new BodyViewComponent { Body = 3 });

        var codec = new NsdComponentCodec<BodyViewComponent>();
        byte[] payload = codec.Serialize(store, entity);
        Assert.NotEmpty(payload);

        var output = new DataStore();
        int outputEntity = output.Alloc();
        codec.Apply(output, outputEntity, payload);

        Assert.True(output.TryGet(outputEntity, out BodyViewComponent result));
        Assert.Equal(3, result.Body);
    }

    [Fact]
    public void IdentifierCodecRoundTripsNameAndTag()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new IdentifierComponent("Ada", "player"));

        var codec = new IdentifierCodec();
        byte[] payload = codec.Serialize(store, entity);
        Assert.NotEmpty(payload);

        var output = new DataStore();
        int outputEntity = output.Alloc();
        codec.Apply(output, outputEntity, payload);

        Assert.True(output.TryGet(outputEntity, out IdentifierComponent result));
        Assert.Equal("Ada", result.Name);
        Assert.Equal("player", result.Tag);
    }

    [Fact]
    public void InitializeRegistersInteractionEventComponent()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);

        Assert.True(NetworkRegistry.TryGetInfo<InteractionEvent>(out NetworkComponentInfo info));
        Assert.Equal(NetworkDirection.ClientOwned, info.Direction);
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
            Kind = (byte)InteractionKind.SecondaryPressed,
            Brick = new BrickInteraction { TargetX = -2, TargetY = 5, TargetZ = 1, HintShape = 3, HintOrientation = 4 },
        });

        var codec = new NsdComponentCodec<InteractionEvent>();
        byte[] payload = codec.Serialize(store, entity);
        Assert.NotEmpty(payload);

        var output = new DataStore();
        int outputEntity = output.Alloc();
        codec.Apply(output, outputEntity, payload);

        Assert.True(output.TryGet(outputEntity, out InteractionEvent result));
        Assert.Equal(0x1234UL, result.Entity);
        Assert.Equal(11u, result.SequenceNumber);
        Assert.Equal(9u, result.ServerTickAtSample);
        Assert.Equal((byte)InteractionKind.SecondaryPressed, result.Kind);
        Assert.NotNull(result.Brick);
        Assert.Equal(-2, result.Brick.Value.TargetX);
        Assert.Equal(5, result.Brick.Value.TargetY);
        Assert.Equal(1, result.Brick.Value.TargetZ);
        Assert.Equal(3, result.Brick.Value.HintShape);
        Assert.Equal(4, result.Brick.Value.HintOrientation);
    }

    [Fact]
    public void InteractionEventCodecRoundTripsHintless()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new InteractionEvent
        {
            Entity = 0x1111,
            SequenceNumber = 12,
            ServerTickAtSample = 10,
            Kind = (byte)InteractionKind.PrimaryReleased,
            Brick = null,
        });

        var codec = new NsdComponentCodec<InteractionEvent>();
        byte[] payload = codec.Serialize(store, entity);

        var output = new DataStore();
        int outputEntity = output.Alloc();
        codec.Apply(output, outputEntity, payload);

        Assert.True(output.TryGet(outputEntity, out InteractionEvent result));
        Assert.False(result.Brick.HasValue);
        Assert.Equal((byte)InteractionKind.PrimaryReleased, result.Kind);
    }

    [Fact]
    public void WorldSnapshotRoundTripsComponentSnapshots()
    {
        var snapshot = new WorldSnapshot
        {
            TickNumber = 5,
            LastProcessedInput = 3,
            Components =
            [
                new ComponentSnapshot(0x1234, 0x1001, [1, 2, 3, 4]),
            ],
            RemovedEntities = [0xFFFF],
        };

        byte[] bytes = snapshot.Serialize();
        WorldSnapshot roundTripped = WorldSnapshot.Deserialize(bytes);

        Assert.Equal(snapshot.TickNumber, roundTripped.TickNumber);
        Assert.Equal(snapshot.LastProcessedInput, roundTripped.LastProcessedInput);
        Assert.Equal(snapshot.Components[0].Entity, roundTripped.Components[0].Entity);
        Assert.Equal(snapshot.Components[0].TypeUuid, roundTripped.Components[0].TypeUuid);
        Assert.Equal(snapshot.Components[0].Payload, roundTripped.Components[0].Payload);
        Assert.Equal(snapshot.RemovedEntities, roundTripped.RemovedEntities);
    }
}