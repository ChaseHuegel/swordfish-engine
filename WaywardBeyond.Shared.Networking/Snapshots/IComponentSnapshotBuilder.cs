using System;
using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Snapshots;

public interface IComponentSnapshotBuilder
{
    Type ComponentType { get; }

    void BuildSnapshot(DataStore store, int entity, ref EntitySnapshotMsg snapshot);

    void ApplySnapshot(EntitySnapshotMsg snapshot, DataStore store, int entity);
}
