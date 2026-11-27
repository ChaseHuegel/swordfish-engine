using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using Xunit;

namespace WaywardBeyond.Voxels.Tests;

/// <summary>Tests for <see cref="OrientationFlags"/>, sparse per-face overrides, and paint palette round-trips.</summary>
public class FaceOverrideTests
{
    [Fact]
    public void OrientationFlagsPacksAndUnpacksOrientationAndOverrideBit()
    {
        var orientation = new Orientation(pitch: 2, yaw: 3, roll: 1);
        var plain = new OrientationFlags(orientation, hasFaceOverrides: false);
        Assert.Equal(orientation, plain.Orientation);
        Assert.False(plain.HasFaceOverrides);

        OrientationFlags flagged = plain.WithFaceOverrides(true);
        Assert.Equal(orientation, flagged.Orientation);
        Assert.True(flagged.HasFaceOverrides);

        OrientationFlags cleared = flagged.WithFaceOverrides(false);
        Assert.Equal(orientation, cleared.Orientation);
        Assert.False(cleared.HasFaceOverrides);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void SparseOverridesWorkAcrossChunkSizesAndStayNullWhenUnpainted(byte chunkSize)
    {
        var voxels = new VoxelObject(chunkSize);
        int coord = chunkSize - 1;
        voxels.Set(coord, coord, coord, new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0));

        ChunkInfo[] unpainted = voxels.GetChunkInfos();
        Assert.Single(unpainted);
        Assert.Null(unpainted[0].Chunk.FaceOverrides);
        Assert.False(voxels.Get(coord, coord, coord).HasFaceOverrides());

        voxels.SetFaceTexture(coord, coord, coord, BrickFaceMask.Front, texturePaintId: 4);
        voxels.SetFaceColor(coord, coord, coord, BrickFaceMask.Front, colorPaintId: 7);

        Assert.True(voxels.Get(coord, coord, coord).HasFaceOverrides());
        Assert.True(voxels.TryGetFaceOverride(coord, coord, coord, BrickFace.Front, out FaceOverride front));
        Assert.Equal(4, front.TexturePaintId);
        Assert.Equal(7, front.ColorPaintId);
        Assert.False(voxels.TryGetFaceOverride(coord, coord, coord, BrickFace.Back, out _));

        //  Clearing only the color slot preserves the texture slot.
        voxels.SetFaceColor(coord, coord, coord, BrickFaceMask.Front, colorPaintId: 0);
        Assert.True(voxels.TryGetFaceOverride(coord, coord, coord, BrickFace.Front, out FaceOverride afterColorClear));
        Assert.Equal(4, afterColorClear.TexturePaintId);
        Assert.Equal(0, afterColorClear.ColorPaintId);
        Assert.True(voxels.Get(coord, coord, coord).HasFaceOverrides());

        //  Clearing the remaining texture slot removes the entry and clears the fast-path bit.
        voxels.SetFaceTexture(coord, coord, coord, BrickFaceMask.Front, texturePaintId: 0);
        Assert.False(voxels.TryGetFaceOverride(coord, coord, coord, BrickFace.Front, out _));
        Assert.False(voxels.Get(coord, coord, coord).HasFaceOverrides());
        Assert.Null(voxels.GetChunkInfos()[0].Chunk.FaceOverrides);
    }

    [Fact]
    public void OverwritingOrBreakingVoxelClearsItsFaceOverrides()
    {
        var voxels = new VoxelObject(16);
        voxels.Set(2, 3, 4, new Voxel(1, new ShapeLight(BrickShape.Block, 15), new Orientation(0, 1, 0)));
        voxels.SetFaceOverride(2, 3, 4, BrickFaceMask.Top | BrickFaceMask.Front, new FaceOverride(texturePaintId: 2, colorPaintId: 5));

        Assert.True(voxels.Get(2, 3, 4).HasFaceOverrides());

        voxels.Set(2, 3, 4, new Voxel());

        Assert.False(voxels.Get(2, 3, 4).HasFaceOverrides());
        Assert.False(voxels.TryGetFaceOverride(2, 3, 4, BrickFace.Top, out _));
        Assert.False(voxels.TryGetFaceOverride(2, 3, 4, BrickFace.Front, out _));
        Assert.Null(voxels.GetChunkInfos()[0].Chunk.FaceOverrides);
    }

