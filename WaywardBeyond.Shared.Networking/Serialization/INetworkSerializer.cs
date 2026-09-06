using System;

namespace WaywardBeyond.Shared.Networking.Serialization;

/// <summary>
/// Non-generic marker shared by every message serializer. Lets a transport index the
/// heterogeneous <see cref="ISerializer{T}"/> set by message type without losing type
/// safety in the DI container (i.e. no bare <c>object[]</c>).
/// </summary>
public interface INetworkSerializer
{
    Type MessageType { get; }
}