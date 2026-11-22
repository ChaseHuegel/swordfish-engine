using System;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels.Tests;

/// <summary>Pins palette maintenance for the container and the persisted palette boundary.</summary>
public class VoxelPaletteTests
{
    [Fact]
    public void SetCountsTheNewIdAndTheEmptyVoxels()
    {
        var voxels = new VoxelObject(4);

        voxels.Set(0, 0, 0, new Voxel(5, 0, 0));

        VoxelPalette palette = Single(voxels).Palette;
        Assert.Equal(1, palette.Count(id: 5));
        Assert.Equal(63, palette.Count(id: 0));
        Assert.True(palette.Any(5));
        Assert.False(palette.Only(0));
    }

    [Fact]
    public void SetWithTheSameIdKeepsTheCounts()
    {
        var voxels = new VoxelObject(4);
        voxels.Set(0, 0, 0, new Voxel(5, 0, 0));

        voxels.Set(0, 0, 0, new Voxel(5, 0, 0));

        VoxelPalette palette = Single(voxels).Palette;
        Assert.Equal(1, palette.Count(id: 5));
        Assert.Equal(63, palette.Count(id: 0));
    }

    [Fact]
    public void SetWithTheEmptyIdRemovesThePreviousId()
    {
        var voxels = new VoxelObject(4);
        voxels.Set(0, 0, 0, new Voxel(5, 0, 0));

        voxels.Set(0, 0, 0, new Voxel());

        VoxelPalette palette = Single(voxels).Palette;
        Assert.False(palette.Any(5));
        Assert.Equal(0, palette.Count(id: 5));
        Assert.Equal(64, palette.Count(id: 0));
    }

    [Fact]
    public void AChunkOfEmptyVoxelsIsOnlyEmpty()
    {
        var voxels = new VoxelObject(4);
        voxels.Set(0, 0, 0, new Voxel());

        VoxelPalette palette = Single(voxels).Palette;

        Assert.True(palette.Only(0));
    }

    [Fact]
    public void DecrementOfAnUnknownIdDoesNotInsertAnEntry()
    {
        var palette = new VoxelPalette();

        palette.Decrement(7);

        Assert.False(palette.Any(7));
        Assert.Equal(0, palette.Count(id: 7));
        Assert.Equal(0, palette.Count());
    }

    private static ChunkData Single(VoxelObject voxels)
    {
        foreach (ChunkData chunk in voxels)
        {
            return chunk;
        }

        throw new InvalidOperationException("The container has no chunk.");
    }
}

/// <summary>Pins the palette boundary between local voxel ids and persisted brick names.</summary>
public class VoxelCodecTests
{
    private const ushort LOCAL_ROCK = 2;
    private const ushort LOCAL_ICE = 3;

    [Fact]
    public void EncodeAttachesANameForEachPresentId()
    {
        VoxelEntityData local = Structure((LOCAL_ROCK, 0));

        VoxelEntityData encoded = VoxelCodec.Encode(in local, Registry(localRock: LOCAL_ROCK));

        Assert.NotNull(encoded.BrickPalette);
        Assert.Equal(LOCAL_ROCK + 1, encoded.BrickPalette!.Length);
        Assert.Equal("wb:rock", encoded.BrickPalette[LOCAL_ROCK]);
    }

    [Fact]
    public void EncodeIsANoOpWhenAPaletteIsAlreadyPresent()
    {
        VoxelEntityData local = Structure((LOCAL_ROCK, 0));
        VoxelEntityData encoded = VoxelCodec.Encode(in local, Registry(localRock: LOCAL_ROCK));

        VoxelEntityData again = VoxelCodec.Encode(in encoded, Registry(localRock: LOCAL_ICE));

        Assert.Same(encoded.BrickPalette, again.BrickPalette);
    }

    [Fact]
    public void DecodeResolvesNamesIntoTheLocalIdSpace()
    {
        VoxelEntityData local = Local();
        VoxelEntityData encoded = VoxelCodec.Encode(in local, Registry(localRock: LOCAL_ROCK));

        VoxelEntityData decoded = VoxelCodec.Decode(in encoded, Registry(localRock: LOCAL_ICE));

        Assert.Null(decoded.BrickPalette);
        Assert.Equal(LOCAL_ICE, decoded.Chunks[0].Chunk.Voxels[0].ID);
    }

