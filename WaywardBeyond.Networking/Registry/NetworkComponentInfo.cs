using System;
using Swordfish.ECS;
using WaywardBeyond.Data.Registry;

namespace WaywardBeyond.Networking.Registry;

/// <summary>Registry metadata describing a single networked component type.</summary>
public readonly struct NetworkComponentInfo(Type type, Uuid uuid, NetworkDirection direction, IPayloadCodec codec)
{
    public Type Type { get; } = type;
    public Uuid Uuid { get; } = uuid;
    public NetworkDirection Direction { get; } = direction;
    public IPayloadCodec Codec { get; } = codec;
}