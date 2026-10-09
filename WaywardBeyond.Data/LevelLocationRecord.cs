namespace WaywardBeyond.Data;

/// <summary>One persisted character location row, keyed by character id.</summary>
public readonly struct LevelLocationRecord(in ulong characterId, in byte[] data)
{
    public readonly ulong CharacterId = characterId;
    public readonly byte[] Data = data;
}
