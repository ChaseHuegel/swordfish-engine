using System;
using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Registry;

/// <summary>Typed convenience surface over <see cref="IPayloadCodec"/> for nsd-message components.</summary>
public interface IPayloadCodec<T> : IPayloadCodec
    where T : struct, IDataComponent
{
    byte[] Serialize(in T value);

    T Deserialize(ReadOnlySpan<byte> payload);
}