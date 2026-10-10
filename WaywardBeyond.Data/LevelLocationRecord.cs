namespace WaywardBeyond.Data;

/// <summary>One persisted character location row, keyed by character id.</summary>
public readonly struct LevelLocationRecord(in ulong characterUuid, in byte[] data)
{
    public readonly ulong CharacterUuid = characterUuid;
    public readonly byte[] Data = data;
}
