using System;

namespace WaywardBeyond.Networking.Rpc;

/// <summary>Reassembles one fragmented payload at a time. Parts must arrive in order.</summary>
public sealed class FragmentAssembler
{
    private uint _groupId;
    private int _partCount;
    private byte[][]? _parts;
    private int _received;
    private int _bytes;
    private bool _active;

    /// <summary>True while a group is incomplete.</summary>
    public bool IsActive => _active;

    /// <summary>Appends one part and reports whether the group completed or was rejected.</summary>
    public FragmentState Append(uint groupId, int partIndex, int partCount, ReadOnlySpan<byte> part, out byte[] payload)
    {
        payload = [];

        if (partCount < 1
            || partCount > PayloadFragmenter.MAX_PARTS
            || partIndex < 0
            || partIndex >= partCount
            || part.Length > PayloadFragmenter.FRAGMENT_BUDGET)
        {
            Reset();
            return FragmentState.Rejected;
        }

        if (partIndex == 0)
        {
            //  A new first part replaces any incomplete group, including one with the same id.
            Reset();
            _groupId = groupId;
            _partCount = partCount;
            _parts = new byte[partCount][];
            _active = true;
        }
        else if (!_active || _groupId != groupId || partCount != _partCount || partIndex != _received)
        {
            //  A stale or out-of-order group can never complete. Drop it and wait for a fresh first part.
            Reset();
            return FragmentState.Rejected;
        }

        _bytes += part.Length;
        if (_bytes > PayloadFragmenter.MAX_PAYLOAD_BYTES)
        {
            Reset();
            return FragmentState.Rejected;
        }

        _parts![_received] = part.ToArray();
        _received++;

        if (_received < _partCount)
        {
            return FragmentState.Incomplete;
        }

        byte[][] completed = _parts;
        Reset();
        payload = PayloadFragmenter.Join(completed);
        return FragmentState.Complete;
    }

    /// <summary>Drops the incomplete group, if any.</summary>
    public void Reset()
    {
        _groupId = 0;
        _partCount = 0;
        _parts = null;
        _received = 0;
        _bytes = 0;
        _active = false;
    }
}
