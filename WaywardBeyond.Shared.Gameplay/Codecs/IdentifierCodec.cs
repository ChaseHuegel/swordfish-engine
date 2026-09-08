using System;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// <see cref="IPayloadCodec"/> for the engine's <see cref="IdentifierComponent"/> (Name/Tag), spoken over
/// the wire as the nsd <see cref="IdentifierMessage"/>. Lets the server replicate a player's
/// <see cref="IdentifierComponent"/> (e.g. the character <see cref="IdentifierComponent.Name"/>) to clients
/// through the existing snapshot path.
/// </summary>
public sealed class IdentifierCodec : IPayloadCodec
{
    public Type ComponentType => typeof(IdentifierComponent);

    public byte[] Serialize(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out IdentifierComponent identifier))
        {
            return Array.Empty<byte>();
        }

        var message = new IdentifierMessage(identifier.Name, identifier.Tag);
        return message.Serialize();
    }

    public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        IdentifierMessage message = IdentifierMessage.Deserialize(payload);

        store.AddOrUpdate(entity, new IdentifierComponent(message.Name, message.Tag));
    }
}