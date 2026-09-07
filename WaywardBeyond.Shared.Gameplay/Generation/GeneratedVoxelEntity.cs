using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
///     The result of running world generation for a single voxel structure: the serializable chunk data
///     plus the transform it should occupy. Produced by the game-shared <see cref="WorldGenerator"/>, which
///     stays free of ECS and rendering. A server persists these as <see cref="VoxelEntityData"/> and builds
///     authority bodies via <see cref="VoxelWorldEntityFactory.CreateAuthority"/>; the same data is streamed
///     to a client for its view world.
/// </summary>
public readonly struct GeneratedVoxelEntity(in Uuid uuid, in Vector3 position, in Quaternion orientation, in ChunkInfo[] chunks)
{
    public readonly Uuid Uuid = uuid;
    public readonly Vector3 Position = position;
    public readonly Quaternion Orientation = orientation;
    public readonly ChunkInfo[] Chunks = chunks;
}