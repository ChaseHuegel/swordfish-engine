using System;
using System.Collections.Generic;
using System.Numerics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using Xunit;

namespace WaywardBeyond.Voxels.Tests;

/// <summary>Tests for region extraction, palette coding, 90-degree rotations, exhaustive mirroring, and application.</summary>
public class VoxelRegionTests
{
    [Fact]
    public void RegionLimitsValidateEdgesAndTotalVolume()
    {
        Assert.True(RegionLimits.IsValidSize(1, 1, 1));
        Assert.True(RegionLimits.IsValidSize(64, 32, 32));
        Assert.False(RegionLimits.IsValidSize(0, 1, 1));
        Assert.False(RegionLimits.IsValidSize(65, 1, 1));
        Assert.False(RegionLimits.IsValidSize(64, 64, 64));

        Assert.True(RegionLimits.IsValidBounds(new Int3(-5, -5, -5), new Int3(4, 4, 4)));
        Assert.False(RegionLimits.IsValidBounds(new Int3(5, 0, 0), new Int3(4, 0, 0)));
        Assert.False(RegionLimits.IsValidBounds(new Int3(0, 0, 0), new Int3(64, 0, 0)));
    }

    [Fact]
    public void ExtractAndApplyPreserveShapesOrientationsAndFaceOverrides()
    {
        var bricks = BrickRegistry.FromNames(["wb:panel", "wb:thruster"]);
        var paints = PaintRegistry.FromNames(["wb:caution", "wb:red", "wb:cyan"]);
        ushort panelId = bricks.Id("wb:panel");
        ushort thrusterId = bricks.Id("wb:thruster");
        ushort cautionId = paints.Id("wb:caution");
        ushort redId = paints.Id("wb:red");
        ushort cyanId = paints.Id("wb:cyan");

        var source = new VoxelObject(16);
        var slopeOrient = new Orientation(pitch: 1, yaw: 2, roll: 0);
        source.Set(-2, 15, 16, new Voxel(panelId, new ShapeLight(BrickShape.Slope, 12), slopeOrient));
        source.SetFaceOverride(-2, 15, 16, BrickFaceMask.Front, new FaceOverride(cautionId, redId));
        source.Set(0, 17, 17, new Voxel(thrusterId, new ShapeLight(BrickShape.Custom, 9), new Orientation(0, 1, 0)));
        source.SetFaceColor(0, 17, 17, BrickFaceMask.Back, cyanId);

        // Outside the selection box; must not be included.
        source.Set(5, 17, 17, new Voxel(panelId, new ShapeLight(BrickShape.Block, 15), 0));

        VoxelRegionExtractResult extracted = VoxelRegionExtractor.Extract(source, new Int3(0, 17, 17), new Int3(-2, 15, 16));
        Assert.Equal(2, extracted.BrickCount);
        Assert.Equal(3, extracted.Region.SizeX);
        Assert.Equal(3, extracted.Region.SizeY);
        Assert.Equal(2, extracted.Region.SizeZ);

        // Transient lighting is cleared to 0 on extraction.
        var extractedVoxels = new VoxelObject(16, extracted.Region.Chunks);
        Assert.Equal(0, extractedVoxels.Get(0, 0, 0).GetLightLevel());

        // Apply onto another structure with SkipAir: existing bricks at empty source cells stay intact.
        var target = new VoxelObject(16);
        target.Set(11, 11, 10, new Voxel(panelId, new ShapeLight(BrickShape.Block, 15), 0));
        var dirty = new HashSet<Short3>();
        int written = VoxelRegionApplier.ApplyTo(target, extracted.Region, new Int3(10, 10, 10), overwriteAir: false, dirty);
        Assert.Equal(2, written);
        Assert.NotEmpty(dirty);

        Assert.Equal(panelId, target.Get(11, 11, 10).ID);
        Voxel placedSlope = target.Get(10, 10, 10);
        Assert.Equal(panelId, placedSlope.ID);
        Assert.Equal(BrickShape.Slope, placedSlope.GetShapeLight().Shape);
        Assert.Equal(slopeOrient, placedSlope.GetOrientation());
        Assert.True(target.TryGetFaceOverride(10, 10, 10, BrickFace.Front, out FaceOverride slopeOv));
        Assert.Equal(cautionId, slopeOv.TexturePaintId);
        Assert.Equal(redId, slopeOv.ColorPaintId);

        // OverwriteAir clears existing brick where source has air.
        VoxelRegionApplier.ApplyTo(target, extracted.Region, new Int3(10, 10, 10), overwriteAir: true, dirty);
        Assert.Equal(0, target.Get(11, 11, 10).ID);
        Assert.Equal(thrusterId, target.Get(12, 12, 11).ID);
        Assert.True(target.TryGetFaceOverride(12, 12, 11, BrickFace.Back, out FaceOverride thrusterOv));
        Assert.Equal(cyanId, thrusterOv.ColorPaintId);
    }

