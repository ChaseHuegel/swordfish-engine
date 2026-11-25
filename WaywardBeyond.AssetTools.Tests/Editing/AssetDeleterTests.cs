using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Editing;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Tests.Editing;

/// <summary>
///     Verifies standalone, multi-entry, tag, and read-only boundary behavior of <see cref="AssetDeleter"/>.
/// </summary>
public sealed class AssetDeleterTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary directory for asset deletion tests.
    /// </summary>
    public AssetDeleterTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-deleter-tests-{Guid.NewGuid():N}");
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
    public void DeletesStandaloneMaterialBlueprintLocalizationAndSingleEntryTomlFiles()
    {
        string pack = CreateMinimalPack("standalone-pack");
        string assets = Path.Combine(pack, "assets");

        string matFile = Path.Combine(assets, "materials", "hull.toml");
        string bpFile = Path.Combine(assets, "blueprints", "starter.wbp");
        string locFile = Path.Combine(assets, "lang", "en", "custom.toml");
        string singleBrickFile = Path.Combine(assets, "bricks", "solo.toml");

        WriteFile(matFile, "Shader = \"textured\"\nTextures = [\"block/hull.png\"]\n");
        WriteBytes(bpFile, [1, 2, 3, 4]);
        WriteFile(locFile, "\"Language\" = \"en\"\n\n[Translations]\n\"item.solo\" = \"Solo\"\n");
        WriteFile(
            singleBrickFile,
            "[[Bricks]]\n" +
            "ID = \"mod:solo\"\n" +
            "Shape = \"block\"\n" +
            "[Bricks.Sounds]\n" +
            "Place = [\"metal_place\"]\n" +
            "[Bricks.Textures]\n" +
            "Default = [\"solo\"]\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        var deleter = new AssetDeleter();

        IndexedAsset materialAsset = ws.GetAsset(AssetKind.Material, "hull").Value;
        Result<AssetDeleteResult> matDeleted = deleter.Delete(ws, in materialAsset);
        Assert.True(matDeleted.Success, matDeleted.Message);
        Assert.True(matDeleted.Value.DeletedWholeFile);
        Assert.Null(matDeleted.Value.UpdatedText);
        Assert.False(File.Exists(matFile));

        Result<AssetDeleteResult> bpDeleted = deleter.Delete(ws, AssetKind.Blueprint, bpFile);
        Assert.True(bpDeleted.Success, bpDeleted.Message);
        Assert.True(bpDeleted.Value.DeletedWholeFile);
        Assert.Null(bpDeleted.Value.UpdatedText);
        Assert.False(File.Exists(bpFile));

        Result<AssetDeleteResult> locDeleted = deleter.Delete(ws, AssetKind.Localization, locFile);
        Assert.True(locDeleted.Success, locDeleted.Message);
        Assert.True(locDeleted.Value.DeletedWholeFile);
        Assert.Null(locDeleted.Value.UpdatedText);
        Assert.False(File.Exists(locFile));

        IndexedAsset soloBrick = ws.GetAsset(AssetKind.Brick, "mod:solo").Value;
        Result<AssetDeleteResult> brickDeleted = deleter.Delete(ws, in soloBrick);
        Assert.True(brickDeleted.Success, brickDeleted.Message);
        Assert.True(brickDeleted.Value.DeletedWholeFile);
        Assert.Null(brickDeleted.Value.UpdatedText);
        Assert.False(File.Exists(singleBrickFile));
    }

    [Fact]
    public void DeletesSingleEntryFromMultiEntryBrickAndItemFilesPreservingSiblingsAndComments()
    {
        string pack = CreateMinimalPack("multi-pack");
        string assets = Path.Combine(pack, "assets");
        string bricksFile = Path.Combine(assets, "bricks", "set.toml");
        string itemsFile = Path.Combine(assets, "items", "set.toml");

        WriteFile(
            bricksFile,
            "# Brick set header comment\n" +
            "\n" +
            "[[Bricks]]\n" +
            "ID    = \"mod:first\"\n" +
            "Shape = \"block\"\n" +
            "[Bricks.Sounds]\n" +
            "Place = [\"metal_place\"]\n" +
            "[Bricks.Textures]\n" +
            "Default = [\"first\"]\n" +
            "\n" +
            "[[Bricks]]\n" +
            "ID    = \"mod:middle\"\n" +
            "Shape = \"slab\"\n" +
            "[Bricks.Sounds]\n" +
            "Place = [\"metal_place\"]\n" +
            "[Bricks.Textures]\n" +
            "Default = [\"middle\"]\n" +
            "[[Bricks.Colliders]]\n" +
            "Type   = \"box\"\n" +
            "Offset = { X = 0.0, Y = -0.25, Z = 0.0 }\n" +
            "Size   = { X = 1.0, Y = 0.5, Z = 1.0 }\n" +
            "\n" +
            "# Keep this comment for last brick\n" +
            "[[Bricks]]\n" +
            "ID    = \"mod:last\"\n" +
            "Shape = \"slope\"\n" +
            "[Bricks.Textures]\n" +
            "Default = [\"last\"]\n");

        WriteFile(
            itemsFile,
            "# Palette items\n" +
            "\n" +
            "[[Items]]\n" +
            "ID   = \"mod:item_a\"\n" +
            "Name = \"item.a\"\n" +
            "[Items.Placeable]\n" +
            "Type = \"Brick\"\n" +
            "ID   = \"mod:first\"\n" +
            "\n" +
            "[[Items]]\n" +
            "ID   = \"mod:item_b\"\n" +
            "Name = \"item.b\"\n" +
            "[Items.Tool]\n" +
            "Type = \"Mining\"\n");

        var loader = new WorkspaceLoader();
        var deleter = new AssetDeleter();

        //  Delete middle brick with child tables ([Bricks.Sounds], [Bricks.Textures], [[Bricks.Colliders]]).
        WorkspaceIndex ws1 = loader.LoadPack(pack).Value;
        Result<IndexedAsset> middleBrickResult = ws1.GetAsset(AssetKind.Brick, "mod:middle");
        Assert.True(middleBrickResult.Success, middleBrickResult.Message);
        IndexedAsset middleBrick = middleBrickResult.Value;
        Result<AssetDeleteResult> delMiddle = deleter.Delete(ws1, in middleBrick);

        Assert.True(delMiddle.Success, delMiddle.Message);
        Assert.False(delMiddle.Value.DeletedWholeFile);
        Assert.NotNull(delMiddle.Value.UpdatedText);
        Assert.True(File.Exists(bricksFile));

        string afterMiddle = File.ReadAllText(bricksFile);
        Assert.Equal(delMiddle.Value.UpdatedText, afterMiddle);
        Assert.Contains("# Brick set header comment", afterMiddle, StringComparison.Ordinal);
        Assert.Contains("# Keep this comment for last brick", afterMiddle, StringComparison.Ordinal);
        Assert.Contains("ID    = \"mod:first\"", afterMiddle, StringComparison.Ordinal);
        Assert.Contains("ID    = \"mod:last\"", afterMiddle, StringComparison.Ordinal);
        Assert.DoesNotContain("mod:middle", afterMiddle, StringComparison.Ordinal);
        Assert.DoesNotContain("Bricks.Colliders", afterMiddle, StringComparison.Ordinal);
        Assert.False(TomlDocument.Parse(afterMiddle).HasSyntaxErrors);

        //  Delete last brick by ID -> trims trailing blank lines cleanly while keeping first brick and header comment.
        WorkspaceIndex ws2 = loader.LoadPack(pack).Value;
        Result<AssetDeleteResult> delLast = deleter.Delete(ws2, AssetKind.Brick, bricksFile, assetId: "mod:last");
        Assert.True(delLast.Success, delLast.Message);
        Assert.False(delLast.Value.DeletedWholeFile);

        string afterLast = File.ReadAllText(bricksFile);
        Assert.Contains("# Brick set header comment", afterLast, StringComparison.Ordinal);
        Assert.Contains("ID    = \"mod:first\"", afterLast, StringComparison.Ordinal);
        Assert.DoesNotContain("mod:last", afterLast, StringComparison.Ordinal);
        Assert.False(TomlDocument.Parse(afterLast).HasSyntaxErrors);

        //  Delete first item by (tableIndex, entryIndex) -> preserves header comment and second item with its [Items.Tool] sub-table.
        Result<AssetDeleteResult> delFirstItem = deleter.Delete(ws2, AssetKind.Item, itemsFile, tableIndex: 0, entryIndex: 0);
        Assert.True(delFirstItem.Success, delFirstItem.Message);
        Assert.False(delFirstItem.Value.DeletedWholeFile);

        string afterFirstItem = File.ReadAllText(itemsFile);
        Assert.Contains("# Palette items", afterFirstItem, StringComparison.Ordinal);
        Assert.DoesNotContain("mod:item_a", afterFirstItem, StringComparison.Ordinal);
        Assert.DoesNotContain("Items.Placeable", afterFirstItem, StringComparison.Ordinal);
        Assert.Contains("ID   = \"mod:item_b\"", afterFirstItem, StringComparison.Ordinal);
        Assert.Contains("[Items.Tool]", afterFirstItem, StringComparison.Ordinal);
        Assert.False(TomlDocument.Parse(afterFirstItem).HasSyntaxErrors);
    }

    [Fact]
    public void DeletesSingleTagKeyFromSharedFileAndDeletesWholeFileWhenLastTagRemoved()
    {
        string pack = CreateMinimalPack("tags-pack");
        string tagsFile = Path.Combine(pack, "assets", "lang", "tags", "sounds.toml");
        WriteFile(
            tagsFile,
            "# Invariant sound tags\n" +
            "\n" +
            "\"Language\" = \"\"\n" +
            "\n" +
            "[Tags]\n" +
            "\"metal_place\" = [\n" +
            "    \"sounds/place/metal.1.wav\",\n" +
            "]\n" +
            "\n" +
            "# Keep break sound comment\n" +
            "\"metal_break\" = [\n" +
            "    \"sounds/remove/metal.1.wav\",\n" +
            "]\n");

        var loader = new WorkspaceLoader();
        var deleter = new AssetDeleter();

        WorkspaceIndex ws1 = loader.LoadPack(pack).Value;
        IndexedTagEntry firstTag = ws1.TagEntries.Single(static t => t.TagName == "metal_place");
        Result<AssetDeleteResult> removedFirst = deleter.Delete(ws1, in firstTag);

        Assert.True(removedFirst.Success, removedFirst.Message);
        Assert.False(removedFirst.Value.DeletedWholeFile);
        Assert.NotNull(removedFirst.Value.UpdatedText);

        string remaining = File.ReadAllText(tagsFile);
        Assert.Contains("# Invariant sound tags", remaining, StringComparison.Ordinal);
        Assert.Contains("# Keep break sound comment", remaining, StringComparison.Ordinal);
        Assert.DoesNotContain("metal_place", remaining, StringComparison.Ordinal);
        Assert.Contains("\"metal_break\"", remaining, StringComparison.Ordinal);
        Assert.False(TomlDocument.Parse(remaining).HasSyntaxErrors);

        //  Deleting the final remaining tag deletes the whole file.
        WorkspaceIndex ws2 = loader.LoadPack(pack).Value;
        IndexedTagEntry lastTag = ws2.TagEntries.Single(static t => t.TagName == "metal_break");
        Result<AssetDeleteResult> removedLast = deleter.Delete(ws2, in lastTag);

        Assert.True(removedLast.Success, removedLast.Message);
        Assert.True(removedLast.Value.DeletedWholeFile);
        Assert.Null(removedLast.Value.UpdatedText);
        Assert.False(File.Exists(tagsFile));

        //  Aligned single-line [Tags] removes one key cleanly and re-aligns remaining keys.
        string alignedTagsFile = Path.Combine(pack, "assets", "lang", "tags", "bricks.toml");
        WriteFile(
            alignedTagsFile,
            "\"Language\" = \"\"\n" +
            "\n" +
            "[Tags]\n" +
            "\"brick.a\"    = [\"wb:a\"]\n" +
            "\"brick.long\" = [\"wb:b\"]\n" +
            "\"brick.c\"    = [\"wb:c\"]\n");

        WorkspaceIndex ws3 = loader.LoadPack(pack).Value;
        IndexedTagEntry alignedTag = ws3.TagEntries.Single(static t => t.TagName == "brick.long");
        Result<AssetDeleteResult> removedAligned = deleter.Delete(ws3, in alignedTag);
        Assert.True(removedAligned.Success, removedAligned.Message);
        Assert.False(removedAligned.Value.DeletedWholeFile);

        string remainingAligned = File.ReadAllText(alignedTagsFile);
        Assert.DoesNotContain("brick.long", remainingAligned, StringComparison.Ordinal);
        Assert.Contains("\"brick.a\" = [\"wb:a\"]", remainingAligned, StringComparison.Ordinal);
        Assert.Contains("\"brick.c\" = [\"wb:c\"]", remainingAligned, StringComparison.Ordinal);
        Assert.False(TomlDocument.Parse(remainingAligned).HasSyntaxErrors);
    }

    [Fact]
    public void RefusesDeletionInReadOnlyBaseRootAndOutsideWritableWorkspaceRoots()
    {
        string installDir = Path.Combine(_tempRoot, "game-install");
        string baseModule = Path.Combine(installDir, "modules", "base");
        string packDir = Path.Combine(_tempRoot, "my-pack");
        string outsideFile = Path.Combine(_tempRoot, "outside.toml");

        WriteFile(Path.Combine(installDir, "config", "modules.toml"), "LoadOrder = [\"base\"]\n");
        WriteFile(Path.Combine(baseModule, "manifest.toml"), "ID = \"base\"\nName = \"Base Game\"\n");
        string baseBrickPath = Path.Combine(baseModule, "assets", "bricks", "core.toml");
        string baseTagPath = Path.Combine(baseModule, "assets", "lang", "tags", "bricks.toml");
        WriteFile(baseBrickPath, "[[Bricks]]\nID = \"wb:core\"\nShape = \"block\"\n");
        WriteFile(baseTagPath, "\"Language\" = \"\"\n\n[Tags]\n\"brick.base\" = [\"wb:core\"]\n");

        WriteFile(Path.Combine(packDir, "manifest.toml"), "ID = \"my.pack\"\nName = \"My Pack\"\n");
        Directory.CreateDirectory(Path.Combine(packDir, "assets"));
        WriteFile(outsideFile, "[[Bricks]]\nID = \"ext:brick\"\nShape = \"block\"\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(packDir, installDir).Value;
        var deleter = new AssetDeleter();

        IndexedAsset readOnlyBrick = ws.GetAsset(AssetKind.Brick, "wb:core").Value;
        Result<AssetDeleteResult> baseDelete = deleter.Delete(ws, in readOnlyBrick);
        Assert.False(baseDelete.Success);
        Assert.True(File.Exists(baseBrickPath));

        IndexedTagEntry readOnlyTag = ws.TagEntries.Single(static t => t.TagName == "brick.base");
        Result<AssetDeleteResult> baseTagDelete = deleter.Delete(ws, in readOnlyTag);
        Assert.False(baseTagDelete.Success);
        Assert.True(File.Exists(baseTagPath));

        Result<AssetDeleteResult> directReadOnly = deleter.Delete(ws, AssetKind.Brick, baseBrickPath, "wb:core");
        Assert.False(directReadOnly.Success);
        Assert.True(File.Exists(baseBrickPath));

        Result<AssetDeleteResult> outsideDelete = deleter.Delete(ws, AssetKind.Brick, outsideFile, "ext:brick");
        Assert.False(outsideDelete.Success);
        Assert.True(File.Exists(outsideFile));

        string traversalPath = Path.Combine(packDir, "assets", "..", "..", "outside.toml");
        Result<AssetDeleteResult> traversalDelete = deleter.Delete(ws, AssetKind.Brick, traversalPath, "ext:brick");
        Assert.False(traversalDelete.Success);
        Assert.True(File.Exists(outsideFile));
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
}
