using WaywardBeyond.Shared.Networking.Components;
using Xunit;

namespace Swordfish.Tests;

public class InteractionStageBufferTests
{
    private static InteractionEvent Event(uint sequence, uint tick, InteractionKind kind = InteractionKind.PrimaryPressed)
    {
        return new InteractionEvent
        {
            Entity = 1,
            SequenceNumber = sequence,
            ServerTickAtSample = tick,
            Kind = (byte)kind,
            Brick = null,
        };
    }

    [Fact]
    public void ConsumesEachStagedEventPerSimTick()
    {
        var buffer = new InteractionStageBuffer();
        buffer.Stage(Event(1, 10));
        buffer.Stage(Event(2, 11));
        buffer.Stage(Event(3, 12));

        Assert.True(buffer.TryGet(10, out InteractionEvent e1));
        Assert.Equal(1u, e1.SequenceNumber);

        Assert.True(buffer.TryGet(11, out InteractionEvent e2));
        Assert.Equal(2u, e2.SequenceNumber);

        Assert.True(buffer.TryGet(12, out InteractionEvent e3));
        Assert.Equal(3u, e3.SequenceNumber);
    }

    [Fact]
    public void KeepsDistinctEventsSharingASimTick()
    {
        var buffer = new InteractionStageBuffer();
        buffer.Stage(Event(1, 10));
        buffer.Stage(Event(2, 10));
        buffer.Stage(Event(3, 10));

        Assert.True(buffer.TryGet(10, out InteractionEvent result));
        Assert.Equal(3u, result.SequenceNumber);

        //  Each discrete edge targets the same tick but is a distinct edit; none may be collapsed.
        Assert.True(buffer.TryConsume(10, lastSequenceNumber: 0, out InteractionEvent e1));
        Assert.Equal(1u, e1.SequenceNumber);
        Assert.True(buffer.TryConsume(10, lastSequenceNumber: 1, out InteractionEvent e2));
        Assert.Equal(2u, e2.SequenceNumber);
        Assert.True(buffer.TryConsume(10, lastSequenceNumber: 2, out InteractionEvent e3));
        Assert.Equal(3u, e3.SequenceNumber);
    }

    [Fact]
    public void DedupesBySequenceNumber()
    {
        var buffer = new InteractionStageBuffer();
        buffer.Stage(Event(1, 10));
        //  A retransmit carrying the same sequence overwrites its slot in place rather than staging twice.
        buffer.Stage(Event(1, 12));

        Assert.True(buffer.TryGet(12, out InteractionEvent result));
        Assert.Equal(1u, result.SequenceNumber);
        Assert.False(buffer.TryGet(11, out _));
    }

    [Fact]
    public void ReturnsNewestAtOrBelowSimTick()
    {
        var buffer = new InteractionStageBuffer();
        buffer.Stage(Event(1, 5));
        buffer.Stage(Event(2, 9));
        buffer.Stage(Event(3, 12));

        //  Tick 10 has no staged event; return the newest at or below it (tick 9).
        Assert.True(buffer.TryGet(10, out InteractionEvent result));
        Assert.Equal(2u, result.SequenceNumber);
    }

    [Fact]
    public void LateEventAtOrBelowTickIsStillConsumed()
    {
        var buffer = new InteractionStageBuffer();
        buffer.Stage(Event(1, 9));
        buffer.Stage(Event(2, 14));

        Assert.True(buffer.TryGet(12, out InteractionEvent result));
        Assert.Equal(1u, result.SequenceNumber);
    }

    [Fact]
    public void SnapshotPreservesEveryDistinctEdgeAndClearEmpties()
    {
        var buffer = new InteractionStageBuffer();
        buffer.Stage(Event(1, 10));
        buffer.Stage(Event(2, 10));
        buffer.Stage(Event(3, 12));

        InteractionEvent[] snapshot = buffer.Snapshot();
        Assert.Equal(3, snapshot.Length);
        Assert.Equal(1u, snapshot[0].SequenceNumber);
        Assert.Equal(2u, snapshot[1].SequenceNumber);
        Assert.Equal(3u, snapshot[2].SequenceNumber);

        buffer.Clear();
        Assert.Equal(0, buffer.Snapshot().Length);
        Assert.False(buffer.TryGet(12, out _));
    }
}