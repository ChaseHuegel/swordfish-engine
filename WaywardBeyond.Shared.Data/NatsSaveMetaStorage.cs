using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Data;

/// <summary>
/// Client-owned metadata for game saves (per-level "last played" and "time played" on this client), so
/// multiplayer clients each track their own stats for a shared save. Each process runs its own local NATS,
/// so this bucket is per-client by construction. See <see cref="NatsCharacterStorage"/> for the sibling
/// client-owned character bucket.
/// </summary>
public class NatsSaveMetaStorage(in KeyValueStore keyValueStore) : ISaveMetaStorage
{
    private const string BUCKET_NAME = "saves";

    private readonly KeyValueStore _keyValueStore = keyValueStore;

    public Result<SaveMeta> Get(string levelGuid)
    {
        Result<byte[]> getResult = _keyValueStore.Get<byte[]>(BUCKET_NAME, levelGuid);
        if (!getResult.Success)
        {
            return Result<SaveMeta>.FromFailure(getResult.Message);
        }

        if (getResult.Value.Length == 0)
        {
            return Result<SaveMeta>.FromFailure("Save meta not found (empty value).");
        }

        try
        {
            SaveMeta meta = SaveMeta.Deserialize(getResult.Value);
            return Result<SaveMeta>.FromSuccess(meta);
        }
        catch (System.Exception ex)
        {
            return Result<SaveMeta>.FromFailure($"Failed to deserialize save meta: {ex.Message}");
        }
    }

    public IEnumerable<KeyValuePair<string, SaveMeta>> GetAll()
    {
        Result<string[]> keysResult = _keyValueStore.GetKeys(BUCKET_NAME);
        if (!keysResult.Success)
        {
            return [];
        }

        var metas = new List<KeyValuePair<string, SaveMeta>>();
        for (var i = 0; i < keysResult.Value.Length; i++)
        {
            string key = keysResult.Value[i];
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            Result<SaveMeta> metaResult = Get(key);
            if (metaResult.Success)
            {
                metas.Add(new KeyValuePair<string, SaveMeta>(key, metaResult.Value));
            }
        }

        return metas;
    }

    public Result Save(string levelGuid, SaveMeta meta)
    {
        try
        {
            byte[] data = meta.Serialize();
            return _keyValueStore.Put(BUCKET_NAME, levelGuid, data);
        }
        catch (System.Exception ex)
        {
            return Result.FromFailure($"Failed to serialize save meta for saving: {ex.Message}");
        }
    }

    public Result Delete(string levelGuid)
    {
        return _keyValueStore.Delete(BUCKET_NAME, levelGuid);
    }
}