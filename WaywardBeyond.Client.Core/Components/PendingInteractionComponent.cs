using System.Collections.Generic;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Client.Core.Components;

/// <summary>
/// Client-side bookkeeping for the local player's voxel interactions: <see cref="Queue"/> tracks the
/// outstanding predictions awaiting their authoritative <see cref="VoxelEditMessage"/> echo (see
/// <see cref="PendingInteractionQueue"/>), and <see cref="Outbound"/> buffers the discrete interaction
/// edges not yet transmitted upstream. Unlike a single <c>InteractionEvent</c> component slot, the
/// outbound buffer retains every click, so rapid placements that land between replication sends are not
/// silently overwritten and dropped.
/// </summary>
public struct PendingInteractionComponent(in PendingInteractionQueue queue) : IDataComponent
{
    public readonly PendingInteractionQueue Queue = queue;

    /// <summary>Edges authored by <c>PlayerInteractionService</c> awaiting replication to the server.</summary>
    public readonly InteractionStageBuffer Outbound = new();
}

/// <summary>
/// Thread-safe bookkeeping for outstanding local voxel predictions keyed by (entity, coordinate). The
/// prediction is authored from the interaction service thread and reconciled on the ECS thread, so the
/// queue is lock-guarded.
/// </summary>
public sealed class PendingInteractionQueue
{
    private readonly object _gate = new();

    private readonly List<PendingEdit> _edits = [];

    /// <summary>Registers a new outstanding prediction, collapsing a prior one at the same cell.</summary>
    public void Register(int entity, Int3 coordinate, in Voxel original, in Voxel predicted, uint sequence, uint serverTickAtSample)
    {
        lock (_gate)
        {
            int existing = FindIndex(entity, coordinate);
            var edit = new PendingEdit(entity, coordinate, original, predicted, sequence, serverTickAtSample);
            if (existing >= 0)
            {
                _edits[existing] = edit;
            }
            else
            {
                _edits.Add(edit);
            }
        }
    }

    /// <summary>Looks up the outstanding prediction for the given cell, if any.</summary>
    public bool TryFind(int entity, Int3 coordinate, out PendingEdit edit)
    {
        lock (_gate)
        {
            int index = FindIndex(entity, coordinate);
            if (index >= 0)
            {
                edit = _edits[index];
                return true;
            }

            edit = default;
            return false;
        }
    }

    /// <summary>Looks up the outstanding prediction echoed by the given interaction sequence, if any.</summary>
    public bool TryFindBySequence(uint sequence, out PendingEdit edit)
    {
        lock (_gate)
        {
            for (var i = 0; i < _edits.Count; i++)
            {
                if (_edits[i].Sequence == sequence)
                {
                    edit = _edits[i];
                    return true;
                }
            }

            edit = default;
            return false;
        }
    }

    /// <summary>Removes the prediction for the given cell, reporting whether one was present.</summary>
    public bool Remove(int entity, Int3 coordinate)
    {
        lock (_gate)
        {
            int index = FindIndex(entity, coordinate);
            if (index < 0)
            {
                return false;
            }

            _edits.RemoveAt(index);
            return true;
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _edits.Count;
            }
        }
    }

    public PendingEdit this[int index]
    {
        get
        {
            lock (_gate)
            {
                return _edits[index];
            }
        }
    }

    /// <summary>Returns a copy of all outstanding predictions under the queue lock.</summary>
    public PendingEdit[] Snapshot()
    {
        lock (_gate)
        {
            return _edits.ToArray();
        }
    }

    public void RemoveAt(int index)
    {
        lock (_gate)
        {
            _edits.RemoveAt(index);
        }
    }

    private int FindIndex(int entity, Int3 coordinate)
    {
        for (var i = 0; i < _edits.Count; i++)
        {
            if (_edits[i].Entity == entity && _edits[i].Coordinate == coordinate)
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>An outstanding predicted voxel edit waiting for reconciliation.</summary>
public readonly struct PendingEdit
{
    public readonly int Entity;
    public readonly Int3 Coordinate;
    public readonly Voxel Original;
    public readonly Voxel Predicted;
    public readonly uint Sequence;
    public readonly uint ServerTickAtSample;

    public PendingEdit(int entity, Int3 coordinate, Voxel original, Voxel predicted, uint sequence, uint serverTickAtSample)
    {
        Entity = entity;
        Coordinate = coordinate;
        Original = original;
        Predicted = predicted;
        Sequence = sequence;
        ServerTickAtSample = serverTickAtSample;
    }
}