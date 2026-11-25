#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Migrations;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests.Blueprints;

/// <summary>Tests <see cref="FileBlueprintStorage"/> persistence, sanitization, versioning, and error handling.</summary>
public class FileBlueprintStorageTests
{
    [Fact]
    public void SaveListLoadRenameAndDeleteRoundTrip()
    {
        using var dir = new TempDirectory();
        FileBlueprintStorage storage = CreateStorage(dir.Path);

        VoxelRegionData encoded = CreateEncodedRegion();
        Result<BlueprintSummary> first = storage.Save("Starter Shuttle!", in encoded, brickCount: 2);
        Assert.True(first.Success, first.Message);
        Assert.Equal("starter-shuttle", first.Value.Id);
        Assert.Equal("Starter Shuttle!", first.Value.Name);
        Assert.Equal(2, first.Value.SizeX);
        Assert.Equal(1, first.Value.SizeY);
        Assert.Equal(1, first.Value.SizeZ);
        Assert.Equal(2, first.Value.BrickCount);

        Thread.Sleep(5);
        Result<BlueprintSummary> second = storage.Save("Starter Shuttle!", in encoded, brickCount: 2);
        Assert.True(second.Success, second.Message);
        Assert.Equal("starter-shuttle-2", second.Value.Id);

        Result<IReadOnlyList<BlueprintSummary>> listed = storage.List();
        Assert.True(listed.Success);
        Assert.Equal(2, listed.Value.Count);
        Assert.Equal(second.Value.Id, listed.Value[0].Id);
        Assert.Equal(first.Value.Id, listed.Value[1].Id);

        Result<BlueprintData> loaded = storage.Load(first.Value.Id);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal("Starter Shuttle!", loaded.Value.Name);
        Assert.Equal(DataVersion.CURRENT, loaded.Value.Version.DataVersion);
        Assert.Equal(2, loaded.Value.BrickCount);

        Result<BlueprintSummary> renamed = storage.Rename(first.Value.Id, "Cargo Pod");
        Assert.True(renamed.Success, renamed.Message);
        Assert.Equal("cargo-pod", renamed.Value.Id);
        Assert.Equal("Cargo Pod", renamed.Value.Name);
        Assert.False(storage.Load(first.Value.Id).Success);
        Assert.True(storage.Load("cargo-pod").Success);

        Assert.True(storage.Delete("cargo-pod").Success);
        Assert.False(storage.Load("cargo-pod").Success);
        Assert.False(storage.Delete("cargo-pod").Success);
    }

    [Fact]
    public void RejectsPathTraversalAndInvalidNames()
    {
        using var dir = new TempDirectory();
        FileBlueprintStorage storage = CreateStorage(dir.Path);
        VoxelRegionData encoded = CreateEncodedRegion();

        Assert.False(storage.Save("   ", in encoded, brickCount: 1).Success);
        Assert.False(storage.Save(new string('a', FileBlueprintStorage.MAX_NAME_LENGTH + 1), in encoded, brickCount: 1).Success);

        Assert.False(storage.Load("../secret").Success);
        Assert.False(storage.Load("sub/blueprint").Success);
        Assert.False(storage.Load(@"sub\blueprint").Success);
        Assert.False(storage.Rename("../secret", "Valid").Success);
        Assert.False(storage.Delete("../secret").Success);
    }

    [Fact]
    public void RejectsFutureFormatVersionsAndSkipsCorruptFilesOnList()
    {
        using var dir = new TempDirectory();
        FileBlueprintStorage storage = CreateStorage(dir.Path);
        VoxelRegionData encoded = CreateEncodedRegion();

        Result<BlueprintSummary> saved = storage.Save("Valid Ship", in encoded, brickCount: 2);
        Assert.True(saved.Success);

        string blueprintsDir = Path.Combine(dir.Path, "blueprints");
        var futureBlueprint = new BlueprintData(
            new Data.Version(DataVersion.CURRENT + 1, "Future", "Test"),
            "Future Ship",
            100,
            100,
            2,
            encoded.SizeX,
            encoded.SizeY,
            encoded.SizeZ,
            encoded.Chunks,
            encoded.BrickPalette,
            encoded.PaintPalette
        );
        File.WriteAllBytes(Path.Combine(blueprintsDir, "future-ship.wbp"), futureBlueprint.Serialize());
        File.WriteAllBytes(Path.Combine(blueprintsDir, "corrupt.wbp"), [1, 2, 3, 4]);

        Result<BlueprintData> futureLoad = storage.Load("future-ship");
        Assert.False(futureLoad.Success);

        Result<BlueprintData> corruptLoad = storage.Load("corrupt");
        Assert.False(corruptLoad.Success);

        Result<IReadOnlyList<BlueprintSummary>> listResult = storage.List();
        Assert.True(listResult.Success);
        Assert.Single(listResult.Value);
        Assert.Equal("Valid Ship", listResult.Value[0].Name);
    }

