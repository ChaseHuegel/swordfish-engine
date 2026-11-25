using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Blueprints;
using WaywardBeyond.AssetTools.Diagnostics;
using WaywardBeyond.AssetTools.Packs;
using WaywardBeyond.AssetTools.Validation;
using WaywardBeyond.AssetTools.Workspace;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Voxels;
using DataVersionType = WaywardBeyond.Data.Version;

namespace WaywardBeyond.AssetTools.Tests.Packs;

/// <summary>
///     Verifies pack versioning, staged export, sanitized diagnostics zips, and pack blueprint management.
/// </summary>
public sealed class PacksDiagnosticsAndBlueprintsTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary directory for pack, diagnostics, and blueprint tests.
    /// </summary>
    public PacksDiagnosticsAndBlueprintsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-packs-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void PackVersionAssessmentHandlesAllCompatibilityCases()
    {
        var metaV1 = new PackMetadata("p", "ns", ContentVersion: 1, "0.1.0", DateTimeOffset.UtcNow);
        var metaV2 = new PackMetadata("p", "ns", ContentVersion: 2, "0.2.0", DateTimeOffset.UtcNow);

        PackVersionAssessment repo = PackVersionAssessment.Assess(WorkspaceMode.Repo, null, toolContentVersion: 1);
        Assert.Equal(PackVersionStatus.NotApplicableRepo, repo.Status);
        Assert.False(repo.IsReadOnly);

        PackVersionAssessment unversioned = PackVersionAssessment.Assess(WorkspaceMode.Pack, null, toolContentVersion: 1);
        Assert.Equal(PackVersionStatus.UnversionedWarning, unversioned.Status);
        Assert.False(unversioned.IsReadOnly);

        PackVersionAssessment newer = PackVersionAssessment.Assess(WorkspaceMode.Pack, metaV2, toolContentVersion: 1);
        Assert.Equal(PackVersionStatus.NewerReadOnly, newer.Status);
        Assert.True(newer.IsReadOnly);

        PackVersionAssessment older = PackVersionAssessment.Assess(WorkspaceMode.Pack, metaV1, toolContentVersion: 2);
        Assert.Equal(PackVersionStatus.OlderWarning, older.Status);
        Assert.False(older.IsReadOnly);

        PackVersionAssessment exact = PackVersionAssessment.Assess(WorkspaceMode.Pack, metaV1, toolContentVersion: 1);
        Assert.Equal(PackVersionStatus.Compatible, exact.Status);
        Assert.False(exact.IsReadOnly);
    }

    [Fact]
    public void PackExporterBlocksOnBrokenStampsStagedPackAndLeavesWorkingTreeUntouched()
    {
        string packDir = Path.Combine(_tempRoot, "export-pack");
        string outputDir = Path.Combine(_tempRoot, "out");
        var scaffolder = new PackScaffolder();
        Result<PackMetadata> created = scaffolder.CreatePack(packDir, "my-pack", "mp", "My Pack", "Author", "0.1.0");
        Assert.True(created.Success);

        string workingPackTomlPath = Path.Combine(packDir, PackMetadata.FILE_NAME);
        byte[] originalPackTomlBytes = File.ReadAllBytes(workingPackTomlPath);

        //  Add a broken brick referencing missing texture to test blocking.
        string badBrickPath = Path.Combine(packDir, "assets", "bricks", "bad.toml");
        File.WriteAllText(badBrickPath, "[[Bricks]]\nID = \"mp:bad\"\nShape = \"block\"\n[Bricks.Textures]\nDefault = [\"missing_tex\"]\n");

        var loader = new WorkspaceLoader();
        var exporter = new PackExporter();
        WorkspaceIndex brokenWs = loader.LoadPack(packDir).Value;
        PackExportOutcome blocked = exporter.Export(brokenWs, outputDir, "0.9.0", format: PackArchiveFormat.Zip);

        Assert.False(blocked.Success);
        Assert.True(blocked.BlockedByValidation);
        Assert.Null(blocked.ArchivePath);

        //  Fix the brick by supplying the texture and export both .zip and .tar.gz!
        File.WriteAllBytes(Path.Combine(packDir, "assets", "textures", "block", "missing_tex.png"), [1, 2, 3]);
        WorkspaceIndex cleanWs = loader.LoadPack(packDir).Value;

        PackExportOutcome zipExport = exporter.Export(cleanWs, outputDir, "0.9.0", format: PackArchiveFormat.Zip);
        Assert.True(zipExport.Success, zipExport.ErrorMessage);
        Assert.EndsWith("my-pack-1.zip", zipExport.ArchivePath, StringComparison.Ordinal);
        Assert.True(File.Exists(zipExport.ArchivePath));

        using (ZipArchive zip = ZipFile.OpenRead(zipExport.ArchivePath!))
        {
            ZipArchiveEntry? stagedPackEntry = zip.GetEntry("pack.toml");
            Assert.NotNull(stagedPackEntry);
            using var reader = new StreamReader(stagedPackEntry.Open());
            string stagedToml = reader.ReadToEnd();
            Assert.Contains("ToolVersion    = \"0.9.0\"", stagedToml, StringComparison.Ordinal);
        }

        //  Working tree pack.toml MUST be untouched!
        Assert.Equal(originalPackTomlBytes, File.ReadAllBytes(workingPackTomlPath));

        //  Auto format always defaults to .zip on both Linux and Windows.
        PackExportOutcome autoExport = exporter.Export(cleanWs, outputDir, "0.9.0", format: PackArchiveFormat.Auto);
        Assert.True(autoExport.Success, autoExport.ErrorMessage);
        Assert.EndsWith("my-pack-1.zip", autoExport.ArchivePath, StringComparison.Ordinal);
        Assert.True(File.Exists(autoExport.ArchivePath));
        using (ZipArchive autoZip = ZipFile.OpenRead(autoExport.ArchivePath!))
        {
            Assert.NotNull(autoZip.GetEntry("pack.toml"));
        }

        PackExportOutcome tarExport = exporter.Export(cleanWs, outputDir, "0.9.0", format: PackArchiveFormat.TarGz);
        Assert.True(tarExport.Success, tarExport.ErrorMessage);
        Assert.EndsWith("my-pack-1.tar.gz", tarExport.ArchivePath, StringComparison.Ordinal);
        Assert.True(File.Exists(tarExport.ArchivePath));

        using FileStream fs = File.OpenRead(tarExport.ArchivePath!);
        using var gzip = new GZipStream(fs, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzip);
        List<string> tarEntries = [];
        while (tarReader.GetNextEntry() is TarEntry tarEntry)
        {
            tarEntries.Add(tarEntry.Name);
        }

        Assert.Contains(tarEntries, static e => e.EndsWith("pack.toml", StringComparison.Ordinal));
    }

    [Fact]
    public void DiagnosticsBundleExcludesContentFilesAndRedactsExternalPaths()
    {
        string workspaceRoot = Path.Combine(_tempRoot, "ws-root");
        string outsideFile = "/home/private-user/secrets/token.txt";
        string logPath = Path.Combine(_tempRoot, "studio.log");
        Directory.CreateDirectory(workspaceRoot);

        File.WriteAllLines(
            logPath,
            [
                $"Loaded workspace {workspaceRoot}/assets/bricks/core.toml",
                $"Failed reading external path {outsideFile} and C:\\Users\\Private\\secret.cfg"
            ]);

        var report = new ValidationReport(
        [
            new ValidationFinding("REF001", ValidationSeverity.Broken, "pack", Path.Combine(workspaceRoot, "assets", "bricks", "core.toml"), 3, 1, "Textures", $"Missing file near {outsideFile}")
        ]);

        string zipPath = Path.Combine(_tempRoot, "diagnostics.zip");
        var input = new DiagnosticsBundleInput(
            ToolVersion: "0.1.0",
            ContentVersion: ContentVersion.CURRENT,
            WorkspaceMode: WorkspaceMode.Pack,
            WorkspaceRoots: [workspaceRoot],
            GlVendor: "Mesa",
            GlRenderer: "llvmpipe",
            AvaloniaVersion: "11.3.23",
            LogFilePath: logPath,
            ValidationReport: report,
            RecentErrors: [$"Error accessing {outsideFile}"]);

        Result writeResult = new DiagnosticsBundleWriter().WriteZip(zipPath, in input);
        Assert.True(writeResult.Success, writeResult.Message);

        using ZipArchive zip = ZipFile.OpenRead(zipPath);
        string[] names = [.. zip.Entries.Select(static e => e.FullName).OrderBy(static n => n, StringComparer.Ordinal)];
        Assert.Equal(["about.json", "log-tail.txt", "recent-errors.txt", "validation-summary.txt"], names);

        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            string text = reader.ReadToEnd();
            Assert.DoesNotContain("private-user", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:\\Users\\Private", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PackBlueprintManagerSupportsCrudInPlaceRenameMoveAndPaletteReencoding()
    {
        string packBpDir = Path.Combine(_tempRoot, "pack", "assets", "blueprints");
        string playerLibDir = Path.Combine(_tempRoot, "player-library");
        Directory.CreateDirectory(playerLibDir);

        BrickRegistry sourceBricks = BrickRegistry.FromNames(["wb:alpha", "wb:core"]);
        BrickRegistry targetBricks = BrickRegistry.FromNames(["wb:core", "wb:zeta"]);
        PaintRegistry sourcePaints = PaintRegistry.FromNames(["wb:blue", "wb:red"]);
        PaintRegistry targetPaints = PaintRegistry.FromNames(["wb:red", "wb:yellow"]);

        ushort srcCoreId = sourceBricks.Id("wb:core");
        ushort srcRedId = sourcePaints.Id("wb:red");
        ushort dstCoreId = targetBricks.Id("wb:core");
        ushort dstRedId = targetPaints.Id("wb:red");
        Assert.NotEqual(srcCoreId, dstCoreId);
        Assert.NotEqual(srcRedId, dstRedId);

        var voxels = new Voxel[1];
        voxels[0] = new Voxel(srcCoreId, 0, 0);
        var overrides = new FaceOverrideData[] { new(0, 0, srcRedId) };
        var localRegion = new VoxelRegionData(1, 1, 1, [new ChunkInfo(0, 0, 0, new Chunk(1, voxels, overrides))], null, null);
        VoxelRegionData playerEncoded = VoxelCodec.Encode(in localRegion, sourceBricks, sourcePaints);

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var playerBlueprint = new BlueprintData(
            new DataVersionType(DataVersion.CURRENT, "Wayward Beyond", "Blueprint"),
            "Starter Pod",
            now,
            now,
            1,
            playerEncoded.SizeX,
            playerEncoded.SizeY,
            playerEncoded.SizeZ,
            playerEncoded.Chunks,
            playerEncoded.BrickPalette,
            playerEncoded.PaintPalette);

        string playerFile = Path.Combine(playerLibDir, "starter-pod.wbp");
        File.WriteAllBytes(playerFile, playerBlueprint.Serialize());

        var manager = new PackBlueprintManager(packBpDir);

        //  Import from player library re-encodes palette cleanly and keeps display name.
        Result<PackBlueprintEntry> imported = manager.ImportFromPlayerLibrary(
            playerFile,
            "ships/starter",
            sourceBricks,
            targetBricks,
            sourcePaints,
            targetPaints);
        Assert.True(imported.Success, imported.Message);
        Assert.Equal("ships/starter", imported.Value.RelativeId);
        Assert.Equal("Starter Pod", imported.Value.Name);

        BlueprintData loadedImported = manager.Load("ships/starter").Value;
        VoxelRegionData importedRegion = loadedImported.ToRegion();
        VoxelRegionData decodedForTarget = VoxelCodec.Decode(in importedRegion, targetBricks, targetPaints);
        Assert.Equal(dstCoreId, decodedForTarget.Chunks[0].Chunk.Voxels[0].ID);
        Assert.NotNull(decodedForTarget.Chunks[0].Chunk.FaceOverrides);
        Assert.Equal(dstRedId, decodedForTarget.Chunks[0].Chunk.FaceOverrides![0].ColorPaintId);

        //  In-place display rename keeps file path / pack ID identical.
        Result<PackBlueprintEntry> renamed = manager.RenameDisplayName("ships/starter", "Starter Pod Mk II");
        Assert.True(renamed.Success, renamed.Message);
        Assert.Equal("ships/starter", renamed.Value.RelativeId);
        Assert.Equal("Starter Pod Mk II", renamed.Value.Name);

        //  Move changes pack ID and returns warning notice.
        Result<PackBlueprintMoveResult> moved = manager.Move("ships/starter", "ships/starter_mk2");
        Assert.True(moved.Success, moved.Message);
        Assert.Equal("ships/starter", moved.Value.OldRelativeId);
        Assert.Equal("ships/starter_mk2", moved.Value.NewRelativeId);
        Assert.Contains("ships/starter_mk2", moved.Value.IdChangeNotice, StringComparison.Ordinal);

        //  Duplicate and export back to player library.
        Result<PackBlueprintEntry> dup = manager.Duplicate("ships/starter_mk2", "ships/copy", "Copy Pod");
        Assert.True(dup.Success, dup.Message);
        Assert.Equal(2, manager.List().Value.Count);

        Result<string> exportedPath = manager.ExportToPlayerLibrary(
            "ships/copy",
            playerLibDir,
            targetBricks,
            sourceBricks,
            targetPaints,
            sourcePaints);
        Assert.True(exportedPath.Success, exportedPath.Message);
        Assert.True(File.Exists(exportedPath.Value));

        //  Delete and limit validation.
        Assert.True(manager.Delete("ships/copy").Success);
        Assert.Single(manager.List().Value);
        Assert.False(manager.RenameDisplayName("ships/starter_mk2", new string('A', 65)).Success);
    }
}
