using Swordfish.ECS;

namespace WaywardBeyond.Client.Voxels;

/// <summary>Identifies a chunk child entity and its identity at allocation time.</summary>
public readonly struct ChunkEntity(in int entity, in Uuid uuid)
{
    public readonly int Entity = entity;
    public readonly Uuid Uuid = uuid;
}