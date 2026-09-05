using System;
using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Registry;

/// <summary>
/// Marks an <see cref="IDataComponent"/> as networked. The <see cref="Uuid"/> is a stable identity
/// for the component type on the wire and must not change between builds.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class NetworkComponentAttribute : Attribute
{
    public Uuid Uuid { get; }

    public NetworkDirection Direction { get; }

    public NetworkComponentAttribute(ulong uuid, NetworkDirection direction = NetworkDirection.ServerOwned)
    {
        Uuid = Uuid.FromValue(uuid);
        Direction = direction;
    }
}