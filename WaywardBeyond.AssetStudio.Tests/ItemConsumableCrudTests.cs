using System.Text;
using Avalonia.Headless.XUnit;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.AssetStudio.ViewModels;
using ToolsSettings = WaywardBeyond.AssetTools.Settings;
using Xunit;

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>
///     Verifies end-to-end CRUD, undo/redo, buff reference picking, validation, and lossless TOML persistence
///     for <c>[Items.Consumable]</c> and <c>[[Items.Consumable.Modifiers]]</c> in Asset Studio.
/// </summary>
public sealed class ItemConsumableCrudTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary directory for item consumable studio integration tests.
    /// </summary>
    public ItemConsumableCrudTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-studio-item-consumable-tests-{Guid.NewGuid():N}");
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
    public void ItemConsumableCrud_CreateEditUndoReloadAndRemove_WorkEndToEnd()
    {
        var packDir = CreatePack();
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

        //  1. Verify item starts non-consumable and BuffId candidates are projected.
        Assert.True(vm.SelectAssetById("test:ration"));
        var itemDoc = vm.SelectedAsset!;
        Assert.Equal(AssetKind.Item, itemDoc.Kind);
        Assert.False(itemDoc.HasConsumable);
        Assert.Equal("Not consumable", itemDoc.ItemConsumableSummary);
        Assert.Contains(itemDoc.ItemConsumableBuffIdField!.FilteredCandidates, c => c.Value == "test:well_fed" && c.Kind == ReferenceKind.BuffId);

        //  2. Enable consumable, configure BuffID + CooldownSeconds + Modifiers with undo/redo, then save.
        itemDoc.EnableItemConsumableCommand.Execute(null);
        Assert.True(itemDoc.HasConsumable);

        itemDoc.ItemConsumableBuffIdField!.Value = "test:well_fed";
        itemDoc.ItemConsumableCooldownField!.Value = "5.5";

        itemDoc.AddItemConsumableModifierCommand.Execute(null);
        Assert.Single(itemDoc.ItemConsumableModifiers);
        itemDoc.ItemConsumableModifiers[0].StatId = "wb:health";
        itemDoc.ItemConsumableModifiers[0].FlatDelta = "20";
        itemDoc.ItemConsumableModifiers[0].PercentDelta = "";

        itemDoc.AddItemConsumableModifierCommand.Execute(null);
        Assert.Equal(2, itemDoc.ItemConsumableModifiers.Count);
        itemDoc.ItemConsumableModifiers[1].StatId = "wb:oxygen";
        itemDoc.ItemConsumableModifiers[1].FlatDelta = "";
        itemDoc.ItemConsumableModifiers[1].PercentDelta = "0.25";

        //  Undo and redo the second modifier edit/add.
        vm.UndoCommand.Execute(null);
        Assert.Equal(string.Empty, itemDoc.ItemConsumableModifiers[1].PercentDelta);
        vm.RedoCommand.Execute(null);
        Assert.Equal("0.25", itemDoc.ItemConsumableModifiers[1].PercentDelta);

        vm.SaveCurrentAssetCommand.Execute(null);
        Assert.False(itemDoc.IsDirty);

        //  3. Reload from disk and verify persisted consumable values, clean validation, and inbound buff reference.
        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "consumable-pack");
        Assert.Equal(0, vm.FatalCount);
        Assert.Equal(0, vm.BrokenCount);

        Assert.True(vm.SelectAssetById("test:ration"));
        itemDoc = vm.SelectedAsset!;
        Assert.True(itemDoc.HasConsumable);
        Assert.Equal("test:well_fed", itemDoc.ItemConsumableBuffIdField!.Value);
        Assert.Equal("5.5", itemDoc.ItemConsumableCooldownField!.Value);
        Assert.Equal(2, itemDoc.ItemConsumableModifiers.Count);
        Assert.Equal("wb:health", itemDoc.ItemConsumableModifiers[0].StatId);
        Assert.Equal("20", itemDoc.ItemConsumableModifiers[0].FlatDelta);
        Assert.Equal(string.Empty, itemDoc.ItemConsumableModifiers[0].PercentDelta);
        Assert.Equal("wb:oxygen", itemDoc.ItemConsumableModifiers[1].StatId);
        Assert.Equal(string.Empty, itemDoc.ItemConsumableModifiers[1].FlatDelta);
        Assert.Equal("0.25", itemDoc.ItemConsumableModifiers[1].PercentDelta);

        Assert.True(vm.SelectAssetById("test:well_fed"));
        var buffDoc = vm.SelectedAsset!;
        Assert.Contains(buffDoc.InboundReferences, r => r.SourceAssetId == "test:ration" && r.FieldPath == "Consumable.BuffID");

        //  4. Update existing consumable (clear optional BuffID, update cooldown, remove second modifier) and save.
        Assert.True(vm.SelectAssetById("test:ration"));
        itemDoc = vm.SelectedAsset!;
        itemDoc.ItemConsumableBuffIdField!.Value = string.Empty;
        itemDoc.ItemConsumableCooldownField!.Value = "3";
        itemDoc.RemoveItemConsumableModifierCommand.Execute(itemDoc.ItemConsumableModifiers[1]);
        Assert.Single(itemDoc.ItemConsumableModifiers);

        vm.SaveCurrentAssetCommand.Execute(null);
        Assert.False(itemDoc.IsDirty);

        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "consumable-pack");
        Assert.Equal(0, vm.FatalCount);
        Assert.Equal(0, vm.BrokenCount);
        Assert.True(vm.SelectAssetById("test:ration"));
        itemDoc = vm.SelectedAsset!;
        Assert.True(itemDoc.HasConsumable);
        Assert.Equal(string.Empty, itemDoc.ItemConsumableBuffIdField!.Value);
        Assert.Equal("3", itemDoc.ItemConsumableCooldownField!.Value);
        Assert.Single(itemDoc.ItemConsumableModifiers);
        Assert.Equal("wb:health", itemDoc.ItemConsumableModifiers[0].StatId);

        //  5. Remove consumable, verify undo restores it, then remove and save to delete TOML sections.
        itemDoc.RemoveItemConsumableCommand.Execute(null);
        Assert.False(itemDoc.HasConsumable);
        Assert.Empty(itemDoc.ItemConsumableModifiers);

        vm.UndoCommand.Execute(null);
        Assert.True(itemDoc.HasConsumable);
        Assert.Single(itemDoc.ItemConsumableModifiers);

        vm.RedoCommand.Execute(null);
        Assert.False(itemDoc.HasConsumable);
        Assert.Empty(itemDoc.ItemConsumableModifiers);

        vm.SaveCurrentAssetCommand.Execute(null);
        Assert.False(itemDoc.IsDirty);

        var savedText = File.ReadAllText(Path.Combine(packDir, "assets", "items", "ration.toml"));
        Assert.DoesNotContain("[Items.Consumable]", savedText);
        Assert.DoesNotContain("[[Items.Consumable.Modifiers]]", savedText);
        Assert.Contains("ID          = \"test:ration\"", savedText);

        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "consumable-pack");
        Assert.Equal(0, vm.FatalCount);
        Assert.Equal(0, vm.BrokenCount);
        Assert.True(vm.SelectAssetById("test:ration"));
        Assert.False(vm.SelectedAsset!.HasConsumable);
    }

    [AvaloniaFact]
    public void ItemConsumableValidation_ReportsMissingBuffAndInvalidModifiers()
    {
        var packDir = CreatePack();
        WriteFile(
            Path.Combine(packDir, "assets", "items", "bad_consumable.toml"),
            "[[Items]]\n" +
            "ID          = \"test:bad_consumable\"\n" +
            "Name        = \"item.test.ration\"\n" +
            "Description = \"item.test.ration.desc\"\n" +
            "MaxStack    = 10\n" +
            "[Items.Consumable]\n" +
            "BuffID          = \"test:missing_buff\"\n" +
            "CooldownSeconds = -2.0\n" +
            "[[Items.Consumable.Modifiers]]\n" +
            "StatId = \"wb:health\"\n");

        var dataPaths = CreateIsolatedDataPaths(Path.Combine(_tempRoot, "appdata-val"));
        using var logSession = new StudioLogSession(Path.Combine(_tempRoot, "logs-val"), ToolsSettings.ToolLogLevel.Information);
        var service = new AssetToolsStudioWorkspaceService(dataPaths, logSession);
        var previewController = new StudioGlPreviewController();

        using var vm = new MainWindowViewModel(
            service,
            previewController,
            startOnWelcomeScreen: false,
            enableLiveGlViewport: false,
            initialWorkspacePath: packDir);

        Assert.True(vm.BrokenCount >= 3);
        Assert.Contains(vm.AllFindings, f => f.Code == "REF001" && f.FieldKey == "ConsumableBuffID");
        Assert.Contains(vm.AllFindings, f => f.Code == "CONSUMABLE001" && f.FieldKey == "ConsumableCooldownSeconds");
        Assert.Contains(vm.AllFindings, f => f.Code == "CONSUMABLE001" && f.FieldKey == "Consumable.Modifiers");
    }

    private string CreatePack()
    {
        var packDir = Path.Combine(_tempRoot, "pack");
        var assets = Path.Combine(packDir, "assets");
        Directory.CreateDirectory(Path.Combine(assets, "items"));
        Directory.CreateDirectory(Path.Combine(assets, "buffs"));
        Directory.CreateDirectory(Path.Combine(assets, "lang", "en"));

        WriteFile(
            Path.Combine(packDir, "pack.toml"),
            "ID = \"consumable_pack\"\n" +
            "Namespace = \"test\"\n" +
            "DisplayName = \"Consumable Pack\"\n" +
            "Author = \"Tester\"\n" +
            "ContentVersion = 1\n");

        WriteFile(
            Path.Combine(assets, "lang", "en", "item.toml"),
            "\"Language\" = \"en\"\n\n[Translations]\n" +
            "\"item.test.ration\" = \"Field Ration\"\n" +
            "\"item.test.ration.desc\" = \"Compact emergency ration.\"\n" +
            "\"buff.test.well_fed\" = \"Well Fed\"\n");

        WriteFile(
            Path.Combine(assets, "buffs", "well_fed.toml"),
            "[[Buffs]]\n" +
            "ID              = \"test:well_fed\"\n" +
            "Name            = \"buff.test.well_fed\"\n" +
            "DurationSeconds = 60.0\n" +
            "StackPolicy     = \"Refresh\"\n");

        WriteFile(
            Path.Combine(assets, "items", "ration.toml"),
            "[[Items]]\n" +
            "ID          = \"test:ration\"\n" +
            "Name        = \"item.test.ration\"\n" +
            "Description = \"item.test.ration.desc\"\n" +
            "MaxStack    = 10\n");

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
        File.WriteAllBytes(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content));
    }
}
