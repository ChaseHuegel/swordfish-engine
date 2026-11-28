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
///     Verifies end-to-end Asset Studio CRUD for <c>[Bricks.Entity.Light]</c> (including color picker)
///     and <c>[[Bricks.Entity.SlotGroups]]</c>.
/// </summary>
public sealed class BrickEntityCrudTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temp workspace directory for brick entity studio CRUD tests.
    /// </summary>
    public BrickEntityCrudTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-studio-brick-entity-{Guid.NewGuid():N}");
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
    public void BrickEntityLightWithColorPickerAndSlotGroupsRoundTripThroughSaveAndReload()
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

        Assert.True(vm.SelectAssetById("test:lamp_crate"));
        var doc = vm.SelectedAsset!;
        Assert.Equal(AssetKind.Brick, doc.Kind);

        //  Entity light color field uses the studio color picker control (IsColorField == true).
        Assert.NotNull(doc.BrickEntityLightColorField);
        Assert.True(doc.BrickEntityLightColorField!.IsColorField);
        Assert.False(doc.BrickHasEntityLight);
        Assert.Empty(doc.BrickEntitySlotGroups);

        //  Enable Entity Light, edit fields (including color picker value and offset), and add a slot group + power stat.
        doc.EnableBrickEntityLightCommand.Execute(null);
        Assert.True(doc.BrickHasEntityLight);

        doc.BrickEntityLightColorField.Value = "#33CCFF";
        doc.BrickEntityLightRadiusField!.Value = "6.5";
        doc.BrickEntityLightSizeField!.Value = "0.75";
        doc.BrickEntityLightIntensityField!.Value = "3.5";
        doc.BrickEntityLightCastShadowsField!.Value = "true";
        doc.BrickEntityLightOffsetField!.Value = "0, 0.5, 0.25";
        doc.BrickEntityLightTintFromPaintField!.Value = "true";

        doc.AddBrickEntitySlotGroupCommand.Execute(null);
        Assert.Single(doc.BrickEntitySlotGroups);
        doc.BrickEntitySlotGroups[0].GroupId = "wb:cargo";
        doc.BrickEntitySlotGroups[0].Capacity = "18";

        doc.AddPowerKwStatisticCommand.Execute(null);
        Assert.Contains(doc.Statistics, static s => s.Key == "wb:power_kw");

        //  Save to disk and reload the workspace to verify lossless TOML persistence.
        vm.SaveCurrentAssetCommand.Execute(null);
        Assert.False(doc.IsDirty);

        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "brick-entity-pack");
        Assert.Equal(0, vm.FatalCount);
        Assert.Equal(0, vm.BrokenCount);
        Assert.True(vm.SelectAssetById("test:lamp_crate"));
        var reloaded = vm.SelectedAsset!;

        Assert.True(reloaded.BrickHasEntityLight);
        Assert.Equal("#33CCFF", reloaded.BrickEntityLightColorField!.Value);
        Assert.Equal("6.5", reloaded.BrickEntityLightRadiusField!.Value);
        Assert.Equal("0.75", reloaded.BrickEntityLightSizeField!.Value);
        Assert.Equal("3.5", reloaded.BrickEntityLightIntensityField!.Value);
        Assert.Equal("true", reloaded.BrickEntityLightCastShadowsField!.Value);
        Assert.Equal("0, 0.5, 0.25", reloaded.BrickEntityLightOffsetField!.Value);
        Assert.Equal("true", reloaded.BrickEntityLightTintFromPaintField!.Value);

        Assert.Single(reloaded.BrickEntitySlotGroups);
        Assert.Equal("wb:cargo", reloaded.BrickEntitySlotGroups[0].GroupId);
        Assert.Equal("18", reloaded.BrickEntitySlotGroups[0].Capacity);

        //  Verify headless visual mount of MainWindow with BrickFormView renders cleanly.
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

        //  Remove Entity Light and Slot Group, save, and verify clean removal on reload.
        reloaded.RemoveBrickEntityLightCommand.Execute(null);
        reloaded.RemoveBrickEntitySlotGroupCommand.Execute(reloaded.BrickEntitySlotGroups[0]);
        Assert.False(reloaded.BrickHasEntityLight);
        Assert.Empty(reloaded.BrickEntitySlotGroups);

        vm.SaveCurrentAssetCommand.Execute(null);
        Assert.False(reloaded.IsDirty);

        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "brick-entity-pack");
        Assert.True(vm.SelectAssetById("test:lamp_crate"));
        var cleared = vm.SelectedAsset!;
        Assert.False(cleared.BrickHasEntityLight);
        Assert.Empty(cleared.BrickEntitySlotGroups);
    }

    private static ToolsSettings.ToolDataPaths CreateIsolatedDataPaths(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, ToolsSettings.ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);
        return ToolsSettings.ToolDataPaths.Resolve(executableDirectory: root);
    }

    private string CreatePack()
    {
        var packDir = Path.Combine(_tempRoot, "pack");
        Directory.CreateDirectory(Path.Combine(packDir, "assets", "bricks"));
        Directory.CreateDirectory(Path.Combine(packDir, "assets", "inventory"));
        Directory.CreateDirectory(Path.Combine(packDir, "assets", "textures", "block"));

        File.WriteAllText(
            Path.Combine(packDir, "pack.toml"),
            "ID = \"test\"\nName = \"Test Pack\"\nVersion = \"1.0.0\"\n",
            Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(packDir, "assets", "textures", "block", "panel.png"), [0x89, 0x50, 0x4E, 0x47]);
        File.WriteAllText(
            Path.Combine(packDir, "assets", "inventory", "slot_groups.toml"),
            "[[SlotGroups]]\nID = \"wb:cargo\"\nCapacity = 27\n",
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(packDir, "assets", "bricks", "lamp_crate.toml"),
            """
            [[Bricks]]
            ID = "test:lamp_crate"
            Shape = "block"
            [Bricks.Textures]
            Default = ["panel"]
            """,
            Encoding.UTF8);

        return packDir;
    }
}
