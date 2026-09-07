using Swordfish.ECS;
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