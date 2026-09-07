using System;
using System.Numerics;
using Swordfish.Library.Types.Shapes;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Shared collision-shape derivation for voxel structures. Builds a <see cref="CompoundShape"/> from
/// <see cref="VoxelEntityData"/> voxel occupancy alone: one unit <see cref="Box3"/> per non-air voxel at
/// its integer world-local coordinate, mirroring the client's render-pipeline
/// <see cref="WaywardBeyond.Client.Core.Voxels.Processing.CollisionPostPass"/> exactly (including
/// negative chunk offsets). Both the server authority builder and the client view builder consume the
/// same derivation so prediction and authority collide identically. The wire/persisted
/// <see cref="VoxelEntityData"/> never carries a collision shape, so this is the single source of truth
/// for collider geometry.
/// </summary>
public static class VoxelColliderBuilder
{
    /// <summary>
    /// Builds the collision shape for a voxel entity from its serialized chunks. The shape's geometry is
    /// expressed in world-local voxel coordinates; the owning entity's <c>TransformComponent</c> places it
    /// in the world.
    /// </summary>
    public static CompoundShape BuildCollition(in ChunkInfo[] chunks)
    {
        var shapes = new Shape[chunks.Length * 1];
        var positions = new Vector3[chunks.Length * 1];
        var orientations = new Quaternion[chunks.Length * 1];
        var count = 0;

        for (var c = 0; c < chunks.Length; c++)
        {
            ChunkInfo chunkInfo = chunks[c];
            Chunk chunk = chunkInfo.Chunk;

            int chunkSize = chunk.Size;
            if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
            {
                continue;
            }

            int shift = int.TrailingZeroCount(chunkSize);
            int mask = chunkSize - 1;
            int shift2 = shift * 2;

            Voxel[] voxels = chunk.Voxels;
            int baseX = chunkInfo.OffsetX * chunkSize;
            int baseY = chunkInfo.OffsetY * chunkSize;
            int baseZ = chunkInfo.OffsetZ * chunkSize;

            shapes = EnsureCapacity(shapes, count + voxels.Length);
            positions = EnsureCapacity(positions, count + voxels.Length);
            orientations = EnsureCapacity(orientations, count + voxels.Length);

            for (var i = 0; i < voxels.Length; i++)
            {
                if (voxels[i].ID == 0)
                {
                    continue;
                }

                int x = i & mask;
                int y = (i >> shift) & mask;
                int z = i >> shift2;

                shapes[count] = new Shape(new Box3(Vector3.One));
                positions[count] = new Vector3(baseX + x, baseY + y, baseZ + z);
                orientations[count] = Quaternion.Identity;
                count++;
            }
        }

        if (count == 0)
        {
            return new CompoundShape([], [], []);
        }

        return new CompoundShape(
            shapes.AsSpan(0, count).ToArray(),
            positions.AsSpan(0, count).ToArray(),
            orientations.AsSpan(0, count).ToArray()
        );
    }

    private static Shape[] EnsureCapacity(Shape[] array, int capacity)
    {
        if (capacity <= array.Length)
        {
            return array;
        }

        var next = new Shape[Math.Max(capacity, array.Length * 2)];
        Array.Copy(array, next, array.Length);
        return next;
    }

    private static Vector3[] EnsureCapacity(Vector3[] array, int capacity)
    {
        if (capacity <= array.Length)
        {
            return array;
        }

        var next = new Vector3[Math.Max(capacity, array.Length * 2)];
        Array.Copy(array, next, array.Length);
        return next;
    }

    private static Quaternion[] EnsureCapacity(Quaternion[] array, int capacity)
    {
        if (capacity <= array.Length)
        {
            return array;
        }

        var next = new Quaternion[Math.Max(capacity, array.Length * 2)];
        Array.Copy(array, next, array.Length);
        return next;
    }
}