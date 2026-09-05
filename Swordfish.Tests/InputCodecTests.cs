using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Snapshots;
using Xunit;

namespace Swordfish.Tests;

public class InputCodecTests
{
    [Fact]
    public void RoundTripsInputComponentThroughCodec()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        var input = new InputComponent
        {
            Movement = new Vector3(1f, 2f, 3f),
            LookDelta = new Vector2(4f, 5f),
            Jump = true,
            SequenceNumber = 9,
            ServerTickAtSample = 7,
        };
        store.AddOrUpdate(entity, input);

        var codec = new InputCodec();
        byte[] payload = codec.Serialize(store, entity);

        Assert.NotEmpty(payload);

        var output = new DataStore();
        int outputEntity = output.Alloc();
        codec.Apply(output, outputEntity, payload);

        Assert.True(output.TryGet(outputEntity, out InputComponent result));
        Assert.Equal(input.Movement, result.Movement);
        Assert.Equal(input.LookDelta, result.LookDelta);
        Assert.Equal(input.Jump, result.Jump);
        Assert.Equal(input.SequenceNumber, result.SequenceNumber);
        Assert.Equal(input.ServerTickAtSample, result.ServerTickAtSample);
    }

    [Fact]
    public void WorldSnapshotRoundTripsComponentSnapshots()
    {
        var snapshot = new WaywardBeyond.Shared.Networking.WorldSnapshot
        {
            TickNumber = 5,
            LastProcessedInput = 3,
            Components =
            [
                new WaywardBeyond.Shared.Networking.ComponentSnapshot(0x1234, 0x1001, [1, 2, 3, 4]),
            ],
            RemovedEntities = [0xFFFF],
        };

        byte[] bytes = snapshot.Serialize();
        WaywardBeyond.Shared.Networking.WorldSnapshot roundTripped = WaywardBeyond.Shared.Networking.WorldSnapshot.Deserialize(bytes);

        Assert.Equal(snapshot.TickNumber, roundTripped.TickNumber);
        Assert.Equal(snapshot.LastProcessedInput, roundTripped.LastProcessedInput);
        Assert.Equal(snapshot.Components[0].Entity, roundTripped.Components[0].Entity);
        Assert.Equal(snapshot.Components[0].TypeUuid, roundTripped.Components[0].TypeUuid);
        Assert.Equal(snapshot.Components[0].Payload, roundTripped.Components[0].Payload);
        Assert.Equal(snapshot.RemovedEntities, roundTripped.RemovedEntities);
    }
}