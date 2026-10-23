using System;
using Swordfish.ECS;
using WaywardBeyond.Networking.Codecs;

namespace WaywardBeyond.Networking.Registry;

/// <summary>Registry metadata for one networked component type.</summary>
public readonly struct NetworkComponentInfo(Type type, Uuid uuid, NetworkDirection direction, IPayloadCodec codec)
{
    /// <summary>The component type this entry describes.</summary>
    public Type Type { get; } = type;

    /// <summary>The stable wire uuid of the component type.</summary>
    public Uuid Uuid { get; } = uuid;

    /// <summary>The endpoint that is authoritative over the component's data.</summary>
    public NetworkDirection Direction { get; } = direction;

    /// <summary>The codec that serializes and applies the component.</summary>
    public IPayloadCodec Codec { get; } = codec;
}
