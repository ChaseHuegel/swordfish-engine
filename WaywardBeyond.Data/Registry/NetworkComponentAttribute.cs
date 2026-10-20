using System;
using Swordfish.ECS;

namespace WaywardBeyond.Data.Registry;

/// <summary>
/// Marks an <see cref="IDataComponent"/> as networked. The <see cref="Uuid"/> is a stable identity
/// for the component type on the wire and must not change between builds.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class NetworkComponentAttribute(ulong uuid, NetworkDirection direction) : Attribute
{
    public Uuid Uuid { get; } = Uuid.FromValue(uuid);

    public NetworkDirection Direction { get; } = direction;
}