    [Fact]
    public void CodecRoundTripsUnknownModdedPaintIdsLosslessly()
    {
        var bricks = BrickRegistry.FromNames(["wb:panel"]);
        var moddedPaints = PaintRegistry.FromNames(["mod:neon_trim", "mod:plasma_blue", "wb:panel", "wb:red"]);
        var vanillaPaints = PaintRegistry.FromNames(["wb:panel", "wb:red"]);

        var voxels = new VoxelObject(8);
        voxels.Set(1, 2, 3, new Voxel(bricks.Id("wb:panel"), new ShapeLight(BrickShape.Block, 15), 0));
        voxels.SetFaceOverride(
            1, 2, 3,
            BrickFaceMask.Front,
            new FaceOverride(moddedPaints.Id("mod:neon_trim"), moddedPaints.Id("mod:plasma_blue"))
        );
        voxels.SetFaceOverride(
            1, 2, 3,
            BrickFaceMask.Back,
            new FaceOverride(moddedPaints.Id("wb:panel"), moddedPaints.Id("wb:red"))
        );

        var moddedLocal = new VoxelEntityData(
            1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1,
            voxels.GetChunkInfos(),
            null,
            null
        );

        VoxelEntityData saved = VoxelCodec.Encode(in moddedLocal, bricks, moddedPaints).Value;
        Assert.NotNull(saved.PaintPalette);
        Assert.Contains("mod:neon_trim", saved.PaintPalette!);
        Assert.Contains("mod:plasma_blue", saved.PaintPalette!);

        //  Decode in vanilla (allocates extension IDs for unknown paints), re-encode, then decode back in modded.
        VoxelEntityData vanillaDecoded = VoxelCodec.Decode(in saved, bricks, vanillaPaints).Value;
        var vanillaVoxels = new VoxelObject(8, vanillaDecoded.Chunks);
        Assert.True(vanillaVoxels.Get(1, 2, 3).HasFaceOverrides());
        Assert.True(vanillaVoxels.TryGetFaceOverride(1, 2, 3, BrickFace.Front, out FaceOverride vanillaFront));
        Assert.Equal("mod:neon_trim", vanillaPaints.Name(vanillaFront.TexturePaintId));
        Assert.Equal("mod:plasma_blue", vanillaPaints.Name(vanillaFront.ColorPaintId));

        var vanillaLocal = new VoxelEntityData(
            1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1,
            vanillaVoxels.GetChunkInfos(),
            null,
            null
        );
        VoxelEntityData vanillaResaved = VoxelCodec.Encode(in vanillaLocal, bricks, vanillaPaints).Value;
        VoxelEntityData reloadedModded = VoxelCodec.Decode(in vanillaResaved, bricks, moddedPaints).Value;

        var reloadedVoxels = new VoxelObject(8, reloadedModded.Chunks);
        Assert.True(reloadedVoxels.TryGetFaceOverride(1, 2, 3, BrickFace.Front, out FaceOverride reloadedFront));
        Assert.Equal(moddedPaints.Id("mod:neon_trim"), reloadedFront.TexturePaintId);
        Assert.Equal(moddedPaints.Id("mod:plasma_blue"), reloadedFront.ColorPaintId);

        Assert.True(reloadedVoxels.TryGetFaceOverride(1, 2, 3, BrickFace.Back, out FaceOverride reloadedBack));
        Assert.Equal(moddedPaints.Id("wb:panel"), reloadedBack.TexturePaintId);
        Assert.Equal(moddedPaints.Id("wb:red"), reloadedBack.ColorPaintId);
    }

    [Fact]
    public void EmptyAirVoxelsIgnoreFaceOverridesOnWriteAndDeserialization()
    {
        var voxels = new VoxelObject(16);
        voxels.Set(1, 1, 1, new Voxel(0, 0, 0));
        voxels.SetFaceTexture(1, 1, 1, BrickFaceMask.Front, 5);
        voxels.SetFaceColor(1, 1, 1, BrickFaceMask.Front, 7);
        voxels.SetFaceOverride(1, 1, 1, BrickFaceMask.Top, new FaceOverride(3, 4));

        Assert.False(voxels.TryGetFaceOverride(1, 1, 1, BrickFace.Front, out _));
        Assert.False(voxels.TryGetFaceOverride(1, 1, 1, BrickFace.Top, out _));
        Assert.False(voxels.Get(1, 1, 1).HasFaceOverrides());

        var rawVoxels = new Voxel[16 * 16 * 16];
        rawVoxels[0] = new Voxel(0, 0, new OrientationFlags(0).WithFaceOverrides(true));
        rawVoxels[1] = new Voxel(1, 0, new OrientationFlags(0).WithFaceOverrides(true));
        var chunk = new Chunk(16, rawVoxels, [
            new FaceOverrideData(ChunkData.GetFaceKey(0, BrickFace.Front), 5, 7),
            new FaceOverrideData(ChunkData.GetFaceKey(1, BrickFace.Front), 0, 0),
        ]);
        var chunkData = new ChunkData(new Short3(0, 0, 0), chunk, voxels);
        Assert.False(chunkData.HasAnyFaceOverrides);
        Assert.False(chunkData.TryGetFaceOverride(0, BrickFace.Front, out _));
        Assert.False(chunkData.Data.Voxels[0].HasFaceOverrides());
        Assert.False(chunkData.TryGetFaceOverride(1, BrickFace.Front, out _));
        Assert.False(chunkData.Data.Voxels[1].HasFaceOverrides());
    }

    [Fact]
    public void DecodeWithoutPaintRegistryPreservesPaintPalette()
    {
        var bricks = BrickRegistry.FromNames(["wb:panel"]);
        var paints = PaintRegistry.FromNames(["wb:caution", "wb:red"]);
        var voxels = new VoxelObject(8);
        voxels.Set(1, 1, 1, new Voxel(bricks.Id("wb:panel"), new ShapeLight(BrickShape.Block, 15), 0));
        voxels.SetFaceOverride(1, 1, 1, BrickFaceMask.Front, new FaceOverride(paints.Id("wb:caution"), paints.Id("wb:red")));

        var local = new VoxelEntityData(1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, voxels.GetChunkInfos(), null, null);
        VoxelEntityData encoded = VoxelCodec.Encode(in local, bricks, paints).Value;
        Assert.NotNull(encoded.PaintPalette);

        VoxelEntityData bricksOnlyDecoded = VoxelCodec.Decode(in encoded, bricks, paintMap: null).Value;
        Assert.Null(bricksOnlyDecoded.BrickPalette);
        Assert.Equal(encoded.PaintPalette, bricksOnlyDecoded.PaintPalette);

        VoxelEntityData fullyDecoded = VoxelCodec.Decode(in bricksOnlyDecoded, bricks, paints).Value;
        Assert.Null(fullyDecoded.PaintPalette);
        var restored = new VoxelObject(8, fullyDecoded.Chunks);
        Assert.True(restored.TryGetFaceOverride(1, 1, 1, BrickFace.Front, out FaceOverride ov));
        Assert.Equal(paints.Id("wb:caution"), ov.TexturePaintId);
        Assert.Equal(paints.Id("wb:red"), ov.ColorPaintId);
    }
}
