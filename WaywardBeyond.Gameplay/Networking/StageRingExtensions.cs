using System;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Networking;

/// <summary>Domain lookups and staging rules over the networked staging rings.</summary>
public static class StageRingExtensions
{
    /// <summary>Stages one input sample tagged with the local sim tick at which it was staged.</summary>
    public static void Stage(this StageRing<StagedInput> ring, in InputComponent input, uint stagedAtSimTick)
    {
        ring.Add(new StagedInput(in input, stagedAtSimTick));
    }

    /// <summary>
    /// Returns the newest input staged at or before <paramref name="simTick"/> when it was staged no more
    /// than <paramref name="maxAgeTicks"/> ago, or <paramref name="lastAcked"/> when the ring has none at or before <paramref name="simTick"/>.
    /// </summary>
    public static Result<InputComponent> GetCommand(
        this StageRing<StagedInput> ring,
        uint simTick,
        uint maxAgeTicks,
        in StagedInput? lastAcked = null
    )
    {
        for (var index = ring.Count - 1; index >= 0; index--)
        {
            StagedInput candidate = ring[index];
            if (candidate.Input.ServerTickAtSample > simTick)
            {
                continue;
            }

            return IsFresh(in candidate, simTick, maxAgeTicks)
                ? Result<InputComponent>.FromSuccess(candidate.Input)
                : Result<InputComponent>.FromFailure((string?)null);
        }

        if (lastAcked != null)
        {
            StagedInput acked = lastAcked.Value;
            if (acked.Input.ServerTickAtSample <= simTick && IsFresh(in acked, simTick, maxAgeTicks))
            {
                return Result<InputComponent>.FromSuccess(acked.Input);
            }
        }

        return Result<InputComponent>.FromFailure((string?)null);
    }

    /// <summary>Drops every input staged at or before <paramref name="simTick"/> and returns the newest dropped sample, if any.</summary>
    public static StagedInput? TrimAtOrBelow(this StageRing<StagedInput> ring, uint simTick)
    {
        StagedInput? newest = null;
        while (ring.Count > 0 && ring[0].Input.ServerTickAtSample <= simTick)
        {
            newest = ring[0];
            ring.Advance(1);
        }

        return newest;
    }

    private static bool IsFresh(in StagedInput candidate, uint simTick, uint maxAgeTicks)
    {
        //  Age is measured from when the sample was staged locally rather than ServerTickAtSample, so an
        //  idle world without recent snapshots stays fresh and a hostile client cannot spoof the age check.
        //  If simTick rewound below StagedAtSimTick (e.g. after AlignTo), treat the age as zero.
        return simTick <= candidate.StagedAtSimTick
            || simTick - candidate.StagedAtSimTick <= maxAgeTicks;
    }

    /// <summary>Stages one interaction edge, replacing a re-staged sequence in place.</summary>
    public static void Stage(this StageRing<InteractionEvent> ring, in InteractionEvent value)
    {
        for (var index = 0; index < ring.Count; index++)
        {
            if (ring[index].SequenceNumber == value.SequenceNumber)
            {
                ring[index] = value;
                return;
            }
        }

        ring.Add(value);
    }

    /// <summary>Returns the oldest unconsumed interaction staged at or before <paramref name="simTick"/>.</summary>
    public static Result<InteractionEvent> Consume(this StageRing<InteractionEvent> ring, uint simTick, uint lastSequenceNumber)
    {
        var found = false;
        InteractionEvent best = default;

        for (var index = 0; index < ring.Count; index++)
        {
            InteractionEvent entry = ring[index];
            if (entry.ServerTickAtSample > simTick || entry.SequenceNumber <= lastSequenceNumber)
            {
                continue;
            }

            if (!found
                || entry.ServerTickAtSample < best.ServerTickAtSample
                || (entry.ServerTickAtSample == best.ServerTickAtSample && entry.SequenceNumber < best.SequenceNumber))
            {
                found = true;
                best = entry;
            }
        }

        return found
            ? Result<InteractionEvent>.FromSuccess(best)
            : Result<InteractionEvent>.FromFailure((string?)null);
    }

    /// <summary>Stages one unified inventory transfer op, replacing a re-staged sequence in place.</summary>
    public static void Stage(this StageRing<InventoryOp> ring, uint sequenceNumber, in TransferOp transfer)
    {
        var value = new InventoryOp(sequenceNumber, in transfer);
        for (var index = 0; index < ring.Count; index++)
        {
            if (ring[index].SequenceNumber == value.SequenceNumber)
            {
                ring[index] = value;
                return;
            }
        }

        ring.Add(value);
    }

    /// <summary>Returns the oldest unconsumed inventory op.</summary>
    public static Result<InventoryOp> Consume(this StageRing<InventoryOp> ring, uint lastSequenceNumber)
    {
        var found = false;
        InventoryOp best = default;

        for (var index = 0; index < ring.Count; index++)
        {
            InventoryOp entry = ring[index];
            if (entry.SequenceNumber <= lastSequenceNumber)
            {
                continue;
            }

            if (!found || entry.SequenceNumber < best.SequenceNumber)
            {
                found = true;
                best = entry;
            }
        }

        return found
            ? Result<InventoryOp>.FromSuccess(best)
            : Result<InventoryOp>.FromFailure((string?)null);
    }

    /// <summary>Stages one station action op, replacing a re-staged sequence in place.</summary>
    public static void Stage(this StageRing<StagedStationAction> ring, uint sequenceNumber, in StationActionOp op)
    {
        var value = new StagedStationAction(sequenceNumber, in op);
        for (var index = 0; index < ring.Count; index++)
        {
            if (ring[index].SequenceNumber == value.SequenceNumber)
            {
                ring[index] = value;
                return;
            }
        }

        ring.Add(value);
    }

    /// <summary>Returns the oldest unconsumed station action op.</summary>
    public static Result<StagedStationAction> Consume(this StageRing<StagedStationAction> ring, uint lastSequenceNumber)
    {
        var found = false;
        StagedStationAction best = default;

        for (var index = 0; index < ring.Count; index++)
        {
            StagedStationAction entry = ring[index];
            if (entry.SequenceNumber <= lastSequenceNumber)
            {
                continue;
            }

            if (!found || entry.SequenceNumber < best.SequenceNumber)
            {
                found = true;
                best = entry;
            }
        }

        return found
            ? Result<StagedStationAction>.FromSuccess(best)
            : Result<StagedStationAction>.FromFailure((string?)null);
    }
}