    [Fact]
    public void DecodeLeavesPaletteLessDataAlone()
    {
        VoxelEntityData local = Local();

        VoxelEntityData decoded = VoxelCodec.Decode(in local, Registry(localRock: LOCAL_ICE));

        Assert.Equal(LOCAL_ROCK, decoded.Chunks[0].Chunk.Voxels[0].ID);
    }

    [Fact]
    public void DecodeMapsADroppedPaletteEntryToTheEmptyVoxel()
    {
        VoxelEntityData local = Structure((LOCAL_ROCK, 0));
        VoxelEntityData encoded = VoxelCodec.Encode(in local, Registry(localRock: LOCAL_ROCK));

        //  Drop the palette entry the voxel refers to, which a corrupt record can do.
        encoded.BrickPalette![LOCAL_ROCK] = string.Empty;

        VoxelEntityData decoded = VoxelCodec.Decode(in encoded, Registry(localRock: LOCAL_ICE));

        Assert.Equal(0, decoded.Chunks[0].Chunk.Voxels[0].ID);
    }

    [Fact]
    public void ResolveAllocatesStableExtensionIdOutsideRegisteredSpace()
    {
        var registry = BrickRegistry.FromNames(["wb:rock", "wb:ice"]);
        int initialCount = registry.Count;
        ushort rockId = registry.Id("wb:rock");

        Assert.Equal(0, registry.Resolve(string.Empty));
        Assert.Equal(rockId, registry.Resolve("wb:rock"));

        ushort unknownFirst = registry.Resolve("mod:ore");
        ushort unknownSecond = registry.Resolve("mod:crystal");

        Assert.Equal(initialCount + 1, unknownFirst);
        Assert.Equal(initialCount + 2, unknownSecond);
        Assert.Equal(unknownFirst, registry.Resolve("mod:ore"));
        Assert.Equal(unknownSecond, registry.Resolve("mod:crystal"));

        Assert.Equal(initialCount, registry.Count);
        Assert.Equal(0, registry.Id("mod:ore"));
        Assert.Equal(0, registry.Id("mod:crystal"));
        Assert.Equal("mod:ore", registry.Name(unknownFirst));
        Assert.Equal("mod:crystal", registry.Name(unknownSecond));
    }

