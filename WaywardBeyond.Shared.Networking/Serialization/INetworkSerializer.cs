using System;

namespace WaywardBeyond.Shared.Networking.Serialization;

/// <summary>Uniform serializer over a message type, used by transports to (de)serialize wire frames.</summary>
public interface INetworkSerializer
{
    Type MessageType { get; }

    byte[] Serialize(object message);

    object Deserialize(ReadOnlySpan<byte> data);
}