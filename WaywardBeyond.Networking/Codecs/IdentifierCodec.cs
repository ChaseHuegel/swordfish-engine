using System;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Registry;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>
/// <see cref="IPayloadCodec"/> for the engine's <see cref="IdentifierComponent"/> (Name/Tag), spoken over
/// the wire as the nsd <see cref="IdentifierMessage"/>. Lets the server replicate a player's
/// <see cref="IdentifierComponent"/> (e.g. the character <see cref="IdentifierComponent.Name"/>) to clients
/// through the existing snapshot path.
/// </summary>
public sealed class IdentifierCodec : IPayloadCodec
{
    /// <inheritdoc/>
    public Type ComponentType => typeof(IdentifierComponent);

    /// <inheritdoc/>
    public Result<byte[]> Serialize(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out IdentifierComponent identifier))
        {
            return Result<byte[]>.FromFailure($"Entity {entity} has no IdentifierComponent.");
        }

        var message = new IdentifierMessage(identifier.Name ?? string.Empty, identifier.Tag ?? string.Empty);
        return Result<byte[]>.FromSuccess(message.Serialize());
    }

    /// <inheritdoc/>
    public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        IdentifierMessage message = IdentifierMessage.Deserialize(payload);

        store.AddOrUpdate(entity, new IdentifierComponent(message.Name, message.Tag));
    }
}