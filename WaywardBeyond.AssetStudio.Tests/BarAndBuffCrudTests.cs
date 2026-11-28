using System.Text;
using Avalonia.Headless.XUnit;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.AssetStudio.ViewModels;
using ToolsSettings = WaywardBeyond.AssetTools.Settings;
using Xunit;

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>
///     Verifies end-to-end Bar and Buff CRUD, structured editing, undo/redo, persistence, clone, and delete in Asset Studio.
/// </summary>
public sealed class BarAndBuffCrudTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary directory for bar and buff studio integration tests.
    /// </summary>
    public BarAndBuffCrudTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-studio-bar-buff-tests-{Guid.NewGuid():N}");
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
    public void BarAndBuffCrud_EditSaveReloadUndoCloneDelete_WorkEndToEnd()
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

        //  Verify NewAssetDialog exposes Bar and Buff with expected folders and default IDs.
        Assert.Contains(AssetKind.Bar, NewAssetDialogViewModel.AVAILABLE_KINDS);
        Assert.Contains(AssetKind.Buff, NewAssetDialogViewModel.AVAILABLE_KINDS);

        var newDialog = new NewAssetDialogViewModel(service, static _ => { }, static () => { }, defaultNamespace: "test");
        newDialog.SelectedKind = AssetKind.Bar;
        Assert.Equal("test:new_bar", newDialog.AssetId);
        Assert.Equal("assets/bars/new_bar.toml", newDialog.ComputedTargetPath);

        newDialog.SelectedKind = AssetKind.Buff;
        Assert.Equal("test:new_buff", newDialog.AssetId);
        Assert.Equal("assets/buffs/new_buff.toml", newDialog.ComputedTargetPath);

        //  Create a Bar and a Buff document.
        var createBar = service.CreateAssetDocument(new NewAssetRequest(
            AssetKind.Bar,
            "test:health",
            UseExistingFile: false,
            TargetRelativeFile: "assets/bars/vitals.toml"));
        Assert.True(createBar.Success, createBar.Message);

        var createBuff = service.CreateAssetDocument(new NewAssetRequest(
            AssetKind.Buff,
            "test:well_fed",
            UseExistingFile: false,
            TargetRelativeFile: "assets/buffs/status.toml"));
        Assert.True(createBuff.Success, createBuff.Message);

        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "bar-buff-pack");

        //  1. Edit Bar fields, save, and reload.
        Assert.True(vm.SelectAssetById("test:health"));
        var barDoc = vm.SelectedAsset!;
        Assert.Equal(AssetKind.Bar, barDoc.Kind);
        Assert.True(barDoc.IsBar);

        barDoc.BarStatIdField!.Value = "wb:health";
        barDoc.BarMaxStatIdField!.Value = "wb:max_health";
        barDoc.BarDefaultMaxField!.Value = "100";
        barDoc.BarIconCountField!.Value = "10";
        barDoc.BarIconFullField!.Value = "ui/hud/heart_full.png";
        barDoc.BarIconHalfField!.Value = "ui/hud/heart_half.png";
        barDoc.BarIconEmptyField!.Value = "ui/hud/heart_empty.png";
        barDoc.BarWarnThresholdField!.Value = "0.35";
        barDoc.BarCriticalThresholdField!.Value = "0.15";
        barDoc.BarOrderField!.Value = "1";
        barDoc.BarHideInCreativeField!.Value = "true";
        barDoc.BarColorTintField!.Value = "#FF5555";

        vm.SaveCurrentAssetCommand.Execute(null);
        Assert.False(barDoc.IsDirty);

        //  2. Edit Buff fields + StatModifiers + RegenRates with undo/redo, then save.
        Assert.True(vm.SelectAssetById("test:well_fed"));
        var buffDoc = vm.SelectedAsset!;
        Assert.Equal(AssetKind.Buff, buffDoc.Kind);
        Assert.True(buffDoc.IsBuff);

        buffDoc.NameField!.Value = "buff.well_fed.name";
        buffDoc.DescriptionField!.Value = "buff.well_fed.desc";
        buffDoc.IconField!.Value = "icons/buffs/well_fed.png";
        buffDoc.BuffDurationField!.Value = "90";
        buffDoc.BuffStackPolicyField!.Value = "Extend";

        buffDoc.AddBuffStatModifierCommand.Execute(null);
        Assert.Single(buffDoc.BuffStatModifiers);
        buffDoc.BuffStatModifiers[0].StatId = "wb:move_speed";
        buffDoc.BuffStatModifiers[0].FlatDelta = "";
        buffDoc.BuffStatModifiers[0].PercentDelta = "0.2";

        buffDoc.AddBuffStatModifierCommand.Execute(null);
        Assert.Equal(2, buffDoc.BuffStatModifiers.Count);
        vm.UndoCommand.Execute(null);
        Assert.Single(buffDoc.BuffStatModifiers);
        vm.RedoCommand.Execute(null);
        Assert.Equal(2, buffDoc.BuffStatModifiers.Count);
        buffDoc.RemoveBuffStatModifierCommand.Execute(buffDoc.BuffStatModifiers[1]);
        Assert.Single(buffDoc.BuffStatModifiers);

        buffDoc.AddBuffRegenRateCommand.Execute(null);
        Assert.Single(buffDoc.BuffRegenRates);
        buffDoc.BuffRegenRates[0].StatId = "wb:health";
        buffDoc.BuffRegenRates[0].PerSecond = "2.5";

        vm.SaveCurrentAssetCommand.Execute(null);
        Assert.False(buffDoc.IsDirty);

        //  3. Reload workspace from disk and verify clean validation and persisted values.
        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "bar-buff-pack");
        Assert.Equal(0, vm.FatalCount);
        Assert.Equal(0, vm.BrokenCount);

        Assert.True(vm.SelectAssetById("test:health"));
        barDoc = vm.SelectedAsset!;
        Assert.Equal("wb:health", barDoc.BarStatIdField!.Value);
        Assert.Equal("wb:max_health", barDoc.BarMaxStatIdField!.Value);
        Assert.Equal("100", barDoc.BarDefaultMaxField!.Value);
        Assert.Equal("10", barDoc.BarIconCountField!.Value);
        Assert.Equal("ui/hud/heart_full.png", barDoc.BarIconFullField!.Value);
        Assert.Equal("1", barDoc.BarOrderField!.Value);
        Assert.Equal("true", barDoc.BarHideInCreativeField!.Value);
        Assert.Equal("#FF5555", barDoc.BarColorTintField!.Value);

        Assert.True(vm.SelectAssetById("test:well_fed"));
        buffDoc = vm.SelectedAsset!;
        Assert.Equal("buff.well_fed.name", buffDoc.NameField!.Value);
        Assert.Equal("buff.well_fed.desc", buffDoc.DescriptionField!.Value);
        Assert.Equal("icons/buffs/well_fed.png", buffDoc.IconField!.Value);
        Assert.Equal("90", buffDoc.BuffDurationField!.Value);
        Assert.Equal("Extend", buffDoc.BuffStackPolicyField!.Value);
        Assert.Single(buffDoc.BuffStatModifiers);
        Assert.Equal("wb:move_speed", buffDoc.BuffStatModifiers[0].StatId);
        Assert.Equal("0.2", buffDoc.BuffStatModifiers[0].PercentDelta);
        Assert.Single(buffDoc.BuffRegenRates);
        Assert.Equal("wb:health", buffDoc.BuffRegenRates[0].StatId);
        Assert.Equal("2.5", buffDoc.BuffRegenRates[0].PerSecond);

        //  4. Duplicate and delete via workspace service.
        var dupBar = service.DuplicateAsset(AssetKind.Bar, "test:health", barDoc.AbsoluteFilePath, barDoc.TableIndex);
        Assert.True(dupBar.Success, dupBar.Message);
        Assert.Equal("test:health_2", dupBar.Value.NewAssetId);

        var dupBuff = service.DuplicateAsset(AssetKind.Buff, "test:well_fed", buffDoc.AbsoluteFilePath, buffDoc.TableIndex);
        Assert.True(dupBuff.Success, dupBuff.Message);
        Assert.Equal("test:well_fed_2", dupBuff.Value.NewAssetId);

        var delBar = service.DeleteAsset(AssetKind.Bar, "test:health_2", barDoc.AbsoluteFilePath, -1, -1);
        Assert.True(delBar.Success, delBar.Message);

        var delBuff = service.DeleteAsset(AssetKind.Buff, "test:well_fed_2", buffDoc.AbsoluteFilePath, -1, -1);
        Assert.True(delBuff.Success, delBuff.Message);

        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "bar-buff-pack");
        Assert.True(vm.SelectAssetById("test:health"));
        Assert.True(vm.SelectAssetById("test:well_fed"));
        Assert.False(vm.SelectAssetById("test:health_2"));
        Assert.False(vm.SelectAssetById("test:well_fed_2"));
    }

    private string CreatePack()
    {
        var packDir = Path.Combine(_tempRoot, "pack");
        var assets = Path.Combine(packDir, "assets");
        Directory.CreateDirectory(Path.Combine(assets, "textures", "ui", "hud"));
        Directory.CreateDirectory(Path.Combine(assets, "textures", "icons", "buffs"));
        Directory.CreateDirectory(Path.Combine(assets, "lang", "en"));

        WriteFile(
            Path.Combine(packDir, "pack.toml"),
            "ID = \"bar_buff_pack\"\n" +
            "Namespace = \"test\"\n" +
            "DisplayName = \"Bar Buff Pack\"\n" +
            "Author = \"Tester\"\n" +
            "ContentVersion = 1\n");

        WriteFile(
            Path.Combine(assets, "lang", "en", "buffs.toml"),
            "\"Language\" = \"en\"\n\n[Translations]\n" +
            "\"buff.well_fed.name\" = \"Well Fed\"\n" +
            "\"buff.well_fed.desc\" = \"Restores health over time.\"\n");

        byte[] minimalPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        File.WriteAllBytes(Path.Combine(assets, "textures", "ui", "hud", "heart_full.png"), minimalPng);
        File.WriteAllBytes(Path.Combine(assets, "textures", "ui", "hud", "heart_half.png"), minimalPng);
        File.WriteAllBytes(Path.Combine(assets, "textures", "ui", "hud", "heart_empty.png"), minimalPng);
        File.WriteAllBytes(Path.Combine(assets, "textures", "icons", "buffs", "well_fed.png"), minimalPng);

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
