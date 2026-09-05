using System;
using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Registry;

/// <summary>
/// Serializes an ECS component into the opaque binary payload carried by a
/// <see cref="ComponentSnapshot"/>, and applies such a payload back onto an entity.
/// </summary>
public interface IPayloadCodec
{
    /// <summary>The component type this codec serializes.</summary>
    Type ComponentType { get; }

    /// <summary>Serializes the component present on <paramref name="entity"/> into a payload byte blob.</summary>
    byte[] Serialize(DataStore store, int entity);

    /// <summary>Applies a deserialized payload onto <paramref name="entity"/>.</summary>
    void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload);
}