    [Fact]
    public void RegionCodecRoundTripsBricksPaintsAndUnknownEntries()
    {
        var moddedBricks = BrickRegistry.FromNames(["wb:panel", "mod:hull"]);
        var moddedPaints = PaintRegistry.FromNames(["wb:red", "mod:neon"]);
        var vanillaBricks = BrickRegistry.FromNames(["wb:panel"]);
        var vanillaPaints = PaintRegistry.FromNames(["wb:red"]);

        var voxels = new VoxelObject(16);
        voxels.Set(0, 0, 0, new Voxel(moddedBricks.Id("wb:panel"), new ShapeLight(BrickShape.Block, 0), 0));
        voxels.Set(1, 0, 0, new Voxel(moddedBricks.Id("mod:hull"), new ShapeLight(BrickShape.Stair, 0), new Orientation(0, 1, 0)));
        voxels.SetFaceOverride(1, 0, 0, BrickFaceMask.Top, new FaceOverride(moddedPaints.Id("mod:neon"), moddedPaints.Id("wb:red")));

        VoxelRegionExtractResult extracted = VoxelRegionExtractor.Extract(voxels, new Int3(0, 0, 0), new Int3(1, 0, 0));
        VoxelRegionData encoded = VoxelCodec.Encode(extracted.Region, moddedBricks, moddedPaints);
        Assert.Contains("mod:hull", encoded.BrickPalette!);
        Assert.Contains("mod:neon", encoded.PaintPalette!);

        VoxelRegionData vanillaDecoded = VoxelCodec.Decode(encoded, vanillaBricks, vanillaPaints);
        var vanillaObj = new VoxelObject(16, vanillaDecoded.Chunks);
        Assert.Equal("mod:hull", vanillaBricks.Name(vanillaObj.Get(1, 0, 0).ID));
        Assert.True(vanillaObj.TryGetFaceOverride(1, 0, 0, BrickFace.Top, out FaceOverride vanillaOv));
        Assert.Equal("mod:neon", vanillaPaints.Name(vanillaOv.TexturePaintId));
        Assert.Equal("wb:red", vanillaPaints.Name(vanillaOv.ColorPaintId));
    }