    [Fact]
    public void SavesAndLoadsMultiChunkBlueprintOver64KB()
    {
        using var dir = new TempDirectory();
        FileBlueprintStorage storage = CreateStorage(dir.Path);

        var bricks = BrickRegistry.FromNames(["wb:panel"]);
        var voxels = new VoxelObject(16);
        ushort panelId = bricks.Id("wb:panel");
        for (var z = 0; z < 8; z++)
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 32; x++)
        {
            voxels.Set(x, y, z, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));
        }

        VoxelRegionExtractResult extracted = VoxelRegionExtractor.Extract(voxels, new Int3(0, 0, 0), new Int3(31, 7, 7));
        VoxelRegionData encoded = VoxelCodec.Encode(extracted.Region, bricks);

        Result<BlueprintSummary> saved = storage.Save("Large Hangar", in encoded, extracted.BrickCount);
        Assert.True(saved.Success, saved.Message);
        Assert.Equal(32, saved.Value.SizeX);
        Assert.Equal(8, saved.Value.SizeY);
        Assert.Equal(8, saved.Value.SizeZ);

        Result<BlueprintData> loaded = storage.Load(saved.Value.Id);
        Assert.True(loaded.Success, loaded.Message);
        Assert.True(loaded.Value.Serialize().Length > 65_535);

        VoxelRegionData roundTrip = loaded.Value.ToRegion();
        Assert.Equal(32, roundTrip.SizeX);
        Assert.Equal(8, roundTrip.SizeY);
        Assert.Equal(8, roundTrip.SizeZ);
        Assert.Equal(32 * 8 * 8, VoxelRegionApplier.CountSolidVoxels(in roundTrip));
    }

    [Fact]
    public void RejectsOversizedRegionDimensionsAndOversizedFilesOnLoad()
    {
        using var dir = new TempDirectory();
        FileBlueprintStorage storage = CreateStorage(dir.Path);

        var oversizedEdge = new VoxelRegionData(FileBlueprintStorage.MAX_REGION_EDGE + 1, 1, 1, [], null, null);
        Assert.False(storage.Save("Too Wide", in oversizedEdge, 0).Success);

        var oversizedVolume = new VoxelRegionData(64, 64, 17, [], null, null);
        Assert.False(storage.Save("Too Dense", in oversizedVolume, 0).Success);

        string hugeFile = Path.Combine(dir.Path, "blueprints", "too-big" + FileBlueprintStorage.FILE_EXTENSION);
        Directory.CreateDirectory(Path.GetDirectoryName(hugeFile)!);
        using (FileStream fs = File.Create(hugeFile))
        {
            fs.SetLength(FileBlueprintStorage.MAX_FILE_BYTES + 1);
        }

        Result<BlueprintData> loaded = storage.Load("too-big");
        Assert.False(loaded.Success);
    }

    [Fact]
    public void ListUsesSummaryCacheAndInvalidatesOnExternalModification()
    {
        using var dir = new TempDirectory();
        FileBlueprintStorage storage = CreateStorage(dir.Path);
        VoxelRegionData encoded = CreateEncodedRegion();

        Result<BlueprintSummary> saved = storage.Save("Cached Hull", in encoded, 2);
        Assert.True(saved.Success, saved.Message);

        Result<IReadOnlyList<BlueprintSummary>> firstList = storage.List();
        Assert.True(firstList.Success);
        Assert.Single(firstList.Value);
        Assert.Equal("Cached Hull", firstList.Value[0].Name);

        string filePath = Path.Combine(dir.Path, "blueprints", saved.Value.Id + FileBlueprintStorage.FILE_EXTENSION);
        Result<BlueprintData> loaded = storage.Load(saved.Value.Id);
        Assert.True(loaded.Success);
        BlueprintData updated = new(
            loaded.Value.Version,
            "Externally Edited Hull",
            loaded.Value.CreatedAtMs,
            loaded.Value.ModifiedAtMs + 1000,
            loaded.Value.BrickCount,
            loaded.Value.SizeX,
            loaded.Value.SizeY,
            loaded.Value.SizeZ,
            loaded.Value.Chunks,
            loaded.Value.BrickPalette,
            loaded.Value.PaintPalette);
        File.WriteAllBytes(filePath, updated.Serialize());
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddSeconds(5));

        Result<IReadOnlyList<BlueprintSummary>> secondList = storage.List();
        Assert.True(secondList.Success);
        Assert.Single(secondList.Value);
        Assert.Equal("Externally Edited Hull", secondList.Value[0].Name);

        File.Delete(filePath);
        Result<IReadOnlyList<BlueprintSummary>> afterDelete = storage.List();
        Assert.True(afterDelete.Success);
        Assert.Empty(afterDelete.Value);
    }

    private static FileBlueprintStorage CreateStorage(string rootDir)
    {
        var config = new StorageConfig();
        AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(rootDir));
        var storagePaths = new StoragePaths(config, appPaths);
        return new FileBlueprintStorage(NullLogger<FileBlueprintStorage>.Instance, storagePaths, new Migrator([]));
    }

    private static VoxelRegionData CreateEncodedRegion()
    {
        var bricks = BrickRegistry.FromNames(["wb:panel"]);
        var voxels = new VoxelObject(16);
        ushort panelId = bricks.Id("wb:panel");
        voxels.Set(0, 0, 0, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));
        voxels.Set(1, 0, 0, new Voxel(panelId, new ShapeLight(BrickShape.Slope, 0), new Orientation(0, 1, 0)));
        VoxelRegionExtractResult extracted = VoxelRegionExtractor.Extract(voxels, new Int3(0, 0, 0), new Int3(1, 0, 0));
        return VoxelCodec.Encode(extracted.Region, bricks);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wb-bp-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
