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

        ChunkInfo[] chunks = [new(0, 0, 0, new Chunk(2, voxels))];
        return new VoxelEntityData(
            1,
            0, 0, 0,
            0, 0, 0, 1,
            1, 1, 1,
            chunks,
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
