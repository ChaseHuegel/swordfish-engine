using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Editing;
using WaywardBeyond.AssetTools.Workspace;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using DataVersionType = WaywardBeyond.Data.Version;

namespace WaywardBeyond.AssetTools.Tests.Editing;

/// <summary>
///     Verifies <see cref="AssetIdAllocator"/> and <see cref="AssetCloner"/> across multi-entry TOML, materials, and blueprints.
/// </summary>
public sealed class AssetClonerTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary directory for asset cloning tests.
    /// </summary>
    public AssetClonerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-cloner-tests-{Guid.NewGuid():N}");
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
    public void AllocatorAllocatesIncrementingUniqueIdsAcrossAllKindsAndShadowedRoots()
    {
        string installDir = Path.Combine(_tempRoot, "game-install");
        string baseDir = Path.Combine(installDir, "modules", "base");
        string packDir = Path.Combine(_tempRoot, "my-pack");

        WriteFile(Path.Combine(installDir, "config", "modules.toml"), "LoadOrder = [\"base\"]\n");
        WriteFile(Path.Combine(baseDir, "manifest.toml"), "ID = \"base\"\nName = \"Base\"\n");
        WriteFile(Path.Combine(packDir, "manifest.toml"), "ID = \"my.pack\"\nName = \"My Pack\"\n");

        //  Base defines wb:core, wb:core_2, and shadowed wb:core_3; pack overrides wb:core_3.
        WriteFile(
            Path.Combine(baseDir, "assets", "bricks", "core.toml"),
            "[[Bricks]]\nID = \"wb:core\"\nShape = \"block\"\n\n" +
            "[[Bricks]]\nID = \"wb:core_2\"\nShape = \"block\"\n\n" +
            "[[Bricks]]\nID = \"wb:core_3\"\nShape = \"block\"\n");
        WriteFile(
            Path.Combine(packDir, "assets", "bricks", "override.toml"),
            "[[Bricks]]\nID = \"wb:core_3\"\nShape = \"slab\"\n\n" +
            "[[Bricks]]\nID = \"wb:gap_3\"\nShape = \"block\"\n\n" +
            "[[Bricks]]\nID = \"wb:one_1\"\nShape = \"block\"\n");

        WriteFile(Path.Combine(baseDir, "assets", "materials", "items", "laser.toml"), "Shader = \"textured\"\n");
        WriteFile(Path.Combine(packDir, "assets", "materials", "items", "laser_2.toml"), "Shader = \"textured\"\n");
        WriteBytes(Path.Combine(baseDir, "assets", "blueprints", "mainMenu.wbp"), [1, 2, 3]);
        WriteBytes(Path.Combine(packDir, "assets", "blueprints", "sub", "ship.wbp"), [4, 5, 6]);
        WriteBytes(Path.Combine(packDir, "assets", "blueprints", "sub_2", "shuttle.wbp"), [7, 8, 9]);

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(packDir, installDir).Value;

        Assert.Equal("wb:core_4", AssetIdAllocator.AllocateNextId(ws, AssetKind.Brick, "wb:core").Value);
        Assert.Equal("wb:core_4", AssetIdAllocator.AllocateNextId(ws, AssetKind.Brick, "wb:core_2").Value);
        Assert.Equal("wb:core_4", AssetIdAllocator.AllocateNextId(ws, AssetKind.Brick, "wb:core_3").Value);
        Assert.Equal("wb:gap_2", AssetIdAllocator.AllocateNextId(ws, AssetKind.Brick, "wb:gap_3").Value);
        Assert.Equal("wb:one_1_2", AssetIdAllocator.AllocateNextId(ws, AssetKind.Brick, "wb:one_1").Value);

        Assert.Equal("items/laser_3", AssetIdAllocator.AllocateNextId(ws, AssetKind.Material, "items/laser").Value);
        Assert.Equal("items/laser_3", AssetIdAllocator.AllocateNextId(ws, AssetKind.Material, "items/laser_2").Value);
        Assert.Equal("mainMenu_2", AssetIdAllocator.AllocateNextId(ws, AssetKind.Blueprint, "mainMenu").Value);
        Assert.Equal("sub/ship_2", AssetIdAllocator.AllocateNextId(ws, AssetKind.Blueprint, "sub/ship").Value);
        Assert.Equal("sub_2/shuttle_2", AssetIdAllocator.AllocateNextId(ws, AssetKind.Blueprint, "sub_2/shuttle").Value);

        Assert.False(AssetIdAllocator.AllocateNextId(ws, AssetKind.Brick, "   ").Success);
    }

    [Fact]
    public void DuplicateAndPasteMultiEntryTomlPreservesSubTablesCommentsQuoteStyleAndIndices()
    {
        string packDir = CreateMinimalPack("clone-pack");
        string assets = Path.Combine(packDir, "assets");
        string bricksFile = Path.Combine(assets, "bricks", "set.toml");
        string itemsFile = Path.Combine(assets, "items", "tools.toml");
        string bodiesFile = Path.Combine(assets, "bodies", "human.toml");
        string skillsFile = Path.Combine(assets, "skills", "mining.toml");
        string paintsFile = Path.Combine(assets, "paints", "colors.toml");

        WriteFile(
            bricksFile,
            "# Top comment\n" +
            "[[Bricks]]\n" +
            "ID    = 'wb:core' # Keep single quotes and inline comment\n" +
            "Shape = \"block\"\n" +
            "# Keep interior comment\n" +
            "[Bricks.Textures]\n" +
            "Default = [\"core\"]\n" +
            "[[Bricks.Colliders]]\n" +
            "Type = \"box\"\n" +
            "Offset = { X = 0.0, Y = 0.0, Z = 0.0 }\n" +
            "Size = { X = 1.0, Y = 1.0, Z = 1.0 }\n" +
            "\n" +
            "# Sibling comment stays with sibling\n" +
            "[[Bricks]]\n" +
            "ID    = \"wb:hull\"\n" +
            "Shape = \"block\"\n");

        WriteFile(
            itemsFile,
            "[[Items]]\n" +
            "ID = \"wb:drill\"\n" +
            "[Items.ViewModel]\n" +
            "Mesh = \"items/drill.obj\"\n");

        WriteFile(
            bodiesFile,
            "[[Bodies]]\n" +
            "ID = \"wb:human\"\n" +
            "[Bodies.States.standing]\n" +
            "front = [\"characters/front.png\"]\n");

        WriteFile(
            skillsFile,
            "[[Skills]]\n" +
            "ID = \"mining\"\n" +
            "[Skills.Levels]\n" +
            "1 = 100\n");

        WriteFile(
            paintsFile,
            "[[Paints]]\n" +
            "ID = \"wb:rust\"\n" +
            "Color = \"#be4a2f\"\n");

        var loader = new WorkspaceLoader();
        var cloner = new AssetCloner();

        WorkspaceIndex ws1 = loader.LoadPack(packDir).Value;
        IndexedAsset coreBrick = ws1.GetAsset(AssetKind.Brick, "wb:core").Value;

        Result<AssetCloneResult> dupBrick = cloner.DuplicateAsset(
            ws1,
            AssetKind.Brick,
            coreBrick.Id,
            coreBrick.Provenance.FilePath,
            coreBrick.Provenance.TableIndex);

        Assert.True(dupBrick.Success, dupBrick.Message);
        Assert.Equal("wb:core_2", dupBrick.Value.NewAssetId);
        Assert.Equal(4, dupBrick.Value.NewTableIndex);
        Assert.Equal(2, dupBrick.Value.NewEntryIndex);
        Assert.False(dupBrick.Value.CreatedNewFile);

        string bricksAfter = File.ReadAllText(bricksFile);
        Assert.Contains("ID    = 'wb:core_2' # Keep single quotes and inline comment", bricksAfter, StringComparison.Ordinal);
        Assert.Contains("# Keep interior comment", bricksAfter, StringComparison.Ordinal);
        Assert.DoesNotContain("# Sibling comment stays with sibling\n\n[[Bricks]]\nID    = 'wb:core_2'", bricksAfter, StringComparison.Ordinal);
        Assert.Equal(2, Occurrences(bricksAfter, "[[Bricks.Colliders]]"));

        //  Reload and duplicate wb:core_2 -> wb:core_3.
        WorkspaceIndex ws2 = loader.LoadPack(packDir).Value;
        IndexedAsset clonedBrick = ws2.GetAsset(AssetKind.Brick, "wb:core_2").Value;
        Assert.Equal(dupBrick.Value.NewTableIndex, clonedBrick.Provenance.TableIndex);
        Assert.Equal(dupBrick.Value.NewEntryIndex, clonedBrick.Provenance.EntryIndex);

        Result<AssetCloneResult> dupBrick2 = cloner.DuplicateAsset(
            ws2,
            AssetKind.Brick,
            clonedBrick.Id,
            clonedBrick.Provenance.FilePath,
            clonedBrick.Provenance.TableIndex);
        Assert.True(dupBrick2.Success, dupBrick2.Message);
        Assert.Equal("wb:core_3", dupBrick2.Value.NewAssetId);
        Assert.Equal(7, dupBrick2.Value.NewTableIndex);
        Assert.Equal(3, dupBrick2.Value.NewEntryIndex);

        //  Verify Item, Body, Skill, Paint also duplicate with their subtables intact.
        Assert.Equal("wb:drill_2", cloner.DuplicateAsset(ws2, AssetKind.Item, "wb:drill", itemsFile, 0).Value.NewAssetId);
        Assert.Equal("wb:human_2", cloner.DuplicateAsset(ws2, AssetKind.Body, "wb:human", bodiesFile, 0).Value.NewAssetId);
        Assert.Equal("mining_2", cloner.DuplicateAsset(ws2, AssetKind.Skill, "mining", skillsFile, 0).Value.NewAssetId);
        Assert.Equal("wb:rust_2", cloner.DuplicateAsset(ws2, AssetKind.Paint, "wb:rust", paintsFile, 0).Value.NewAssetId);

        WorkspaceIndex ws3 = loader.LoadPack(packDir).Value;
        Assert.True(ws3.GetAsset(AssetKind.Item, "wb:drill_2").Success);
        Assert.True(ws3.GetAsset(AssetKind.Body, "wb:human_2").Success);
        Assert.True(ws3.GetAsset(AssetKind.Skill, "mining_2").Success);
        Assert.True(ws3.GetAsset(AssetKind.Paint, "wb:rust_2").Success);
        Assert.Contains("[Items.ViewModel]", File.ReadAllText(itemsFile), StringComparison.Ordinal);
        Assert.Contains("[Bodies.States.standing]", File.ReadAllText(bodiesFile), StringComparison.Ordinal);
        Assert.Contains("[Skills.Levels]", File.ReadAllText(skillsFile), StringComparison.Ordinal);
    }

    [Fact]
    public void MaterialAndBlueprintCloningCreatesSiblingFilesAndRejectsReadOnlyAndMissingAssets()
    {
        string installDir = Path.Combine(_tempRoot, "game-install");
        string baseDir = Path.Combine(installDir, "modules", "base");
        string packDir = Path.Combine(_tempRoot, "my-pack");

        WriteFile(Path.Combine(installDir, "config", "modules.toml"), "LoadOrder = [\"base\"]\n");
        WriteFile(Path.Combine(baseDir, "manifest.toml"), "ID = \"base\"\nName = \"Base\"\n");
        WriteFile(Path.Combine(packDir, "manifest.toml"), "ID = \"my.pack\"\nName = \"My Pack\"\n");
        Directory.CreateDirectory(Path.Combine(packDir, "assets"));

        string baseBrickPath = Path.Combine(baseDir, "assets", "bricks", "core.toml");
        WriteFile(baseBrickPath, "[[Bricks]]\nID = \"wb:core\"\nShape = \"block\"\n");

        string matPath = Path.Combine(packDir, "assets", "materials", "items", "laser.toml");
        WriteFile(matPath, "ID       = 'items/laser'\nShader   = \"textured\"\nTextures = [\"items/laser.png\"]\n");

        string bpPath = Path.Combine(packDir, "assets", "blueprints", "sub", "ship.wbp");
        var bpData = new BlueprintData(
            new DataVersionType(DataVersion.CURRENT, "Wayward Beyond", "Blueprint"),
            "ship",
            1000,
            1000,
            4,
            2,
            2,
            2,
            [],
            ["wb:core"],
            null);
        WriteBytes(bpPath, bpData.Serialize());

        string rawBpPath = Path.Combine(packDir, "assets", "blueprints", "raw_test.wbp");
        WriteBytes(rawBpPath, [9, 8, 7, 6]);

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(packDir, installDir).Value;
        var cloner = new AssetCloner();

        //  Missing files / missing assets fail cleanly via Result.
        Assert.False(cloner.CaptureClipboardPayload(ws, AssetKind.Brick, "wb:missing", baseBrickPath, 0).Success);
        Assert.False(cloner.CaptureClipboardPayload(ws, AssetKind.Brick, "wb:core", Path.Combine(packDir, "nope.toml"), 0).Success);
        Assert.False(cloner.CaptureClipboardPayload(ws, AssetKind.Material, "items/wrong", matPath, -1).Success);

        //  Capturing from read-only base succeeds, duplicating into read-only base fails, pasting into writable pack succeeds.
        Result<AssetClipboardPayload> basePayload = cloner.CaptureClipboardPayload(ws, AssetKind.Brick, "wb:core", baseBrickPath, 0);
        Assert.True(basePayload.Success, basePayload.Message);
        Assert.False(cloner.DuplicateAsset(ws, AssetKind.Brick, "wb:core", baseBrickPath, 0).Success);
        Assert.False(cloner.PasteAsset(ws, basePayload.Value, targetRootId: "base").Success);
        Assert.False(cloner.PasteAsset(ws, basePayload.Value, targetFilePath: baseBrickPath).Success);

        Result<AssetCloneResult> pastedIntoPack = cloner.PasteAsset(ws, basePayload.Value);
        Assert.True(pastedIntoPack.Success, pastedIntoPack.Message);
        Assert.Equal("wb:core_2", pastedIntoPack.Value.NewAssetId);
        Assert.True(File.Exists(pastedIntoPack.Value.TargetFilePath));

        //  Material cloning creates sibling items/laser_2.toml and updates ID preserving single quotes.
        Result<AssetCloneResult> dupMat = cloner.DuplicateAsset(ws, AssetKind.Material, "items/laser", matPath, -1);
        Assert.True(dupMat.Success, dupMat.Message);
        Assert.Equal("items/laser_2", dupMat.Value.NewAssetId);
        Assert.True(dupMat.Value.CreatedNewFile);
        Assert.EndsWith(Path.Combine("materials", "items", "laser_2.toml"), dupMat.Value.TargetFilePath, StringComparison.Ordinal);
        Assert.Contains("ID       = 'items/laser_2'", File.ReadAllText(dupMat.Value.TargetFilePath), StringComparison.Ordinal);

        //  Real BlueprintData cloning writes sibling sub/ship_2.wbp and updates display name.
        Result<AssetCloneResult> dupBp = cloner.DuplicateAsset(ws, AssetKind.Blueprint, "sub/ship", bpPath, -1);
        Assert.True(dupBp.Success, dupBp.Message);
        Assert.Equal("sub/ship_2", dupBp.Value.NewAssetId);
        Assert.True(dupBp.Value.CreatedNewFile);
        BlueprintData clonedBp = BlueprintData.Deserialize(File.ReadAllBytes(dupBp.Value.TargetFilePath));
        Assert.Equal("ship_2", clonedBp.Name);
        Assert.NotNull(clonedBp.BrickPalette);
        Assert.Equal(["wb:core"], clonedBp.BrickPalette);

        //  Raw/opaque blueprint payload still clones cleanly byte-for-byte.
        Result<AssetCloneResult> dupRawBp = cloner.DuplicateAsset(ws, AssetKind.Blueprint, "raw_test", rawBpPath, -1);
        Assert.True(dupRawBp.Success, dupRawBp.Message);
        Assert.Equal("raw_test_2", dupRawBp.Value.NewAssetId);
        Assert.Equal([9, 8, 7, 6], File.ReadAllBytes(dupRawBp.Value.TargetFilePath));
    }

    private string CreateMinimalPack(string name)
    {
        string pack = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(Path.Combine(pack, "assets"));
        WriteFile(Path.Combine(pack, "manifest.toml"), $"ID = \"{name}\"\nName = \"{name}\"\n");
        return pack;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteBytes(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static int Occurrences(string text, string substring)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(substring, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }
}
