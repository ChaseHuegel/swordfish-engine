using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using Xunit;

namespace Swordfish.Tests;

public class VoxelEntityDataCodecTests
{
    private static IBrickIdMap Map => BaseBrickCatalog.Registry;

    private static VoxelEntityData Make(ushort id)
    {
        var voxels = new Voxel[2];
        voxels[0] = new Voxel(id, 7, 0);
        var chunk = new Chunk(1, voxels);
        return new VoxelEntityData(0xBEEF, 1, 2, 3, 0, 0, 0, 1, 1, 1, 1, [new ChunkInfo(0, 0, 0, chunk)], _BrickPalette: null);
    }

    [Fact]
    public void EncodeToPaletteRecordsNamesForRegistryIds()
    {
        ushort rockId = Map.Id("wb:rock");
        VoxelEntityData source = Make(rockId);

        VoxelEntityData encoded = VoxelEntityDataCodec.EncodeToPalette(source, Map);

        //  A live structure already carries registry ids; the palette records the name for each.
        Assert.True(VoxelEntityDataCodec.HasPalette(encoded));
        Assert.Equal(rockId, encoded.Chunks[0].Chunk.Voxels[0].ID);
        Assert.Equal("wb:rock", encoded.BrickPalette[rockId]);
    }

    [Fact]
    public void EncodeToPaletteIsIdempotent()
    {
        VoxelEntityData first = VoxelEntityDataCodec.EncodeToPalette(Make(Map.Id("wb:ice")), Map);
        VoxelEntityData second = VoxelEntityDataCodec.EncodeToPalette(first, Map);

        Assert.Equal(first.BrickPalette!, second.BrickPalette!);
        Assert.Equal(first.Chunks[0].Chunk.Voxels[0].ID, second.Chunks[0].Chunk.Voxels[0].ID);
    }

    [Fact]
    public void DecodeToLocalResolvesPaletteNamesToMapIds()
    {
        ushort coreId = Map.Id("wb:core");
        VoxelEntityData encoded = VoxelEntityDataCodec.EncodeToPalette(Make(coreId), Map);

        VoxelEntityData local = VoxelEntityDataCodec.DecodeToLocal(encoded, Map);

        Assert.Equal(coreId, local.Chunks[0].Chunk.Voxels[0].ID);
        Assert.False(VoxelEntityDataCodec.HasPalette(local));
    }

    [Fact]
    public void DecodeToLocalPassesThroughStructureWithoutPalette()
    {
        ushort coreId = Map.Id("wb:core");
        VoxelEntityData source = Make(coreId);

        VoxelEntityData local = VoxelEntityDataCodec.DecodeToLocal(source, Map);

        //  No palette means the ids are already local registry ids.
        Assert.Equal(coreId, local.Chunks[0].Chunk.Voxels[0].ID);
        Assert.False(VoxelEntityDataCodec.HasPalette(local));
    }

    [Fact]
    public void UnknownBareFnvLegacyIdMapsToAir()
    {
        //  9999 has no base brick FNV mapping in the catalog.
        VoxelEntityData legacy = Make(9999);
        VoxelEntityData encoded = VoxelEntityDataCodec.EncodeLegacyToPalette(legacy);

        Assert.Equal((ushort)0, encoded.Chunks[0].Chunk.Voxels[0].ID);
    }

    [Fact]
    public void EncodeLegacyToPaletteMapsBareFnvIdToNamespacedName()
    {
        //  A data version 3 save stores FNV1a of the BARE name ("rock"), not the namespaced id.
        ushort bareRockId = FNV1a.ComputeDataID("rock");
        VoxelEntityData encoded = VoxelEntityDataCodec.EncodeLegacyToPalette(Make(bareRockId));

        Assert.True(VoxelEntityDataCodec.HasPalette(encoded));
        ushort rockRegistryId = Map.Id("wb:rock");
        Assert.Equal(rockRegistryId, encoded.Chunks[0].Chunk.Voxels[0].ID);
        Assert.Equal("wb:rock", encoded.BrickPalette[rockRegistryId]);
    }

    [Fact]
    public void RoundTripThroughSerialize()
    {
        ushort rockId = Map.Id("wb:rock");
        VoxelEntityData encoded = VoxelEntityDataCodec.EncodeToPalette(Make(rockId), Map);

        VoxelEntityData deserialized = VoxelEntityData.Deserialize(encoded.Serialize());
        Assert.True(VoxelEntityDataCodec.HasPalette(deserialized));
        Assert.Equal(encoded.BrickPalette!, deserialized.BrickPalette!);

        VoxelEntityData local = VoxelEntityDataCodec.DecodeToLocal(deserialized, Map);
        Assert.Equal(rockId, local.Chunks[0].Chunk.Voxels[0].ID);
    }
}