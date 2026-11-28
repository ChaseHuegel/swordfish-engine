using System.Text;
using WaywardBeyond.AssetTools.Cli;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Editing;
using WaywardBeyond.AssetTools.Storage;
using WaywardBeyond.AssetTools.Validation;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Tests.Editing;

/// <summary>
///     Verifies slot group indexing, validation, CRUD (scaffold, clone, delete, rename), CLI listing, and deep-link reference extraction.
/// </summary>
public sealed class SlotGroupCrudAndDeepLinkingTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary workspace directory for slot group tool tests.
    /// </summary>
    public SlotGroupCrudAndDeepLinkingTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-slotgroup-tests-{Guid.NewGuid():N}");
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
    public void IndexesSlotGroupsExtractsTwoWayTagReferencesAndValidatesCleanly()
    {
        string pack = CreatePackWithItemTags("slotgroup-index-pack");
        string assets = Path.Combine(pack, "assets");

        WriteFile(
            Path.Combine(assets, "inventory", "slot_groups.toml"),
            "[[SlotGroups]]\n" +
            "ID = \"test:wearables\"\n" +
            "Capacity = 2\n" +
            "AllowedTags = [\"item.wearable\"]\n" +
            "\n" +
            "[[SlotGroups.Slots]]\n" +
            "Index = 0\n" +
            "AllowedTags = [\"item.wearable.helmet\"]\n" +
            "\n" +
            "[[SlotGroups.Slots]]\n" +
            "Index = 1\n" +
            "AllowedTags = [\"item.wearable.torso\"]\n" +
            "\n" +
            "[[SlotGroups]]\n" +
            "ID = \"test:fuel\"\n" +
            "Capacity = 1\n" +
            "AllowedTags = [\"item.fuel\"]\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.False(report.BlocksExport);
        Assert.Equal(0, report.FatalCount);
        Assert.Equal(0, report.BrokenCount);
        Assert.True(ws.GetAsset(AssetKind.SlotGroup, "test:wearables").Success);
        Assert.True(ws.GetAsset(AssetKind.SlotGroup, "test:fuel").Success);

        //  Deep-link inbound references: tag -> slot group (both group-level and per-slot overrides).
        Assert.Contains(ws.GetInboundReferences("item.wearable"), static r => r.SourceKind == AssetKind.SlotGroup && r.SourceAssetId == "test:wearables" && r.FieldName == "AllowedTags[0]");
        Assert.Contains(ws.GetInboundReferences("item.wearable.helmet"), static r => r.SourceKind == AssetKind.SlotGroup && r.SourceAssetId == "test:wearables" && r.FieldName == "Slots[0].AllowedTags[0]");
        Assert.Contains(ws.GetInboundReferences("item.wearable.torso"), static r => r.SourceKind == AssetKind.SlotGroup && r.SourceAssetId == "test:wearables" && r.FieldName == "Slots[1].AllowedTags[0]");
        Assert.Contains(ws.GetInboundReferences("item.fuel"), static r => r.SourceKind == AssetKind.SlotGroup && r.SourceAssetId == "test:fuel" && r.FieldName == "AllowedTags[0]");

        //  CLI ids command includes SlotGroup entries.
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exit = new CliRunner(stdout, stderr, useColor: false).Run(["ids", pack]);
        Assert.Equal(CliExitCode.CLEAN, exit);
        string output = stdout.ToString();
        Assert.Contains("[SlotGroup]", output, StringComparison.Ordinal);
        Assert.Contains("test:wearables", output, StringComparison.Ordinal);
        Assert.Contains("test:fuel", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatesInvalidSlotGroupCapacitiesSlotIndicesAndTagReferences()
    {
        string pack = CreatePackWithItemTags("slotgroup-invalid-pack");
        string assets = Path.Combine(pack, "assets");

        WriteFile(
            Path.Combine(assets, "inventory", "broken.toml"),
            "[[SlotGroups]]\n" +
            "ID = \"test:broken_group\"\n" +
            "Capacity = 2\n" +
            "AllowedTags = [\"ITEM.WEARABLE\", \"item.missing_tag\", \"item.wearable\", \"item.wearable\"]\n" +
            "\n" +
            "[[SlotGroups.Slots]]\n" +
            "Index = 0\n" +
            "AllowedTags = [\"item.wearable.helmt\"]\n" +
            "\n" +
            "[[SlotGroups.Slots]]\n" +
            "Index = 0\n" +
            "AllowedTags = [\"\"]\n" +
            "\n" +
            "[[SlotGroups.Slots]]\n" +
            "Index = 5\n" +
            "AllowedTags = [\"item.wearable.torso\"]\n" +
            "\n" +
            "[[SlotGroups]]\n" +
            "ID = \"test:negative_cap\"\n" +
            "Capacity = -1\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.True(report.BlocksExport);
        Assert.Contains(report.Findings, static f => f.Code == "PATH001" && f.Field == "AllowedTags[0]" && f.SuggestedFixValue == "item.wearable" && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field == "AllowedTags[1]");
        Assert.Contains(report.Findings, static f => f.Code == "TAG001" && f.Field == "AllowedTags[3]" && f.IsSafeBulkFix);
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field == "Slots[0].AllowedTags[0]" && f.SuggestedFixValue == "item.wearable.helmet");
        Assert.Contains(report.Findings, static f => f.Code == "SLOTGROUP002" && f.Field == "Slots[1].Index");
        Assert.Contains(report.Findings, static f => f.Code == "SLOTGROUP002" && f.Field == "Slots[1].AllowedTags[0]");
        Assert.Contains(report.Findings, static f => f.Code == "SLOTGROUP001" && f.Field == "Slots[2].Index");
        Assert.Contains(report.Findings, static f => f.Code == "SLOTGROUP001" && f.Field == "Capacity");
    }

    [Fact]
    public void ScaffoldsAppendsClonesDeletesAndRenamesSlotGroupsPreservingOwnedSlots()
    {
        string pack = CreatePackWithItemTags("slotgroup-crud-pack");
        string assets = Path.Combine(pack, "assets");
        string file = Path.Combine(assets, "inventory", "custom_groups.toml");

        //  1. Scaffold new slot group file
        var scaffolder = new AssetScaffolder();
        TomlDocument createdDoc = scaffolder.CreateInNewFile(AssetKind.SlotGroup, "test:belt", file).Value;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, createdDoc.ToBytes());

        //  Append a second slot group entry to the same file
        TomlDocument loaded = TomlDocument.Load(file).Value;
        Assert.True(scaffolder.AppendToExistingFile(AssetKind.SlotGroup, "test:pouch", loaded).Success);
        File.WriteAllBytes(file, loaded.ToBytes());

        //  Populate per-slot overrides on test:belt and add a recipe referencing SlotGroup = "test:belt"
        WriteFile(
            file,
            "[[SlotGroups]]\n" +
            "ID = \"test:belt\"\n" +
            "Capacity = 2\n" +
            "AllowedTags = [\"item.wearable\"]\n" +
            "\n" +
            "[[SlotGroups.Slots]]\n" +
            "Index = 0\n" +
            "AllowedTags = [\"item.wearable.helmet\"]\n" +
            "\n" +
            "[[SlotGroups]]\n" +
            "ID = \"test:pouch\"\n" +
            "Capacity = 4\n");

        string recipeFile = Path.Combine(assets, "recipes", "craft.toml");
        WriteFile(
            recipeFile,
            "[[Recipes]]\n" +
            "ID = \"test:craft_fuel\"\n" +
            "TargetTag = \"item.fuel\"\n" +
            "DurationSeconds = 1.0\n" +
            "\n" +
            "[[Recipes.Inputs]]\n" +
            "ItemId = \"test:coal\"\n" +
            "Count = 1\n" +
            "SlotGroup = \"test:belt\"\n" +
            "\n" +
            "[[Recipes.Outputs]]\n" +
            "ItemId = \"test:coal\"\n" +
            "Count = 1\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        IndexedAsset original = ws.GetAsset(AssetKind.SlotGroup, "test:belt").Value;

        //  Recipe SlotGroup reference appears as inbound reference on test:belt
        Assert.Contains(ws.GetInboundReferences("test:belt"), static r => r.SourceKind == AssetKind.Recipe && r.SourceAssetId == "test:craft_fuel" && r.FieldName == "Inputs[0].SlotGroup");

        //  2. Duplicate slot group in the same multi-entry file and verify owned [[SlotGroups.Slots]] are copied too
        var cloner = new AssetCloner();
        AssetCloneResult cloned = cloner.DuplicateAsset(ws, AssetKind.SlotGroup, original.Id, original.Provenance.FilePath, original.Provenance.TableIndex).Value;
        Assert.Equal("test:belt_2", cloned.NewAssetId);

        ws = new WorkspaceLoader().LoadPack(pack).Value;
        IndexedAsset duplicate = ws.GetAsset(AssetKind.SlotGroup, "test:belt_2").Value;
        var dupDef = Assert.IsType<WaywardBeyond.Data.SlotGroupDefinition>(duplicate.AuthoritativeDefinition);
        Assert.Equal(2, dupDef.Capacity);
        Assert.NotNull(dupDef.Slots);
        Assert.Single(dupDef.Slots);
        Assert.Equal(0, dupDef.Slots[0].Index);
        Assert.Equal(["item.wearable.helmet"], dupDef.Slots[0].AllowedTags);

        //  3. Delete duplicated entry while keeping original and sibling intact
        AssetDeleteResult deleted = new AssetDeleter().Delete(ws, in duplicate).Value;
        Assert.False(deleted.DeletedWholeFile);

        ws = new WorkspaceLoader().LoadPack(pack).Value;
        Assert.True(ws.GetAsset(AssetKind.SlotGroup, "test:belt").Success);
        Assert.True(ws.GetAsset(AssetKind.SlotGroup, "test:pouch").Success);
        Assert.False(ws.GetAsset(AssetKind.SlotGroup, "test:belt_2").Success);

        //  4. Rename slot group and verify both slot group definition and recipe SlotGroup reference update
        var planner = new RenamePlanner();
        RenamePreview rename = planner.PlanRename(ws, AssetKind.SlotGroup, "test:belt", "test:utility_belt").Value;
        Assert.Equal(2, rename.FilePlans.Count);
        var draftStore = new DraftStore(Path.Combine(_tempRoot, "drafts"));
        Assert.True(planner.ApplyToDrafts(in rename, draftStore).Success);

        string updatedSlotGroupDraft = Encoding.UTF8.GetString(draftStore.LoadDraft(file).Value.DraftBytes);
        string updatedRecipeDraft = Encoding.UTF8.GetString(draftStore.LoadDraft(recipeFile).Value.DraftBytes);
        Assert.Contains("ID = \"test:utility_belt\"", updatedSlotGroupDraft, StringComparison.Ordinal);
        Assert.Contains("SlotGroup = \"test:utility_belt\"", updatedRecipeDraft, StringComparison.Ordinal);
    }

    private string CreatePackWithItemTags(string name)
    {
        string pack = Path.Combine(_tempRoot, name);
        string assets = Path.Combine(pack, "assets");
        Directory.CreateDirectory(assets);
        WriteFile(Path.Combine(pack, "manifest.toml"), $"ID = \"{name}\"\nName = \"{name}\"\n");
        WriteFile(
            Path.Combine(assets, "lang", "en", "item.toml"),
            "\"Language\" = \"en\"\n\n[Translations]\n\"item.coal.name\" = \"Coal\"\n");
        WriteFile(
            Path.Combine(assets, "lang", "tags", "items.toml"),
            "\"Language\" = \"\"\n\n[Tags]\n" +
            "\"item.wearable\" = []\n" +
            "\"item.wearable.helmet\" = []\n" +
            "\"item.wearable.torso\" = []\n" +
            "\"item.fuel\" = [\"test:coal\"]\n");
        WriteFile(
            Path.Combine(assets, "items", "coal.toml"),
            "[[Items]]\nID = \"test:coal\"\nName = \"item.coal.name\"\nMaxStack = 100\n");
        return pack;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
