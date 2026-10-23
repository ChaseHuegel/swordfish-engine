namespace WaywardBeyond.Networking.Rpc;

/// <summary>
/// Reassembles one fragmented payload at a time. Parts must arrive in order. A newer group id, a
/// repeated first part, or an out-of-order part drops the stale incomplete group, so an abandoned
/// batch can never join with a later batch.
/// </summary>
public sealed class FragmentAssembler
{
    private uint _groupId;
    private int _partCount;
    private byte[][]? _parts;
    private int _received;
    private bool _active;

    /// <summary>True while a group is incomplete.</summary>
    public bool IsActive => _active;

    /// <summary>The group id in flight, or zero when idle.</summary>
    public uint GroupId => _groupId;

    /// <summary>
    /// Adds one part. Returns true only when the last part completes the group and
    /// <paramref name="payload"/> holds the joined bytes. The assembler is idle again after a complete
    /// join and after any reset.
    /// </summary>
    public bool TryAppend(uint groupId, int partIndex, int partCount, byte[]? part, out byte[] payload)
    {
        payload = [];
        if (partCount < 1 || partIndex < 0 || partIndex >= partCount)
        {
            Reset();
            return false;
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
            //  A stale group can never complete. Drop it and wait for a fresh first part.
            Reset();
            return false;
        }

        _parts![_received] = part ?? [];
        _received++;

        if (_received < _partCount)
        {
            return false;
        }

        byte[][] completed = _parts;
        Reset();
        payload = PayloadFragmenter.Join(completed);
        return true;
    }

    /// <summary>Drops the incomplete group, if any.</summary>
    public void Reset()
    {
        _groupId = 0;
        _partCount = 0;
        _parts = null;
        _received = 0;
        _active = false;
    }
}
