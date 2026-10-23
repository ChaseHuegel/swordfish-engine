using Xunit;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Networking;

namespace Swordfish.Tests;

public class StageRingTests
{
    private static InteractionEvent Event(uint sequence, uint tick)
    {
        return new InteractionEvent
        {
            Entity = 1,
            SequenceNumber = sequence,
            ServerTickAtSample = tick,
            Kind = InteractionKind.PrimaryPressed,
            Brick = null,
        };
    }

    private static InputComponent Input(uint sequence, uint tick)
    {
        return new InputComponent
        {
            SequenceNumber = sequence,
            ServerTickAtSample = tick,
        };
    }

    [Fact]
    public void ConsumesEachStagedInteractionInTickOrder()
    {
        var ring = new StageRing<InteractionEvent>(8);
        ring.Stage(Event(1, 10));
        ring.Stage(Event(2, 11));
        ring.Stage(Event(3, 12));

        Result<InteractionEvent> result = ring.Consume(12, lastSequenceNumber: 0);
        Assert.True(result.Success);
        Assert.Equal(1u, result.Value.SequenceNumber);

        result = ring.Consume(12, lastSequenceNumber: 1);
        Assert.Equal(2u, result.Value.SequenceNumber);

        result = ring.Consume(12, lastSequenceNumber: 2);
        Assert.Equal(3u, result.Value.SequenceNumber);

        Assert.False(ring.Consume(12, lastSequenceNumber: 3).Success);
    }

    [Fact]
    public void KeepsDistinctInteractionsSharingATick()
    {
        var ring = new StageRing<InteractionEvent>(8);
        ring.Stage(Event(1, 10));
        ring.Stage(Event(2, 10));
        ring.Stage(Event(3, 10));

        Assert.Equal(1u, ring.Consume(10, lastSequenceNumber: 0).Value.SequenceNumber);
        Assert.Equal(2u, ring.Consume(10, lastSequenceNumber: 1).Value.SequenceNumber);
        Assert.Equal(3u, ring.Consume(10, lastSequenceNumber: 2).Value.SequenceNumber);
    }

    [Fact]
    public void DedupesInteractionBySequence()
    {
        var ring = new StageRing<InteractionEvent>(8);
        ring.Stage(Event(1, 10));
        //  A re-staged sequence overwrites its slot in place rather than staging twice.
        ring.Stage(Event(1, 12));

        Assert.Equal(1, ring.Count);
        Assert.Equal(12u, ring.Consume(12, lastSequenceNumber: 0).Value.ServerTickAtSample);
    }

    [Fact]
    public void ReturnsNewestInputAtOrBelowSimTick()
    {
        var ring = new StageRing<InputComponent>(8);
        ring.Add(Input(1, 5));
        ring.Add(Input(2, 9));
        ring.Add(Input(3, 12));

        //  Tick 10 has no staged command; return the newest at or below it (tick 9).
        Assert.Equal(2u, ring.GetCommand(10).Value.SequenceNumber);
        Assert.False(ring.GetCommand(4).Success);
    }

    [Fact]
    public void TrimDropsAcknowledgedInputs()
    {
        var ring = new StageRing<InputComponent>(8);
        ring.Add(Input(1, 5));
        ring.Add(Input(2, 9));
        ring.Add(Input(3, 12));

        ring.TrimAtOrBelow(9);

        Assert.Equal(1, ring.Count);
        Assert.Equal(3u, ring.GetCommand(12).Value.SequenceNumber);
    }

    [Fact]
    public void RingDropsOldestValueAtCapacity()
    {
        var ring = new StageRing<InputComponent>(2);
        ring.Add(Input(1, 1));
        ring.Add(Input(2, 2));
        ring.Add(Input(3, 3));

        Assert.Equal(2, ring.Count);
        Assert.Equal(2u, ring[0].SequenceNumber);
    }

    [Fact]
    public void ClearDropsEveryStagedValue()
    {
        var ring = new StageRing<InteractionEvent>(8);
        ring.Stage(Event(1, 10));
        ring.Stage(Event(2, 12));

        ring.Clear();

        Assert.Equal(0, ring.Count);
        Assert.False(ring.Consume(12, lastSequenceNumber: 0).Success);
    }
}
