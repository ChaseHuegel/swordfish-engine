using System;
using Krill;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>Replicates <see cref="InventoryComponent"/> using its generated serializer and hydrates runtime caches on apply.</summary>
public sealed class InventoryCodec : IPayloadCodec<InventoryComponent>
{
    /// <inheritdoc/>
    public Result<byte[]> Serialize(DataStore store, int entity)
    {
        return store.TryGet(entity, out InventoryComponent inventory)
            ? Result<byte[]>.FromSuccess(Serialize(in inventory))
            : Result<byte[]>.FromFailure((string?)null);
    }

    /// <inheritdoc/>
    public Result Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        try
        {
            InventoryComponent value = InventoryComponent.Deserialize(payload);
            value.Hydrate();
            store.AddOrUpdate(entity, value);
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    /// <inheritdoc/>
    public byte[] Serialize(in InventoryComponent value)
    {
        return value.Serialize();
    }
}
