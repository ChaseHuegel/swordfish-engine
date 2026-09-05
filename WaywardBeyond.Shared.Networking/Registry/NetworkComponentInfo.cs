using System;
using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Registry;

/// <summary>Registry metadata describing a single networked component type.</summary>
public readonly struct NetworkComponentInfo
{
    public Type Type { get; }
    public Uuid Uuid { get; }
    public NetworkDirection Direction { get; }
    public IPayloadCodec Codec { get; }

    public NetworkComponentInfo(Type type, Uuid uuid, NetworkDirection direction, IPayloadCodec codec)
    {
        Type = type;
        Uuid = uuid;
        Direction = direction;
        Codec = codec;
    }
}