using System.Text;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Editing;
using WaywardBeyond.AssetTools.Storage;
using WaywardBeyond.AssetTools.Validation;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Tests.Editing;

/// <summary>
///     Verifies recipe indexing, validation, CRUD (scaffold, clone, delete, rename), and deep-link reference extraction.
/// </summary>
public sealed class RecipeCrudAndDeepLinkingTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary workspace directory for recipe tool tests.
    /// </summary>
    public RecipeCrudAndDeepLinkingTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-recipe-tests-{Guid.NewGuid():N}");
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
    public void IndexesRecipesExtractsDeepLinkReferencesAndValidatesRules()
    {
        string pack = CreatePackWithItemsAndTags("recipe-index-pack");
        string assets = Path.Combine(pack, "assets");

        WriteFile(
            Path.Combine(assets, "recipes", "smelting.toml"),
            "[[recipes]]\n" +
            "id = \"test:smelt_iron\"\n" +
            "station_tag = \"station.smelter\"\n" +
            "mode = \"auto\"\n" +
            "duration_seconds = 3.5\n" +
            "power_kw = -12.0\n" +
            "order = 10\n" +
            "\n" +
            "[[recipes.inputs]]\n" +
            "item_id = \"test:iron_ore\"\n" +
            "count = 2\n" +
            "\n" +
            "[[recipes.inputs]]\n" +
            "item_id = \"tag:item.fuel\"\n" +
            "count = 1\n" +
            "slot_group = \"fuel\"\n" +
            "\n" +
            "[[recipes.outputs]]\n" +
            "item_id = \"test:iron_ingot\"\n" +
            "count = 1\n" +
            "chance = 0.75\n" +
            "\n" +
            "[[recipes]]\n" +
            "id = \"test:burn_fuel\"\n" +
            "station_tag = \"station.generator\"\n" +
            "mode = \"auto\"\n" +
            "duration_seconds = 10.0\n" +
            "power_kw = 50.0\n" +
            "\n" +
            "[[recipes.inputs]]\n" +
            "item_id = \"tag:item.fuel\"\n" +
            "count = 1\n");

        WriteFile(
            Path.Combine(assets, "skills", "metallurgy.toml"),
            "[[Skills]]\n" +
            "ID = \"metallurgy\"\n" +
            "Name = \"skill.name.metallurgy\"\n" +
            "[Skills.Sources.Process]\n" +
            "\"recipe:test:smelt_iron\" = 15\n" +
            "[Skills.Levels]\n" +
            "1 = 100\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.False(report.BlocksExport);
        Assert.True(ws.GetAsset(AssetKind.Recipe, "test:smelt_iron").Success);
        Assert.True(ws.GetAsset(AssetKind.Recipe, "test:burn_fuel").Success);

        //  Deep-link inbound references: item -> recipe, tag -> recipe, skill -> recipe.
        Assert.Contains(ws.GetInboundReferences("test:iron_ore"), static r => r.SourceKind == AssetKind.Recipe && r.SourceAssetId == "test:smelt_iron");
        Assert.Contains(ws.GetInboundReferences("test:iron_ingot"), static r => r.SourceKind == AssetKind.Recipe && r.SourceAssetId == "test:smelt_iron");
        Assert.Contains(ws.GetInboundReferences("item.fuel"), static r => r.SourceKind == AssetKind.Recipe && r.SourceAssetId == "test:smelt_iron");
        Assert.Contains(ws.GetInboundReferences("station.smelter"), static r => r.SourceKind == AssetKind.Recipe && r.SourceAssetId == "test:smelt_iron");
        Assert.Contains(ws.GetInboundReferences("test:smelt_iron"), static r => r.SourceKind == AssetKind.Skill && r.SourceAssetId == "metallurgy");
    }

    [Fact]
    public void ValidatesBrokenRecipeFieldsAndReferences()
    {
        string pack = CreatePackWithItemsAndTags("recipe-invalid-pack");
        string assets = Path.Combine(pack, "assets");

        WriteFile(
            Path.Combine(assets, "recipes", "broken.toml"),
            "[[recipes]]\n" +
            "id = \"test:broken_recipe\"\n" +
            "station_tag = \"\"\n" +
            "mode = \"invalid_mode\"\n" +
            "duration_seconds = -1.0\n" +
            "power_kw = 0.0\n" +
            "\n" +
            "[[recipes.inputs]]\n" +
            "item_id = \"test:missing_ore\"\n" +
            "count = 0\n" +
            "\n" +
            "[[recipes.outputs]]\n" +
            "item_id = \"test:missing_ingot\"\n" +
            "count = 1\n" +
            "chance = 1.5\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        ValidationReport report = new WorkspaceValidator().Validate(ws);

        Assert.True(report.BlocksExport);
        Assert.Contains(report.Findings, static f => f.Code == "RECIPE001" && f.Field == "station_tag");
        Assert.Contains(report.Findings, static f => f.Code == "ENUM001" && f.Field == "mode");
        Assert.Contains(report.Findings, static f => f.Code == "RECIPE002" && f.Field == "duration_seconds");
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field == "inputs[0].item_id");
        Assert.Contains(report.Findings, static f => f.Code == "RECIPE002" && f.Field == "inputs[0].count");
        Assert.Contains(report.Findings, static f => f.Code == "REF001" && f.Field == "outputs[0].item_id");
        Assert.Contains(report.Findings, static f => f.Code == "RECIPE002" && f.Field == "outputs[0].chance");
    }

    [Fact]
    public void ScaffoldsClonesDeletesAndRenamesRecipesAcrossSkillsAndItems()
    {
        string pack = CreatePackWithItemsAndTags("recipe-crud-pack");
        string assets = Path.Combine(pack, "assets");
        string recipesFile = Path.Combine(assets, "recipes", "crafting.toml");

        //  1. Scaffold new recipe file
        var scaffolder = new AssetScaffolder();
        TomlDocument createdDoc = scaffolder.CreateInNewFile(AssetKind.Recipe, "test:craft_plate", recipesFile).Value;
        Directory.CreateDirectory(Path.GetDirectoryName(recipesFile)!);
        File.WriteAllBytes(recipesFile, createdDoc.ToBytes());

        //  Populate valid item references and add a skill referencing recipe:test:craft_plate
        WriteFile(
            recipesFile,
            "[[recipes]]\n" +
            "id = \"test:craft_plate\"\n" +
            "station_tag = \"station.smelter\"\n" +
            "mode = \"manual\"\n" +
            "duration_seconds = 2.0\n" +
            "power_kw = -5.0\n" +
            "\n" +
            "[[recipes.inputs]]\n" +
            "item_id = \"test:iron_ore\"\n" +
            "count = 1\n" +
            "\n" +
            "[[recipes.outputs]]\n" +
            "item_id = \"test:iron_ingot\"\n" +
            "count = 1\n");

        string skillFile = Path.Combine(assets, "skills", "metallurgy.toml");
        WriteFile(
            skillFile,
            "[[Skills]]\n" +
            "ID = \"metallurgy\"\n" +
            "Name = \"skill.name.metallurgy\"\n" +
            "[Skills.Sources.Craft]\n" +
            "\"recipe:test:craft_plate\" = 10\n" +
            "[Skills.Levels]\n" +
            "1 = 100\n");

        WorkspaceIndex ws = new WorkspaceLoader().LoadPack(pack).Value;
        IndexedAsset original = ws.GetAsset(AssetKind.Recipe, "test:craft_plate").Value;

        //  2. Duplicate recipe into the same multi-entry file
        var cloner = new AssetCloner();
        AssetCloneResult cloned = cloner.DuplicateAsset(ws, AssetKind.Recipe, original.Id, original.Provenance.FilePath, original.Provenance.TableIndex).Value;
        Assert.Equal("test:craft_plate_2", cloned.NewAssetId);

        ws = new WorkspaceLoader().LoadPack(pack).Value;
        Assert.True(ws.GetAsset(AssetKind.Recipe, "test:craft_plate").Success);
        Assert.True(ws.GetAsset(AssetKind.Recipe, "test:craft_plate_2").Success);

        //  3. Delete duplicated entry while keeping original intact
        IndexedAsset duplicate = ws.GetAsset(AssetKind.Recipe, "test:craft_plate_2").Value;
        AssetDeleteResult deleted = new AssetDeleter().Delete(ws, in duplicate).Value;
        Assert.False(deleted.DeletedWholeFile);

        ws = new WorkspaceLoader().LoadPack(pack).Value;
        Assert.True(ws.GetAsset(AssetKind.Recipe, "test:craft_plate").Success);
        Assert.False(ws.GetAsset(AssetKind.Recipe, "test:craft_plate_2").Success);

        //  4. Rename recipe and verify skill source key recipe:<id> updates
        var planner = new RenamePlanner();
        RenamePreview renameRecipe = planner.PlanRename(ws, AssetKind.Recipe, "test:craft_plate", "test:forge_plate").Value;
        Assert.Equal(2, renameRecipe.FilePlans.Count);
        var draftStore = new DraftStore(Path.Combine(_tempRoot, "drafts"));
        Assert.True(planner.ApplyToDrafts(in renameRecipe, draftStore).Success);

        string updatedSkillDraft = Encoding.UTF8.GetString(draftStore.LoadDraft(skillFile).Value.DraftBytes);
        Assert.Contains("\"recipe:test:forge_plate\"", updatedSkillDraft, StringComparison.Ordinal);

        //  5. Rename item and verify recipe inputs update
        RenamePreview renameItem = planner.PlanRename(ws, AssetKind.Item, "test:iron_ore", "test:raw_iron").Value;
        Assert.Contains(renameItem.FilePlans, fp => string.Equals(fp.FilePath, recipesFile, StringComparison.Ordinal));
    }

    private string CreatePackWithItemsAndTags(string name)
    {
        string pack = Path.Combine(_tempRoot, name);
        string assets = Path.Combine(pack, "assets");
        Directory.CreateDirectory(assets);
        WriteFile(Path.Combine(pack, "manifest.toml"), $"ID = \"{name}\"\nName = \"{name}\"\n");
        WriteFile(
            Path.Combine(assets, "lang", "en", "item.toml"),
            "\"Language\" = \"en\"\n\n[Translations]\n\"item.iron_ore.name\" = \"Iron Ore\"\n\"item.iron_ingot.name\" = \"Iron Ingot\"\n\"skill.name.metallurgy\" = \"Metallurgy\"\n");
        WriteFile(
            Path.Combine(assets, "lang", "tags", "items.toml"),
            "\"Language\" = \"\"\n\n[Tags]\n\"item.fuel\" = [\"test:iron_ore\"]\n\"station.smelter\" = []\n");
        WriteFile(
            Path.Combine(assets, "items", "ores.toml"),
            "[[Items]]\n" +
            "ID = \"test:iron_ore\"\n" +
            "Name = \"item.iron_ore.name\"\n" +
            "MaxStack = 100\n\n" +
            "[[Items]]\n" +
            "ID = \"test:iron_ingot\"\n" +
            "Name = \"item.iron_ingot.name\"\n" +
            "MaxStack = 100\n");
        return pack;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