    [Fact]
    public void DecodeAndReEncodePreservesUnknownBrickNamesAndVoxelCount()
    {
        var moddedRegistry = BrickRegistry.FromNames(["wb:rock", "mod:ore", "mod:crystal"]);
        ushort moddedRockId = moddedRegistry.Id("wb:rock");
        ushort moddedOreId = moddedRegistry.Id("mod:ore");
        ushort moddedCrystalId = moddedRegistry.Id("mod:crystal");

        var moddedVoxels = new VoxelObject(2);
        moddedVoxels.Set(0, 0, 0, new Voxel(moddedRockId, new ShapeLight(BrickShape.Block, 15), 0));
        moddedVoxels.Set(1, 0, 0, new Voxel(moddedOreId, new ShapeLight(BrickShape.Slope, 12), 3));
        moddedVoxels.Set(0, 1, 0, new Voxel(moddedOreId, new ShapeLight(BrickShape.Stair, 8), 5));
        moddedVoxels.Set(0, 0, 1, new Voxel(moddedCrystalId, new ShapeLight(BrickShape.Slab, 10), 2));

        var moddedLocal = new VoxelEntityData(
            1,
            0, 0, 0,
            0, 0, 0, 1,
            1, 1, 1,
            moddedVoxels.GetChunkInfos(),
            null,
            null
        );
        VoxelEntityData moddedSaved = VoxelCodec.Encode(in moddedLocal, moddedRegistry);

        var vanillaRegistry = BrickRegistry.FromNames(["wb:rock"]);
        VoxelEntityData vanillaDecoded = VoxelCodec.Decode(in moddedSaved, vanillaRegistry);

        var vanillaVoxels = new VoxelObject(2, vanillaDecoded.Chunks);
        VoxelPalette vanillaPalette = SingleChunk(vanillaVoxels).Palette;
        ushort vanillaRockId = vanillaRegistry.Id("wb:rock");
        ushort vanillaOreId = vanillaRegistry.Resolve("mod:ore");
        ushort vanillaCrystalId = vanillaRegistry.Resolve("mod:crystal");

        Assert.Equal(1, vanillaPalette.Count(vanillaRockId));
        Assert.Equal(2, vanillaPalette.Count(vanillaOreId));
        Assert.Equal(1, vanillaPalette.Count(vanillaCrystalId));
        Assert.Equal(4, vanillaPalette.Count(id: 0));

        var vanillaLocal = new VoxelEntityData(
            1,
            0, 0, 0,
            0, 0, 0, 1,
            1, 1, 1,
            vanillaVoxels.GetChunkInfos(),
            null,
            null
        );
        VoxelEntityData vanillaSaved = VoxelCodec.Encode(in vanillaLocal, vanillaRegistry);

        Assert.NotNull(vanillaSaved.BrickPalette);
        Assert.Contains("wb:rock", vanillaSaved.BrickPalette!);
        Assert.Contains("mod:ore", vanillaSaved.BrickPalette!);
        Assert.Contains("mod:crystal", vanillaSaved.BrickPalette!);

        VoxelEntityData reloadedModded = VoxelCodec.Decode(in vanillaSaved, moddedRegistry);
        var reloadedVoxels = new VoxelObject(2, reloadedModded.Chunks);
        VoxelPalette reloadedPalette = SingleChunk(reloadedVoxels).Palette;

        Assert.Equal(1, reloadedPalette.Count(moddedRockId));
        Assert.Equal(2, reloadedPalette.Count(moddedOreId));
        Assert.Equal(1, reloadedPalette.Count(moddedCrystalId));
        Assert.Equal(4, reloadedPalette.Count(id: 0));
        Assert.Equal(new Voxel(moddedRockId, new ShapeLight(BrickShape.Block, 15), 0), reloadedVoxels.Get(0, 0, 0));
        Assert.Equal(new Voxel(moddedOreId, new ShapeLight(BrickShape.Slope, 12), 3), reloadedVoxels.Get(1, 0, 0));
        Assert.Equal(new Voxel(moddedOreId, new ShapeLight(BrickShape.Stair, 8), 5), reloadedVoxels.Get(0, 1, 0));
        Assert.Equal(new Voxel(moddedCrystalId, new ShapeLight(BrickShape.Slab, 10), 2), reloadedVoxels.Get(0, 0, 1));
    }

    private static ChunkData SingleChunk(VoxelObject voxels)
    {
        foreach (ChunkData chunk in voxels)
        {
            return chunk;
        }

        throw new InvalidOperationException("The container has no chunk.");
    }

    private static VoxelEntityData Local()
    {
        return Structure((LOCAL_ROCK, 0));
    }

    private static VoxelEntityData Structure(params (ushort Id, int Index)[] entries)
    {
        var voxels = new Voxel[8];
        foreach ((ushort id, int index) in entries)
        {
            voxels[index] = new Voxel(id, 0, 0);
        }

        ChunkInfo[] chunks = [new(0, 0, 0, new Chunk(2, voxels, null))];
        return new VoxelEntityData(
            1,
            0, 0, 0,
            0, 0, 0, 1,
            1, 1, 1,
            chunks,
            null,
            null
        );
    }

    private static IBrickRegistry Registry(ushort localRock)
    {
        return new FakeRegistry(localRock);
    }

    private sealed class FakeRegistry(ushort localRock) : IBrickRegistry
    {
        public ushort Id(string name)
        {
            return name switch
            {
                "wb:rock" => localRock,
                "wb:ice" => LOCAL_ICE,
                _ => 0,
            };
        }

        public string? Name(ushort id)
        {
            return id switch
            {
                LOCAL_ROCK => "wb:rock",
                LOCAL_ICE => "wb:ice",
                _ => null,
            };
        }
    }
}
