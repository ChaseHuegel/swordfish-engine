using System;
using System.Numerics;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using Xunit;

namespace Swordfish.Tests;

public class VoxelEntityDataCodecTests
{
    private static VoxelEntityData MakeLegacy(ushort id)
    {
        var voxels = new Voxel[2];
        voxels[0] = new Voxel(id, 7, 0);
        var chunk = new Chunk(1, voxels);
        return new VoxelEntityData(0xBEEF, 1, 2, 3, 0, 0, 0, 1, 1, 1, 1, [new ChunkInfo(0, 0, 0, chunk)], _BrickPalette: null);
    }

    [Fact]
    public void EncodeToPaletteReIndexesLegacyIdsIntoRegistryPalette()
    {
        ushort rockId = FNV1a.ComputeDataID("rock");
        VoxelEntityData legacy = MakeLegacy(rockId);

        VoxelEntityData encoded = VoxelEntityDataCodec.EncodeToPalette(legacy);

        //  A legacy (palette-less) FNV rock id becomes the stable registry id for rock.
        Assert.True(VoxelEntityDataCodec.HasPalette(encoded));
        Assert.True(BaseBrickCatalog.Registry.TryId("rock", out ushort rockRegistryId));
        Assert.Equal(rockRegistryId, encoded.Chunks[0].Chunk.Voxels[0].ID);
        Assert.Equal("rock", encoded.BrickPalette[rockRegistryId]);
    }

    [Fact]
    public void EncodeToPaletteIsIdempotent()
    {
        VoxelEntityData legacyIce = MakeLegacy(FNV1a.ComputeDataID("ice"));
        VoxelEntityData encodedOnce = VoxelEntityDataCodec.EncodeToPalette(legacyIce);
        VoxelEntityData encodedTwice = VoxelEntityDataCodec.EncodeToPalette(encodedOnce);

        Assert.Equal(encodedOnce.BrickPalette!, encodedTwice.BrickPalette!);
        Assert.Equal(encodedOnce.Chunks[0].Chunk.Voxels[0].ID, encodedTwice.Chunks[0].Chunk.Voxels[0].ID);
    }

    [Fact]
    public void DecodeToLocalRestoresLegacyFnvIdFromPalette()
    {
        ushort coreId = FNV1a.ComputeDataID("core");
        VoxelEntityData legacyCore = MakeLegacy(coreId);
        VoxelEntityData encoded = VoxelEntityDataCodec.EncodeToPalette(legacyCore);

        //  Resolving palette->name via BaseNameToLocalId restores the original FNV id.
        VoxelEntityData local = VoxelEntityDataCodec.DecodeToLocal(encoded, VoxelEntityDataCodec.BaseNameToLocalId);
        Assert.Equal(coreId, local.Chunks[0].Chunk.Voxels[0].ID);
        Assert.False(VoxelEntityDataCodec.HasPalette(local));
    }

    [Fact]
    public void DecodeToLocalPassesThroughLegacyStructureUnchanged()
    {
        ushort rockId = FNV1a.ComputeDataID("rock");
        VoxelEntityData legacy = MakeLegacy(rockId);

        VoxelEntityData local = VoxelEntityDataCodec.DecodeToLocal(legacy, VoxelEntityDataCodec.BaseNameToLocalId);

        //  No palette means the ids are already the local FNV scheme.
        Assert.Equal(rockId, local.Chunks[0].Chunk.Voxels[0].ID);
        Assert.False(VoxelEntityDataCodec.HasPalette(local));
    }

    [Fact]
    public void UnknownLegacyIdMapsToAir()
    {
        //  9999 has no base brick FNV mapping in the catalog.
        VoxelEntityData legacy = MakeLegacy(9999);
        VoxelEntityData encoded = VoxelEntityDataCodec.EncodeToPalette(legacy);

        Assert.Equal((ushort)0, encoded.Chunks[0].Chunk.Voxels[0].ID);
    }

    [Fact]
    public void RoundTripThroughSerialize()
    {
        ushort rockId = FNV1a.ComputeDataID("rock");
        VoxelEntityData legacyRock = MakeLegacy(rockId);
        VoxelEntityData encoded = VoxelEntityDataCodec.EncodeToPalette(legacyRock);

        VoxelEntityData deserialized = VoxelEntityData.Deserialize(encoded.Serialize());
        Assert.True(VoxelEntityDataCodec.HasPalette(deserialized));
        Assert.Equal(encoded.BrickPalette!, deserialized.BrickPalette!);

        VoxelEntityData local = VoxelEntityDataCodec.DecodeToLocal(deserialized, VoxelEntityDataCodec.BaseNameToLocalId);
        Assert.Equal(rockId, local.Chunks[0].Chunk.Voxels[0].ID);
    }
}