using System;
using Krill;
using WaywardBeyond.Networking.Codecs;

namespace WaywardBeyond.Networking.Registry;

/// <summary>Registry metadata for one networked component type.</summary>
public readonly struct NetworkComponentInfo(
    Type type,
    Uuid uuid,
    NetworkDirection direction,
    IPayloadCodec codec,
    Func<DataStore, int, bool>? replicationFilter = null,
    Func<DataStore, int, ReplicationRecipient, bool>? recipientFilter = null
)
{
    /// <summary>The component type this entry describes.</summary>
    public Type Type { get; } = type;

    /// <summary>The stable wire uuid of the component type.</summary>
    public Uuid Uuid { get; } = uuid;

    /// <summary>The endpoint that is authoritative over the component's data.</summary>
    public NetworkDirection Direction { get; } = direction;

    /// <summary>The codec that serializes and applies the component.</summary>
    public IPayloadCodec Codec { get; } = codec;

    /// <summary>Returns whether an entity replicates this component, or null when every entity does.</summary>
    public Func<DataStore, int, bool>? ReplicationFilter { get; } = replicationFilter;

    /// <summary>Returns whether a recipient receives this component for an entity, or null when every recipient does.</summary>
    public Func<DataStore, int, ReplicationRecipient, bool>? RecipientFilter { get; } = recipientFilter;
}
