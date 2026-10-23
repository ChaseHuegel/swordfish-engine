using System;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>Replicates <see cref="IdentifierComponent"/> as <see cref="IdentifierMessage"/>.</summary>
public sealed class IdentifierCodec : IPayloadCodec<IdentifierComponent>
{
    /// <inheritdoc/>
    public Result<byte[]> Serialize(DataStore store, int entity)
    {
        return store.TryGet(entity, out IdentifierComponent identifier)
            ? Result<byte[]>.FromSuccess(Serialize(in identifier))
            : Result<byte[]>.FromFailure((string?)null);
    }

    /// <inheritdoc/>
    public Result Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        IdentifierMessage message = IdentifierMessage.Deserialize(payload);

        store.AddOrUpdate(entity, new IdentifierComponent(message.Name, message.Tag));
        return Result.FromSuccess();
    }

    /// <inheritdoc/>
    public byte[] Serialize(in IdentifierComponent value)
    {
        var message = new IdentifierMessage(value.Name ?? string.Empty, value.Tag ?? string.Empty);
        return message.Serialize();
    }
}
