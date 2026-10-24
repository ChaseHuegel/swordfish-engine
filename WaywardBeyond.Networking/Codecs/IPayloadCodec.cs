using System;
using Swordfish.ECS;
using Swordfish.Library.Util;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>Serializes a component into a snapshot payload and applies a payload back onto an entity.</summary>
public interface IPayloadCodec
{
    /// <summary>Serializes the component on <paramref name="entity"/>, or fails when the entity does not carry it.</summary>
    Result<byte[]> Serialize(DataStore store, int entity);

    /// <summary>Applies <paramref name="payload"/> to <paramref name="entity"/>, or fails when the payload is invalid.</summary>
    Result Apply(DataStore store, int entity, ReadOnlySpan<byte> payload);
}
