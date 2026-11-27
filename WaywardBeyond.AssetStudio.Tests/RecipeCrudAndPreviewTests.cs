using System.Text;
using Avalonia.Headless.XUnit;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.AssetStudio.ViewModels;
using WaywardBeyond.AssetStudio.Views;
using ToolsSettings = WaywardBeyond.AssetTools.Settings;
using Xunit;

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>
///     Verifies end-to-end Recipe CRUD, deep linking, and live crafting-flow preview in Asset Studio.
/// </summary>
public sealed class RecipeCrudAndPreviewTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary directory for recipe studio integration tests.
    /// </summary>
    public RecipeCrudAndPreviewTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-studio-recipe-tests-{Guid.NewGuid():N}");
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

    [AvaloniaFact]
    public void RecipeCrud_DeepLinking_AndLivePreview_WorkEndToEnd()
    {
        var packDir = CreateRecipePack();
        var dataPaths = CreateIsolatedDataPaths(Path.Combine(_tempRoot, "appdata"));
        using var logSession = new StudioLogSession(Path.Combine(_tempRoot, "logs"), ToolsSettings.ToolLogLevel.Information);
        var service = new AssetToolsStudioWorkspaceService(dataPaths, logSession);
        var previewController = new StudioGlPreviewController();

        using var vm = new MainWindowViewModel(
            service,
            previewController,
            startOnWelcomeScreen: false,
            enableLiveGlViewport: false,
            initialWorkspacePath: packDir);

        //  Verify NewAssetDialog exposes Recipe with assets/recipes folder.
        Assert.Contains(AssetKind.Recipe, NewAssetDialogViewModel.AVAILABLE_KINDS);
        var newDialog = new NewAssetDialogViewModel(service, static _ => { }, static () => { }, defaultNamespace: "test");
        newDialog.SelectedKind = AssetKind.Recipe;
        Assert.Equal("test:new_recipe", newDialog.AssetId);
        Assert.Equal("assets/recipes/new_recipe.toml", newDialog.ComputedTargetPath);

        //  Create a new recipe via real backend.
        var createResult = service.CreateAssetDocument(new NewAssetRequest(
            AssetKind.Recipe,
            "test:smelt_iron",
            UseExistingFile: false,
            TargetRelativeFile: "assets/recipes/smelting.toml"));
        Assert.True(createResult.Success, createResult.Message);

        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "recipe-pack");
        Assert.True(vm.SelectAssetById("test:smelt_iron"));
        var recipeDoc = vm.SelectedAsset!;
        Assert.Equal(AssetKind.Recipe, recipeDoc.Kind);

        //  Verify Recipe Preview surface mode and live preview metrics.
        Assert.True(vm.IsRecipePreview);
        Assert.False(vm.HasVisualPreview);
        Assert.False(vm.IsDataDocumentPreview);
        Assert.False(vm.Has3DPreviewControls);
        Assert.Equal(PreviewSurfaceMode.RecipeFlow, vm.ActivePreviewDescriptor.Mode);

        //  Edit scalar fields and ingredient/output lists, verifying live preview updates + undo/redo.
        recipeDoc.RecipeTargetTagField!.Value = "station.smelter";
        recipeDoc.RecipeModeField!.Value = "Auto";
        recipeDoc.RecipeDurationField!.Value = "4";
        recipeDoc.RecipePowerKwField!.Value = "-10";
        recipeDoc.RecipeOrderField!.Value = "5";

        Assert.Single(recipeDoc.RecipeInputs);
        recipeDoc.RecipeInputs[0].ItemId = "test:iron_ore";
        recipeDoc.RecipeInputs[0].Count = 2;

        recipeDoc.AddRecipeInputCommand.Execute(null);
        Assert.Equal(2, recipeDoc.RecipeInputs.Count);
        recipeDoc.RecipeInputs[1].ItemId = "tag:item.fuel";
        recipeDoc.RecipeInputs[1].Count = 1;
        recipeDoc.RecipeInputs[1].SlotGroup = "fuel";

        Assert.Single(recipeDoc.RecipeOutputs);
        recipeDoc.RecipeOutputs[0].ItemId = "test:iron_ingot";
        recipeDoc.RecipeOutputs[0].Count = 1;
        recipeDoc.RecipeOutputs[0].Chance = "0.5";

        Assert.Equal("AUTO", recipeDoc.RecipeModeBadge);
        Assert.Equal("4 s", recipeDoc.RecipeDurationLabel);
        Assert.Equal("-10 kW", recipeDoc.RecipePowerLabel);
        Assert.Contains("15 cycles/min", recipeDoc.RecipeThroughputLabel, StringComparison.Ordinal);
        Assert.Contains("-40 kJ/cycle", recipeDoc.RecipeThroughputLabel, StringComparison.Ordinal);
        Assert.Equal("50%", recipeDoc.RecipeOutputs[0].ChanceBadge);
        Assert.Equal(0, recipeDoc.RecipeUnresolvedCount);

        //  Verify undo/redo on recipe output add/remove.
        recipeDoc.AddRecipeOutputCommand.Execute(null);
        Assert.Equal(2, recipeDoc.RecipeOutputs.Count);
        vm.UndoCommand.Execute(null);
        Assert.Single(recipeDoc.RecipeOutputs);
        vm.RedoCommand.Execute(null);
        Assert.Equal(2, recipeDoc.RecipeOutputs.Count);
        vm.UndoCommand.Execute(null);
        Assert.Single(recipeDoc.RecipeOutputs);

        //  Save to disk, reload workspace, and verify clean validation & persistence.
        vm.SaveCurrentAssetCommand.Execute(null);
        Assert.False(recipeDoc.IsDirty);

        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "recipe-pack");
        Assert.Equal(0, vm.FatalCount);
        Assert.Equal(0, vm.BrokenCount);

        Assert.True(vm.SelectAssetById("test:smelt_iron"));
        recipeDoc = vm.SelectedAsset!;
        Assert.Equal("station.smelter", recipeDoc.RecipeTargetTagField!.Value);
        Assert.Equal("Auto", recipeDoc.RecipeModeField!.Value);
        Assert.Equal(2, recipeDoc.RecipeInputs.Count);
        Assert.Equal("test:iron_ore", recipeDoc.RecipeInputs[0].ItemId);
        Assert.Equal(2, recipeDoc.RecipeInputs[0].Count);
        Assert.Equal("tag:item.fuel", recipeDoc.RecipeInputs[1].ItemId);
        Assert.Equal("fuel", recipeDoc.RecipeInputs[1].SlotGroup);
        Assert.Single(recipeDoc.RecipeOutputs);
        Assert.Equal("test:iron_ingot", recipeDoc.RecipeOutputs[0].ItemId);
        Assert.Equal("0.5", recipeDoc.RecipeOutputs[0].Chance);

        //  Verify deep linking: Recipe -> Item, Recipe -> Tag, Item -> Recipe, Tag -> Recipe, Skill -> Recipe.
        Assert.Contains(recipeDoc.InboundReferences, static r => r.SourceKind == AssetKind.Skill && r.SourceAssetId == "metallurgy");

        vm.NavigateRecipeTargetCommand.Execute("test:iron_ore");
        Assert.Equal("test:iron_ore", vm.SelectedAsset!.Id);
        Assert.Equal(AssetKind.Item, vm.SelectedAsset.Kind);
        Assert.Contains(vm.SelectedAsset.InboundReferences, static r => r.SourceKind == AssetKind.Recipe && r.SourceAssetId == "test:smelt_iron");

        var inboundFromRecipe = vm.SelectedAsset.InboundReferences.First(static r => r.SourceKind == AssetKind.Recipe && r.SourceAssetId == "test:smelt_iron");
        vm.NavigateInboundReferenceCommand.Execute(inboundFromRecipe);
        Assert.Equal("test:smelt_iron", vm.SelectedAsset!.Id);

        vm.NavigateRecipeTargetCommand.Execute("tag:item.fuel");
        Assert.Equal("item.fuel", vm.SelectedAsset!.Id);
        Assert.Equal(AssetKind.Tag, vm.SelectedAsset.Kind);
        Assert.Contains(vm.SelectedAsset.InboundReferences, static r => r.SourceKind == AssetKind.Recipe && r.SourceAssetId == "test:smelt_iron");

        //  Verify headless visual mount of MainWindow with RecipeFormView & Recipe Preview active.
        Assert.True(vm.SelectAssetById("test:smelt_iron"));
        var window = new MainWindow { DataContext = vm };
        window.Show();
        try
        {
            window.UpdateLayout();
        }
        finally
        {
            window.Close();
        }

        //  Verify duplicate and delete through workspace service.
        var dupResult = service.DuplicateAsset(
            AssetKind.Recipe,
            recipeDoc.Id,
            recipeDoc.AbsoluteFilePath,
            recipeDoc.TableIndex);
        Assert.True(dupResult.Success, dupResult.Message);
        Assert.Equal("test:smelt_iron_2", dupResult.Value.NewAssetId);

        var delResult = service.DeleteAsset(
            AssetKind.Recipe,
            dupResult.Value.NewAssetId,
            dupResult.Value.TargetFilePath,
            dupResult.Value.NewTableIndex,
            dupResult.Value.NewEntryIndex);
        Assert.True(delResult.Success, delResult.Message);

        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "recipe-pack");
        Assert.True(vm.SelectAssetById("test:smelt_iron"));
        Assert.False(vm.SelectAssetById("test:smelt_iron_2"));
    }

    private string CreateRecipePack()
    {
        var packDir = Path.Combine(_tempRoot, "pack");
        var assetsDir = Path.Combine(packDir, "assets");
        Directory.CreateDirectory(assetsDir);

        WriteFile(Path.Combine(packDir, "manifest.toml"), "ID = \"recipe-pack\"\nName = \"Recipe Pack\"\n");
        WriteFile(
            Path.Combine(assetsDir, "lang", "en", "item.toml"),
            "\"Language\" = \"en\"\n\n[Translations]\n\"item.iron_ore.name\" = \"Iron Ore\"\n\"item.iron_ingot.name\" = \"Iron Ingot\"\n\"skill.name.metallurgy\" = \"Metallurgy\"\n");
        WriteFile(
            Path.Combine(assetsDir, "lang", "tags", "items.toml"),
            "\"Language\" = \"\"\n\n[Tags]\n\"item.fuel\" = [\"test:iron_ore\"]\n\"station.smelter\" = []\n");
        WriteFile(
            Path.Combine(assetsDir, "items", "ores.toml"),
            "[[Items]]\n" +
            "ID = \"test:iron_ore\"\n" +
            "Name = \"item.iron_ore.name\"\n" +
            "MaxStack = 100\n\n" +
            "[[Items]]\n" +
            "ID = \"test:iron_ingot\"\n" +
            "Name = \"item.iron_ingot.name\"\n" +
            "MaxStack = 100\n");
        WriteFile(
            Path.Combine(assetsDir, "skills", "metallurgy.toml"),
            "[[Skills]]\n" +
            "ID = \"metallurgy\"\n" +
            "Name = \"skill.name.metallurgy\"\n" +
            "[Skills.Sources.Process]\n" +
            "\"recipe:test:smelt_iron\" = 15\n" +
            "[Skills.Levels]\n" +
            "1 = 100\n");

        return packDir;
    }

    private static ToolsSettings.ToolDataPaths CreateIsolatedDataPaths(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, ToolsSettings.ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);
        return ToolsSettings.ToolDataPaths.Resolve(executableDirectory: root);
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
