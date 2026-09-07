using Swordfish.ECS;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Carries a structure's serialized voxel content on its authority entity so the server can re-persist a
/// dynamic world body with its current authoritative transform (decision 16: structure motion is
/// server-authored and replicated). The server builds the body from a <see cref="VoxelEntityData"/>'s
/// chunks, which are not otherwise retained once the collider is constructed; this component keeps them
/// available for a server-side world save. It is a plain data component - deliberately not part of the
/// replication set (state is already replicated through <c>TransformComponent</c>/<c>PhysicsComponent</c>).
/// </summary>
public struct VoxelEntityDataComponent(in ChunkInfo[] chunks) : IDataComponent
{
    public ChunkInfo[] Chunks = chunks;
}