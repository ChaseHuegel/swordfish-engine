using Xunit;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Networking;

namespace WaywardBeyond.Tests;

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
        var ring = new StageRing<StagedInput>(8);
        ring.Stage(Input(1, 5), stagedAtSimTick: 5);
        ring.Stage(Input(2, 9), stagedAtSimTick: 9);
        ring.Stage(Input(3, 12), stagedAtSimTick: 12);

        //  Tick 10 has no staged command; return the newest at or below it (tick 9).
        Assert.Equal(2u, ring.GetCommand(10, uint.MaxValue).Value.SequenceNumber);
        Assert.False(ring.GetCommand(4, uint.MaxValue).Success);
    }

    [Fact]
    public void ReturnsNoCommandWhenTheNewestInputIsStale()
    {
        var ring = new StageRing<StagedInput>(8);
        ring.Stage(Input(1, 5), stagedAtSimTick: 5);
        ring.Stage(Input(2, 9), stagedAtSimTick: 9);

        Assert.Equal(2u, ring.GetCommand(10, maxAgeTicks: 1).Value.SequenceNumber);
        Assert.False(ring.GetCommand(11, maxAgeTicks: 1).Success);
    }

    [Fact]
    public void FreshlyStagedInputWithOldServerTickAtSampleIsNotStale()
    {
        var ring = new StageRing<StagedInput>(8);
        //  An idle world may not have published a snapshot for hundreds of ticks, so
        //  ServerTickAtSample legitimately lags behind the current sim tick.
        ring.Stage(Input(1, tick: 10), stagedAtSimTick: 500);

        Assert.Equal(1u, ring.GetCommand(501, maxAgeTicks: 120).Value.SequenceNumber);
        Assert.Equal(1u, ring.GetCommand(620, maxAgeTicks: 120).Value.SequenceNumber);
        Assert.False(ring.GetCommand(621, maxAgeTicks: 120).Success);
    }

    [Fact]
    public void InputStagedAheadOfRealignedSimTickIsNotStale()
    {
        var ring = new StageRing<StagedInput>(8);
        ring.Stage(Input(1, tick: 10), stagedAtSimTick: 200);

        Assert.Equal(1u, ring.GetCommand(195, maxAgeTicks: 120).Value.SequenceNumber);
    }

    [Fact]
    public void TrimDropsAcknowledgedInputsAndReturnsNewestDroppedSample()
    {
        var ring = new StageRing<StagedInput>(8);
        ring.Stage(Input(1, 5), stagedAtSimTick: 5);
        ring.Stage(Input(2, 9), stagedAtSimTick: 9);
        ring.Stage(Input(3, 12), stagedAtSimTick: 12);

        StagedInput? trimmed = ring.TrimAtOrBelow(9);

        Assert.NotNull(trimmed);
        Assert.Equal(2u, trimmed.Value.Input.SequenceNumber);
        Assert.Equal(1, ring.Count);
        Assert.Equal(3u, ring.GetCommand(12, uint.MaxValue).Value.SequenceNumber);
        Assert.Null(ring.TrimAtOrBelow(9));
    }

    [Fact]
    public void GetCommandFallsBackToLastAckedWhenRingHasNoSampleAtOrBelowTick()
    {
        var ring = new StageRing<StagedInput>(8);
        ring.Stage(Input(1, 5), stagedAtSimTick: 5);
        StagedInput? lastAcked = ring.TrimAtOrBelow(5);

        Result<InputComponent> steady = ring.GetCommand(10, maxAgeTicks: 10, in lastAcked);
        Assert.True(steady.Success);
        Assert.Equal(1u, steady.Value.SequenceNumber);

        //  Once lastAcked exceeds maxAgeTicks, it resolves as idle input.
        Assert.False(ring.GetCommand(16, maxAgeTicks: 10, in lastAcked).Success);

        //  A future sample still allows fallback for earlier ticks during replay.
        ring.Stage(Input(2, 12), stagedAtSimTick: 12);
        Result<InputComponent> beforeFuture = ring.GetCommand(10, maxAgeTicks: 10, in lastAcked);
        Assert.True(beforeFuture.Success);
        Assert.Equal(1u, beforeFuture.Value.SequenceNumber);

        //  A stale ring entry at or below simTick blocks falling back to lastAcked.
        Assert.False(ring.GetCommand(25, maxAgeTicks: 10, in lastAcked).Success);
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
