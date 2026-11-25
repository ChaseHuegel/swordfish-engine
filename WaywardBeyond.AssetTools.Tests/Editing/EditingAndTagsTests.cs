using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Editing;
using WaywardBeyond.AssetTools.Storage;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Tests.Editing;

/// <summary>
///     Verifies cross-file rename planning, undoable draft batches, asset scaffolding, and two-pane tag editing.
/// </summary>
public sealed class EditingAndTagsTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary directory for editing and tag tests.
    /// </summary>
    public EditingAndTagsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-edit-tests-{Guid.NewGuid():N}");
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
    public void BrickRenamePreviewUpdatesAllReferencesWithWarningAndSupportsUndoableDraftBatch()
    {
        string pack = Path.Combine(_tempRoot, "rename-pack");
        string assets = Path.Combine(pack, "assets");
        string draftsDir = Path.Combine(_tempRoot, "drafts");
        string recoveryDir = Path.Combine(_tempRoot, "recovery");
        WriteFile(Path.Combine(pack, "manifest.toml"), "ID = \"rename.pack\"\nName = \"Rename Pack\"\n");

        string brickFile = Path.Combine(assets, "bricks", "core.toml");
        string itemFile = Path.Combine(assets, "items", "core.toml");
        string skillFile = Path.Combine(assets, "skills", "mining.toml");
        string tagFile = Path.Combine(assets, "lang", "tags", "bricks.toml");

        WriteFile(brickFile, "[[Bricks]]\nID    = \"wb:old_core\"\nShape = \"block\"\n");
        WriteFile(itemFile, "[[Items]]\nID = \"wb:core_item\"\n[Items.Placeable]\nType = \"brick\"\nID   = \"wb:old_core\"\n");
        WriteFile(skillFile, "[[Skills]]\nID = \"mining\"\n[Skills.Sources.Break]\n\"wb:old_core\" = 5\n[Skills.Levels]\n1 = 100\n");
        WriteFile(tagFile, "\"Language\" = \"\"\n\n[Tags]\n\"brick.buildable\" = [\n    \"wb:old_core\",\n]\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        var planner = new RenamePlanner();
        Result<RenamePreview> previewResult = planner.PlanRename(ws, AssetKind.Brick, "wb:old_core", "wb:new_core");

        Assert.True(previewResult.Success, previewResult.Message);
        RenamePreview preview = previewResult.Value;
        Assert.Equal(4, preview.FilePlans.Count);
        Assert.Equal(4, preview.TotalEditCount);
        Assert.Contains(preview.Warnings, static w => w.Contains("legacy save hash", StringComparison.OrdinalIgnoreCase));

        var draftStore = new DraftStore(draftsDir);
        Assert.True(draftStore.StartSession().Success);

        Result<WorkspaceDraftBatch> batchResult = planner.ApplyToDrafts(in preview, draftStore);
        Assert.True(batchResult.Success);
        WorkspaceDraftBatch batch = batchResult.Value;
        Assert.True(batch.IsApplied);
        Assert.Equal(4, draftStore.GetRecoverableDrafts().Value.Count);

        Assert.True(batch.Undo(draftStore).Success);
        Assert.False(batch.IsApplied);
        Assert.Empty(draftStore.GetRecoverableDrafts().Value);

        Assert.True(batch.Apply(draftStore).Success);
        MultiFileSaveResult saved = new MultiFileSaver(recoveryDir).SaveAll(batch.BuildSaveRequests());
        Assert.True(saved.Success);

        Assert.Contains("ID    = \"wb:new_core\"", File.ReadAllText(brickFile), StringComparison.Ordinal);
        Assert.Contains("ID   = \"wb:new_core\"", File.ReadAllText(itemFile), StringComparison.Ordinal);
        Assert.Contains("\"wb:new_core\" = 5", File.ReadAllText(skillFile), StringComparison.Ordinal);
        Assert.Contains("\"wb:new_core\"", File.ReadAllText(tagFile), StringComparison.Ordinal);
    }

    [Fact]
    public void AssetScaffolderCreatesNewEntriesAppendsToExistingAndCreatesLocalizationKey()
    {
        var scaffolder = new AssetScaffolder();

        Assert.Equal("assets/bricks/hull.toml", AssetScaffolder.NormalizeTargetFilePath(AssetKind.Brick, "assets/bricks/hull"));
        Assert.Equal("assets/bricks/hull.toml", AssetScaffolder.NormalizeTargetFilePath(AssetKind.Brick, "assets/bricks/hull.toml"));
        Assert.Equal("assets/bricks/hull.v2.toml", AssetScaffolder.NormalizeTargetFilePath(AssetKind.Brick, "assets/bricks/hull.v2"));
        Assert.Equal("assets/blueprints/ship.wbp", AssetScaffolder.NormalizeTargetFilePath(AssetKind.Blueprint, "assets/blueprints/ship.wbp"));

        Result<TomlDocument> brickDoc = scaffolder.CreateInNewFile(AssetKind.Brick, "mod:titanium", "assets/bricks/titanium");
        Assert.True(brickDoc.Success);
        Assert.False(brickDoc.Value.HasSyntaxErrors);
        Assert.Equal("assets/bricks/titanium.toml", brickDoc.Value.SourcePath);
        Assert.Contains("ID    = \"mod:titanium\"", brickDoc.Value.Text, StringComparison.Ordinal);

        AssetKind[] scaffoldKinds = [AssetKind.Brick, AssetKind.Item, AssetKind.Paint, AssetKind.Skill, AssetKind.Body, AssetKind.Material];
        for (int i = 0; i < scaffoldKinds.Length; i++)
        {
            Result<TomlDocument> created = scaffolder.CreateInNewFile(scaffoldKinds[i], $"mod:sample_{i}", $"assets/sample_{i}");
            Assert.True(created.Success, created.Message);
            Assert.False(created.Value.HasSyntaxErrors);
            Assert.EndsWith(".toml", created.Value.SourcePath, StringComparison.Ordinal);
            Assert.DoesNotContain("\"laser\"", created.Value.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"projector\"", created.Value.Text, StringComparison.OrdinalIgnoreCase);
        }

        TomlDocument existingPaints = TomlDocument.Parse(
            "[[Paints]]\nID    = \"wb:rust\"\nName  = \"paint.rust.name\"\nColor = \"#be4a2f\"\n");
        Result<TomlTableSpan> appended = scaffolder.AppendToExistingFile(AssetKind.Paint, "mod:cobalt", existingPaints);
        Assert.True(appended.Success);
        Assert.Equal(2, existingPaints.GetTables("Paints").Count);

        string langPathWithoutExt = Path.Combine(_tempRoot, "lang", "en", "item");
        string langPath = langPathWithoutExt + ".toml";
        Result<TomlDocument> createdLang = scaffolder.CreateLocalizationKey(langPathWithoutExt, "en", "item.mod.titanium", "Titanium");
        Assert.True(createdLang.Success);
        Assert.Equal(langPath, createdLang.Value.SourcePath);
        WriteFile(langPath, createdLang.Value.Text);

        Result<TomlDocument> updatedLang = scaffolder.CreateLocalizationKey(langPathWithoutExt, "en", "item.mod.titanium.desc", "Strong alloy");
        Assert.True(updatedLang.Success);
        Assert.Equal("Titanium", updatedLang.Value.GetField("Translations", "item.mod.titanium").Value.TypedValue);
        Assert.Equal("Strong alloy", updatedLang.Value.GetField("Translations", "item.mod.titanium.desc").Value.TypedValue);
    }

    [Fact]
    public void TagDocumentModelMergesEffectiveOrderFlagsDuplicatesWarnsOnceOnReorderAndSupportsUndo()
    {
        string baseFile = Path.Combine(_tempRoot, "base", "bricks.toml");
        string packFile = Path.Combine(_tempRoot, "pack", "bricks.toml");
        string packToml = "\"Language\" = \"\"\n\n[Tags]\n\"brick.light\" = [\n    \"mod:lamp\",\n    \"mod:beacon\",\n]\n";
        TomlDocument packDoc = TomlDocument.Parse(packToml, packFile);
        TomlFieldSpan packField = packDoc.GetField("Tags", "brick.light").Value;

        var baseEntry = new IndexedTagEntry(string.Empty, "brick.light", ["wb:light"], "base", "Base Game", baseFile, IsWritable: false, packField);
        var packEntry = new IndexedTagEntry(string.Empty, "brick.light", ["mod:lamp", "mod:beacon"], "pack", "My Pack", packFile, IsWritable: true, packField);
        IReadOnlyList<IndexedTagEntry> allEntries = [baseEntry, packEntry];

        var readOnlyModel = new TagDocumentModel(in baseEntry, allEntries);
        Assert.True(readOnlyModel.IsReadOnly);
        Assert.False(readOnlyModel.Append("wb:other").Success);

        var model = new TagDocumentModel(in packEntry, allEntries);
        Assert.Equal(3, model.EffectiveOrder.Count);
        Assert.Empty(model.DuplicateValues);

        Assert.True(model.Append("wb:light").Success);
        Assert.Contains("wb:light", model.DuplicateValues);
        Assert.Equal(4, model.EffectiveOrder.Count);

        Result<bool> firstReorder = model.Reorder(0, 1);
        Assert.True(firstReorder.Success);
        Assert.True(firstReorder.Value);
        Assert.True(model.HasReorderWarning);
        Assert.Equal(["mod:beacon", "mod:lamp", "wb:light"], model.FileOrder);

        Result<bool> secondReorder = model.Reorder(1, 0);
        Assert.True(secondReorder.Success);
        Assert.False(secondReorder.Value);

        Assert.True(model.Undo().Success);
        Assert.Equal(["mod:beacon", "mod:lamp", "wb:light"], model.FileOrder);

        Assert.True(model.Delete(2).Success);
        Assert.Empty(model.DuplicateValues);

        Assert.True(model.ApplyToDocument(packDoc).Success);
        Assert.Contains("\"mod:beacon\"", packDoc.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"wb:light\"", packDoc.Text, StringComparison.Ordinal);
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
