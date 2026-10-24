using System;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Networking;

/// <summary>Domain lookups and staging rules over the networked staging rings.</summary>
public static class StageRingExtensions
{
    /// <summary>Returns the newest input staged at or before <paramref name="simTick"/>.</summary>
    public static Result<InputComponent> GetCommand(this StageRing<InputComponent> ring, uint simTick)
    {
        for (var index = ring.Count - 1; index >= 0; index--)
        {
            InputComponent candidate = ring[index];
            if (candidate.ServerTickAtSample <= simTick)
            {
                return Result<InputComponent>.FromSuccess(candidate);
            }
        }

        return Result<InputComponent>.FromFailure((string?)null);
    }

    /// <summary>Drops every input staged at or before <paramref name="simTick"/>.</summary>
    public static void TrimAtOrBelow(this StageRing<InputComponent> ring, uint simTick)
    {
        while (ring.Count > 0 && ring[0].ServerTickAtSample <= simTick)
        {
            ring.Advance(1);
        }
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

    /// <summary>Stages one inventory op, replacing a re-staged sequence in place.</summary>
    public static void Stage(this StageRing<InventoryOp> ring, uint sequenceNumber, in SlotMoveOp slotMove)
    {
        var value = new InventoryOp(sequenceNumber, in slotMove);

        for (var index = 0; index < ring.Count; index++)
        {
            if (ring[index].SequenceNumber == sequenceNumber)
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
}
