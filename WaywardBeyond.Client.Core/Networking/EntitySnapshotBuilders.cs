using System;
using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Snapshots;

namespace WaywardBeyond.Client.Core.Networking;

internal sealed class TransformSnapshotBuilder : IComponentSnapshotBuilder
{
    public Type ComponentType => typeof(TransformComponent);

    public void BuildSnapshot(DataStore store, int entity, ref EntitySnapshotMsg snapshot)
    {
        if (!store.TryGet(entity, out TransformComponent transform))
        {
            return;
        }

        snapshot.PositionX = transform.Position.X;
        snapshot.PositionY = transform.Position.Y;
        snapshot.PositionZ = transform.Position.Z;
        snapshot.OrientationX = transform.Orientation.X;
        snapshot.OrientationY = transform.Orientation.Y;
        snapshot.OrientationZ = transform.Orientation.Z;
        snapshot.OrientationW = transform.Orientation.W;
    }

    public void ApplySnapshot(EntitySnapshotMsg snapshot, DataStore store, int entity)
    {
        store.AddOrUpdate(entity, new TransformComponent(
            new Vector3(snapshot.PositionX, snapshot.PositionY, snapshot.PositionZ),
            new Quaternion(snapshot.OrientationX, snapshot.OrientationY, snapshot.OrientationZ, snapshot.OrientationW)
        ));
    }
}

internal sealed class PhysicsSnapshotBuilder : IComponentSnapshotBuilder
{
    public Type ComponentType => typeof(PhysicsComponent);

    public void BuildSnapshot(DataStore store, int entity, ref EntitySnapshotMsg snapshot)
    {
        if (!store.TryGet(entity, out PhysicsComponent physics))
        {
            return;
        }

        snapshot.VelocityX = physics.Velocity.X;
        snapshot.VelocityY = physics.Velocity.Y;
        snapshot.VelocityZ = physics.Velocity.Z;
    }

    public void ApplySnapshot(EntitySnapshotMsg snapshot, DataStore store, int entity)
    {
        store.QueryRef<PhysicsComponent>(entity, 0f, (float d, DataStore s, int e, ref Ref<PhysicsComponent> physics) =>
        {
            physics.Write.Velocity = new Vector3(snapshot.VelocityX, snapshot.VelocityY, snapshot.VelocityZ);
        });
    }
}