    [Fact]
    public void FourQuarterRotationsAroundEachAxisReturnOriginalRegion()
    {
        var bricks = BrickRegistry.FromNames(["wb:panel", "wb:rock"]);
        var paints = PaintRegistry.FromNames(["wb:red", "wb:blue"]);
        ushort panelId = bricks.Id("wb:panel");
        ushort rockId = bricks.Id("wb:rock");
        ushort redId = paints.Id("wb:red");
        ushort blueId = paints.Id("wb:blue");

        var source = new VoxelObject(16);
        source.Set(0, 0, 0, new Voxel(panelId, new ShapeLight(BrickShape.Stair, 0), new Orientation(0, 1, 0)));
        source.SetFaceColor(0, 0, 0, BrickFaceMask.Front, redId);
        // Non-orientable block at non-cube dimensions (3x2x4).
        source.Set(2, 1, 3, new Voxel(rockId, new ShapeLight(BrickShape.Block, 0), Orientation.Identity));
        source.SetFaceColor(2, 1, 3, BrickFaceMask.Right, blueId);

        VoxelRegionData original = VoxelRegionExtractor.Extract(source, new Int3(0, 0, 0), new Int3(2, 1, 3)).Region;

        Orientation[] axes = [new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)];
        foreach (Orientation step in axes)
        {
            VoxelRegionData current = original;
            for (var i = 0; i < 4; i++)
            {
                current = VoxelRegionTransform.Transform(current, step, MirrorAxes.None);
            }

            AssertRegionsEqual(original, current);
        }
    }

    [Fact]
    public void RotationMapsNonOrientableFaceOverridesToRotatedWorldFace()
    {
        var bricks = BrickRegistry.FromNames(["wb:rock"]);
        var paints = PaintRegistry.FromNames(["wb:red"]);
        ushort rockId = bricks.Id("wb:rock");
        ushort redId = paints.Id("wb:red");

        var source = new VoxelObject(16);
        source.Set(0, 0, 0, new Voxel(rockId, new ShapeLight(BrickShape.Block, 0), Orientation.Identity));
        source.SetFaceColor(0, 0, 0, BrickFaceMask.Front, redId);

        VoxelRegionData region = VoxelRegionExtractor.Extract(source, new Int3(0, 0, 0), new Int3(0, 0, 0)).Region;
        // Yaw +90 rotates +Z (Front) to +X (Right) in ChunkData/Orientation convention.
        VoxelRegionData rotated = VoxelRegionTransform.Transform(region, new Orientation(pitch: 0, yaw: 1, roll: 0), MirrorAxes.None);

        var obj = new VoxelObject(16, rotated.Chunks);
        Assert.Equal(Orientation.Identity, obj.Get(0, 0, 0).GetOrientation());
        Assert.True(obj.TryGetFaceOverride(0, 0, 0, BrickFace.Right, out FaceOverride rightOv));
        Assert.Equal(redId, rightOv.ColorPaintId);
        Assert.False(obj.TryGetFaceOverride(0, 0, 0, BrickFace.Front, out _));
    }

    [Fact]
    public void ExhaustiveMirrorAcrossAll24OrientationsAll3AxesAndAll6FacesPreservesNormalsAndIsInvolution()
    {
        var canonicalOrientations = GetAll24CanonicalOrientations();
        Assert.Equal(24, canonicalOrientations.Count);

        MirrorAxes[] mirrorAxes = [MirrorAxes.X, MirrorAxes.Y, MirrorAxes.Z];

        foreach (Orientation orient in canonicalOrientations)
        {
            foreach (MirrorAxes axis in mirrorAxes)
            {
                for (var f = 0; f < 6; f++)
                {
                    var face = (BrickFace)f;
                    var source = new VoxelObject(16);
                    source.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Slope, 0), orient));
                    source.SetFaceColor(0, 0, 0, face.ToMask(), colorPaintId: 42);

                    VoxelRegionData region = VoxelRegionExtractor.Extract(source, new Int3(0, 0, 0), new Int3(0, 0, 0)).Region;
                    VoxelRegionData mirrored = VoxelRegionTransform.Transform(region, Orientation.Identity, axis);

                    var mirroredObj = new VoxelObject(16, mirrored.Chunks);
                    Voxel mirroredVoxel = mirroredObj.Get(0, 0, 0);
                    Orientation mirroredOrient = mirroredVoxel.GetOrientation();

                    // Find the single face carrying the paint override after mirroring.
                    BrickFace? mappedFace = null;
                    for (var mf = 0; mf < 6; mf++)
                    {
                        if (mirroredObj.TryGetFaceOverride(0, 0, 0, (BrickFace)mf, out FaceOverride ov) && ov.ColorPaintId == 42)
                        {
                            Assert.Null(mappedFace);
                            mappedFace = (BrickFace)mf;
                        }
                    }

                    Assert.NotNull(mappedFace);

                    // Verify reflected world-space normal matches transformed face normal.
                    Vector3 originalWorldNormal = Vector3.Transform(face.ToLocalNormal(), orient.ToQuaternion());
                    Vector3 expectedReflectedNormal = axis switch
                    {
                        MirrorAxes.X => new Vector3(-originalWorldNormal.X, originalWorldNormal.Y, originalWorldNormal.Z),
                        MirrorAxes.Y => new Vector3(originalWorldNormal.X, -originalWorldNormal.Y, originalWorldNormal.Z),
                        MirrorAxes.Z => new Vector3(originalWorldNormal.X, originalWorldNormal.Y, -originalWorldNormal.Z),
                        _ => originalWorldNormal,
                    };
                    Vector3 actualWorldNormal = Vector3.Transform(mappedFace!.Value.ToLocalNormal(), mirroredOrient.ToQuaternion());
                    Assert.True(
                        Vector3.Distance(expectedReflectedNormal, actualWorldNormal) < 1e-4f,
                        $"Orient={orient}, Axis={axis}, Face={face}: expected normal {expectedReflectedNormal}, got {actualWorldNormal}"
                    );

                    // Verify involution: mirroring twice along the same axis yields exact original region.
                    VoxelRegionData twice = VoxelRegionTransform.Transform(mirrored, Orientation.Identity, axis);
                    AssertRegionsEqual(region, twice);
                }
            }
        }
    }

    [Fact]
    public void ClearRemovesVoxelsAndOverridesInsideBoundsOnly()
    {
        var source = new VoxelObject(16);
        source.Set(1, 1, 1, new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0));
        source.SetFaceColor(1, 1, 1, BrickFaceMask.Top, 3);
        source.Set(2, 1, 1, new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0));
        source.Set(3, 1, 1, new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0));

        var dirty = new HashSet<Short3>();
        int cleared = VoxelRegionApplier.Clear(source, new Int3(1, 1, 1), new Int3(2, 1, 1), dirty);

        Assert.Equal(2, cleared);
        Assert.Equal(0, source.Get(1, 1, 1).ID);
        Assert.False(source.TryGetFaceOverride(1, 1, 1, BrickFace.Top, out _));
        Assert.Equal(0, source.Get(2, 1, 1).ID);
        Assert.Equal(1, source.Get(3, 1, 1).ID);
        Assert.True(VoxelRegionApplier.HasSolidVoxels(source));
    }

    [Fact]
    public void ToCenteredVoxelObjectShiftsByHalfDimensions()
    {
        var source = new VoxelObject(16);
        source.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Block, 0), 0));
        source.Set(3, 1, 2, new Voxel(2, new ShapeLight(BrickShape.Block, 0), 0));

        VoxelRegionData region = VoxelRegionExtractor.Extract(source, new Int3(0, 0, 0), new Int3(3, 1, 2)).Region;
        VoxelObject centered = VoxelRegionApplier.ToCenteredVoxelObject(region, out Int3 centerOffset);

        Assert.Equal(new Int3(-2, -1, -1), centerOffset);
        Assert.Equal(1, centered.Get(-2, -1, -1).ID);
        Assert.Equal(2, centered.Get(1, 0, 1).ID);
        Assert.Equal(2, VoxelRegionApplier.CountSolidVoxels(region));
    }

    [Fact]
    public void CodecHandlesNullChunksWithoutThrowing()
    {
        var bricks = BrickRegistry.FromNames(["wb:panel"]);
        var paints = PaintRegistry.FromNames(["wb:red"]);

        var emptyRegion = new VoxelRegionData(1, 1, 1, null!, null, null);
        VoxelRegionData encodedEmpty = VoxelCodec.Encode(emptyRegion, bricks, paints);
        Assert.Null(encodedEmpty.BrickPalette);

        var palettedNullRegion = new VoxelRegionData(1, 1, 1, null!, [string.Empty, "wb:panel"], [string.Empty, "wb:red"]);
        VoxelRegionData decodedRegion = VoxelCodec.Decode(palettedNullRegion, bricks, paints);
        Assert.NotNull(decodedRegion.Chunks);
        Assert.Empty(decodedRegion.Chunks);

        var emptyEntity = new VoxelEntityData(1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, null!, null, null);
        VoxelEntityData encodedEntity = VoxelCodec.Encode(emptyEntity, bricks, paints).Value;
        Assert.Null(encodedEntity.BrickPalette);

        var palettedNullEntity = new VoxelEntityData(
            1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, null!, [string.Empty, "wb:panel"], [string.Empty, "wb:red"]);
        VoxelEntityData decodedEntity = VoxelCodec.Decode(palettedNullEntity, bricks, paints).Value;
        Assert.NotNull(decodedEntity.Chunks);
        Assert.Empty(decodedEntity.Chunks);
    }

    [Fact]
    public void RegionLimitsDetectPasteOverflowAndChunkRange()
    {
        Assert.True(RegionLimits.TryComputePasteMax(new Int3(int.MaxValue - 2, 0, 0), 3, 1, 1, out Int3 fitMax));
        Assert.Equal(new Int3(int.MaxValue, 0, 0), fitMax);

        Assert.False(RegionLimits.TryComputePasteMax(new Int3(int.MaxValue, 0, 0), 2, 1, 1, out _));
        Assert.False(RegionLimits.TryComputePasteMax(new Int3(0, int.MinValue, 0), 1, 0, 1, out _));

        Assert.True(RegionLimits.IsWithinChunkRange(new Int3(-524_288, 0, 0), new Int3(524_287, 0, 0), 16));
        Assert.False(RegionLimits.IsWithinChunkRange(new Int3(524_288, 0, 0), new Int3(524_288, 0, 0), 16));
        Assert.False(RegionLimits.IsWithinChunkRange(new Int3(-524_289, 0, 0), new Int3(-524_289, 0, 0), 16));
        Assert.False(RegionLimits.IsWithinChunkRange(default, default, 0));
    }

    [Fact]
    public void MarkAffectedChunksDoesNotWrapAtShortBoundaries()
    {
        var dirtyMin = new HashSet<Short3>();
        VoxelRegionApplier.MarkAffectedChunks(-524_288, 0, 0, 16, dirtyMin);
        Assert.Contains(new Short3(short.MinValue, 0, 0), dirtyMin);
        Assert.DoesNotContain(new Short3(short.MaxValue, 0, 0), dirtyMin);

        var dirtyMax = new HashSet<Short3>();
        VoxelRegionApplier.MarkAffectedChunks(524_287, 0, 0, 16, dirtyMax);
        Assert.Contains(new Short3(short.MaxValue, 0, 0), dirtyMax);
        Assert.DoesNotContain(new Short3(short.MinValue, 0, 0), dirtyMax);
    }

    [Fact]
    public void ApplyToWithOverwriteAirCountsClearedSolidBricks()
    {
        var bricks = BrickRegistry.FromNames(["wb:panel"]);
        ushort panelId = bricks.Id("wb:panel");
        var target = new VoxelObject(16);
        target.Set(0, 0, 0, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));
        target.Set(1, 0, 0, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));

        var emptyRegion = new VoxelRegionData(2, 1, 1, [], null, null);
        int affected = VoxelRegionApplier.ApplyTo(target, emptyRegion, new Int3(0, 0, 0), overwriteAir: true);
        Assert.Equal(2, affected);
        Assert.Equal(0, target.Get(0, 0, 0).ID);
        Assert.Equal(0, target.Get(1, 0, 0).ID);
    }

    private static List<Orientation> GetAll24CanonicalOrientations()
    {
        var result = new List<Orientation>();
        var seen = new HashSet<byte>();

        for (byte b = 0; b < 64; b++)
        {
            var raw = new Orientation(b);
            var canonical = new Orientation(raw.ToQuaternion());
            if (seen.Add(canonical.ToByte()))
            {
                result.Add(canonical);
            }
        }

        return result;
    }

    private static void AssertRegionsEqual(in VoxelRegionData expected, in VoxelRegionData actual)
    {
        Assert.Equal(expected.SizeX, actual.SizeX);
        Assert.Equal(expected.SizeY, actual.SizeY);
        Assert.Equal(expected.SizeZ, actual.SizeZ);

        var expectedObj = new VoxelObject(16, expected.Chunks);
        var actualObj = new VoxelObject(16, actual.Chunks);

        for (var z = 0; z < expected.SizeZ; z++)
        for (var y = 0; y < expected.SizeY; y++)
        for (var x = 0; x < expected.SizeX; x++)
        {
            Voxel ev = expectedObj.Get(x, y, z);
            Voxel av = actualObj.Get(x, y, z);
            Assert.Equal(ev.ID, av.ID);
            Assert.Equal(ev.GetShapeLight().Shape, av.GetShapeLight().Shape);
            Assert.Equal(ev.GetOrientation(), av.GetOrientation());
            Assert.Equal(ev.HasFaceOverrides(), av.HasFaceOverrides());

            for (var f = 0; f < 6; f++)
            {
                var face = (BrickFace)f;
                bool hasE = expectedObj.TryGetFaceOverride(x, y, z, face, out FaceOverride eOv);
                bool hasA = actualObj.TryGetFaceOverride(x, y, z, face, out FaceOverride aOv);
                Assert.Equal(hasE, hasA);
                if (hasE)
                {
                    Assert.Equal(eOv, aOv);
                }
            }
        }
    }
}
