using System.Collections.Generic;
using System.Numerics;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using Xunit;

namespace Swordfish.Tests;

public class SharedWorldGenTests
{
    [Fact]
    public void WorldMaterialCatalogVoxelsUseStableDistinctDataIds()
    {
        Voxel rock = WorldMaterialCatalog.Rock;
        Voxel ice = WorldMaterialCatalog.Ice;
        Voxel core = WorldMaterialCatalog.Core;

        Assert.NotEqual((ushort)0, rock.ID);
        Assert.NotEqual((ushort)0, ice.ID);
        Assert.NotEqual((ushort)0, core.ID);
        Assert.NotEqual(rock.ID, ice.ID);
        Assert.NotEqual(rock.ID, core.ID);
        Assert.NotEqual(ice.ID, core.ID);

        //  The shared catalog derives ids identically to the client brick database's FNV1a rule, and
        //  every worldgen material is a block-shaped, non-luminous voxel.
        Assert.Equal(FNV1a.ComputeDataID("rock"), rock.ID);
        Assert.Equal(FNV1a.ComputeDataID("ice"), ice.ID);
        Assert.Equal(FNV1a.ComputeDataID("core"), core.ID);
        Assert.Equal((byte)0, rock.ShapeLight);
        Assert.Equal((byte)0, ice.Orientation);
    }

    [Fact]
    public void WorldGeneratorProducesCollidableStructuresForSeed()
    {
        GeneratedVoxelEntity[] world = new WorldGenerator(seed: 1337).Generate();

        Assert.Equal(20, world.Length);
        foreach (GeneratedVoxelEntity entity in world)
        {
            Assert.True(entity.Chunks.Length > 0, "Each generated structure should emit cold chunks.");

            //  Every structure must contain at least one solid voxel so it yields a collider.
            var solid = false;
            foreach (ChunkInfo chunkInfo in entity.Chunks)
            {
                foreach (Voxel voxel in chunkInfo.Chunk.Voxels)
                {
                    solid |= voxel.ID != 0;
                }
            }

            Assert.True(solid, "Generated structure should contain at least one solid voxel.");

            //  Authority/physics build consumes the same shared collider derivation as the client.
            Assert.NotEmpty(VoxelColliderBuilder.BuildCollition(entity.Chunks).Shapes);
        }
    }

    [Fact]
    public void WorldGeneratorIsDeterministicForSeed()
    {
        GeneratedVoxelEntity[] first = new WorldGenerator(seed: 1337).Generate();
        GeneratedVoxelEntity[] second = new WorldGenerator(seed: 1337).Generate();

        Assert.Equal(first.Length, second.Length);
        for (var i = 0; i < first.Length; i++)
        {
            Assert.Equal(Serialize(first[i]), Serialize(second[i]));
        }
    }

    [Fact]
    public void WorldGeneratorDiffersForDifferentSeeds()
    {
        byte[] first = SerializeAll(new WorldGenerator(seed: 1337).Generate());
        byte[] second = SerializeAll(new WorldGenerator(seed: 1338).Generate());

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void VoxelChunkWriterRecordsExpectedOffsetsAndVoxels()
    {
        var writer = new VoxelChunkWriter(chunkSize: 16);
        Voxel voxel = WorldMaterialCatalog.Core;

        writer.Set(0, 0, 0, voxel);
        writer.Set(-1, -1, -1, voxel);
        writer.Set(16, 0, 0, voxel);

        ChunkInfo[] chunks = writer.GetChunkInfos();
        Assert.Equal(3, chunks.Length);

        //  World coordinates map to chunk offsets -16>>4 = -1, 0>>4 = 0, 16>>4 = 1.
        Assert.Contains(chunks, c => c.OffsetX == 0 && c.OffsetY == 0 && c.OffsetZ == 0);
        Assert.Contains(chunks, c => c.OffsetX == -1 && c.OffsetY == -1 && c.OffsetZ == -1);
        Assert.Contains(chunks, c => c.OffsetX == 1 && c.OffsetY == 0 && c.OffsetZ == 0);

        //  Every emitted chunk holds exactly the one solid voxel placed and air elsewhere.
        foreach (ChunkInfo chunkInfo in chunks)
        {
            var solidCount = 0;
            foreach (Voxel v in chunkInfo.Chunk.Voxels)
            {
                if (v.ID != 0)
                {
                    solidCount++;
                }
            }

            Assert.Equal(1, solidCount);
        }
    }

    private static byte[] Serialize(in GeneratedVoxelEntity entity)
    {
        //  A fixed uuid keeps the comparison on transform + chunk data; generator-assigned uuids are
        //  intentionally random and not part of determinism.
        var data = new VoxelEntityData(
            _Uuid: 0,
            entity.Position.X,
            entity.Position.Y,
            entity.Position.Z,
            entity.Orientation.X,
            entity.Orientation.Y,
            entity.Orientation.Z,
            entity.Orientation.W,
            Vector3.One.X,
            Vector3.One.Y,
            Vector3.One.Z,
            entity.Chunks
        );
        return data.Serialize();
    }

    private static byte[] SerializeAll(in GeneratedVoxelEntity[] entities)
    {
        var buffer = new List<byte>();
        foreach (GeneratedVoxelEntity entity in entities)
        {
            buffer.AddRange(Serialize(entity));
        }

        return buffer.ToArray();
    }
}