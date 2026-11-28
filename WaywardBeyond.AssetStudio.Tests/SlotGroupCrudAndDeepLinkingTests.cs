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
///     Verifies end-to-end SlotGroup and per-slot override CRUD, two-way tag deep linking, and preview in Asset Studio.
/// </summary>
public sealed class SlotGroupCrudAndDeepLinkingTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary directory for slot group studio integration tests.
    /// </summary>
    public SlotGroupCrudAndDeepLinkingTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-studio-slotgroup-tests-{Guid.NewGuid():N}");
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
    public void SlotGroupAndSlotsCrud_TwoWayTagDeepLinking_AndPreview_WorkEndToEnd()
    {
        var packDir = CreateSlotGroupPack();
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

        //  Verify NewAssetDialog exposes SlotGroup with assets/inventory folder.
        Assert.Contains(AssetKind.SlotGroup, NewAssetDialogViewModel.AVAILABLE_KINDS);
        var newDialog = new NewAssetDialogViewModel(service, static _ => { }, static () => { }, defaultNamespace: "test");
        newDialog.SelectedKind = AssetKind.SlotGroup;
        Assert.Equal("test:new_slot_group", newDialog.AssetId);
        Assert.Equal("assets/inventory/new_slot_group.toml", newDialog.ComputedTargetPath);

        //  Create a new slot group and append a second slot group to the same multi-entry file.
        var createFirst = service.CreateAssetDocument(new NewAssetRequest(
            AssetKind.SlotGroup,
            "test:wearables",
            UseExistingFile: false,
            TargetRelativeFile: "assets/inventory/slot_groups.toml"));
        Assert.True(createFirst.Success, createFirst.Message);

        var createSecond = service.CreateAssetDocument(new NewAssetRequest(
            AssetKind.SlotGroup,
            "test:cargo",
            UseExistingFile: true,
            TargetRelativeFile: "assets/inventory/slot_groups.toml"));
        Assert.True(createSecond.Success, createSecond.Message);

        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "slotgroup-pack");
        Assert.True(vm.SelectAssetById("test:wearables"));
        var groupDoc = vm.SelectedAsset!;
        Assert.Equal(AssetKind.SlotGroup, groupDoc.Kind);

        //  Verify SlotGroup preview surface mode.
        Assert.True(vm.IsSlotGroupPreview);
        Assert.False(vm.HasVisualPreview);
        Assert.False(vm.IsDataDocumentPreview);
        Assert.False(vm.Has3DPreviewControls);
        Assert.Equal(PreviewSurfaceMode.SlotGroupLayout, vm.ActivePreviewDescriptor.Mode);

        //  Edit capacity and group-level AllowedTags using searchable tag picker.
        groupDoc.SlotGroupCapacityField!.Value = "3";
        Assert.Equal(3, groupDoc.SlotGroupCapacityValue);
        Assert.Equal("3 slots", groupDoc.SlotGroupCapacityBadge);

        groupDoc.AddSlotGroupAllowedTagCommand.Execute(null);
        Assert.Single(groupDoc.SlotGroupAllowedTags);
        groupDoc.SlotGroupAllowedTags[0].TagPickerField.PickerFilterText = "wearable";
        Assert.Contains(groupDoc.SlotGroupAllowedTags[0].TagPickerField.FilteredCandidates, static c => c.Value == "item.wearable");
        groupDoc.SlotGroupAllowedTags[0].TagPickerField.SelectCandidateCommand.Execute(
            groupDoc.SlotGroupAllowedTags[0].TagPickerField.FilteredCandidates.First(static c => c.Value == "item.wearable"));
        Assert.Equal("item.wearable", groupDoc.SlotGroupAllowedTags[0].TagName);
        Assert.True(groupDoc.SlotGroupAllowedTags[0].IsResolved);

        //  Add two per-slot overrides ([[SlotGroups.Slots]]) and configure their tags via searchable picker.
        groupDoc.AddSlotFilterCommand.Execute(null);
        Assert.Single(groupDoc.SlotGroupSlots);
        Assert.Equal(0, groupDoc.SlotGroupSlots[0].Index);
        Assert.Single(groupDoc.SlotGroupSlots[0].AllowedTags);
        groupDoc.SlotGroupSlots[0].AllowedTags[0].TagPickerField.PickerFilterText = "helmet";
        groupDoc.SlotGroupSlots[0].AllowedTags[0].TagPickerField.SelectCandidateCommand.Execute(
            groupDoc.SlotGroupSlots[0].AllowedTags[0].TagPickerField.FilteredCandidates.First(static c => c.Value == "item.wearable.helmet"));
        Assert.Equal("item.wearable.helmet", groupDoc.SlotGroupSlots[0].AllowedTags[0].TagName);

        groupDoc.AddSlotFilterCommand.Execute(null);
        Assert.Equal(2, groupDoc.SlotGroupSlots.Count);
        Assert.Equal(1, groupDoc.SlotGroupSlots[1].Index);
        groupDoc.SlotGroupSlots[1].AllowedTags[0].TagPickerField.PickerFilterText = "torso";
        groupDoc.SlotGroupSlots[1].AllowedTags[0].TagPickerField.SelectCandidateCommand.Execute(
            groupDoc.SlotGroupSlots[1].AllowedTags[0].TagPickerField.FilteredCandidates.First(static c => c.Value == "item.wearable.torso"));
        Assert.Equal("item.wearable.torso", groupDoc.SlotGroupSlots[1].AllowedTags[0].TagName);

        //  Add a second tag to slot #1 and verify undo/redo on slot tags and slot add/remove.
        groupDoc.SlotGroupSlots[1].AddTagCommand.Execute(null);
        Assert.Equal(2, groupDoc.SlotGroupSlots[1].AllowedTags.Count);
        groupDoc.SlotGroupSlots[1].AllowedTags[1].TagName = "item.wearable.suit";
        Assert.Equal(0, groupDoc.SlotGroupUnresolvedTagCount);

        groupDoc.AddSlotFilterCommand.Execute(null);
        Assert.Equal(3, groupDoc.SlotGroupSlots.Count);
        vm.UndoCommand.Execute(null);
        Assert.Equal(2, groupDoc.SlotGroupSlots.Count);
        vm.RedoCommand.Execute(null);
        Assert.Equal(3, groupDoc.SlotGroupSlots.Count);
        vm.UndoCommand.Execute(null);
        Assert.Equal(2, groupDoc.SlotGroupSlots.Count);

        //  Save to disk and verify inbound deep links on Tags update immediately without reopening workspace.
        vm.SaveCurrentAssetCommand.Execute(null);
        Assert.False(groupDoc.IsDirty);

        Assert.True(vm.SelectAssetById("item.wearable"));
        Assert.Contains(vm.SelectedAsset!.InboundReferences, static r => r.SourceKind == AssetKind.SlotGroup && r.SourceAssetId == "test:wearables" && r.FieldPath == "AllowedTags[0]");

        Assert.True(vm.SelectAssetById("item.wearable.helmet"));
        Assert.Contains(vm.SelectedAsset!.InboundReferences, static r => r.SourceKind == AssetKind.SlotGroup && r.SourceAssetId == "test:wearables" && r.FieldPath == "Slots[0].AllowedTags[0]");

        Assert.True(vm.SelectAssetById("item.wearable.suit"));
        Assert.Contains(vm.SelectedAsset!.InboundReferences, static r => r.SourceKind == AssetKind.SlotGroup && r.SourceAssetId == "test:wearables" && r.FieldPath == "Slots[1].AllowedTags[1]");

        //  Verify Command Palette search finds the slot group by its referenced tag as well.
        vm.OpenCommandPaletteCommand.Execute(null);
        vm.ActiveCommandPalette!.Query = "item.wearable.helmet";
        Assert.Contains(vm.ActiveCommandPalette.Results, static r => r.Title == "test:wearables");
        vm.CloseAllOverlaysCommand.Execute(null);

        //  Reload workspace from disk and verify clean validation and lossless persistence.
        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "slotgroup-pack");
        Assert.Equal(0, vm.FatalCount);
        Assert.Equal(0, vm.BrokenCount);

        Assert.True(vm.SelectAssetById("test:wearables"));
        groupDoc = vm.SelectedAsset!;
        Assert.Equal(3, groupDoc.SlotGroupCapacityValue);
        Assert.Single(groupDoc.SlotGroupAllowedTags);
        Assert.Equal("item.wearable", groupDoc.SlotGroupAllowedTags[0].TagName);
        Assert.Equal(2, groupDoc.SlotGroupSlots.Count);
        Assert.Equal(0, groupDoc.SlotGroupSlots[0].Index);
        Assert.Equal("item.wearable.helmet", groupDoc.SlotGroupSlots[0].AllowedTags[0].TagName);
        Assert.Equal(1, groupDoc.SlotGroupSlots[1].Index);
        Assert.Equal(["item.wearable.torso", "item.wearable.suit"], groupDoc.SlotGroupSlots[1].AllowedTags.Select(static t => t.TagName).ToArray());

        //  Verify two-way deep linking: SlotGroup -> Tag -> SlotGroup -> Back -> Tag.
        vm.NavigateTagTargetCommand.Execute("item.wearable.helmet");
        Assert.Equal("item.wearable.helmet", vm.SelectedAsset!.Id);
        Assert.Equal(AssetKind.Tag, vm.SelectedAsset.Kind);
        var inboundFromSlotGroup = vm.SelectedAsset.InboundReferences.First(static r => r.SourceKind == AssetKind.SlotGroup && r.SourceAssetId == "test:wearables");

        vm.NavigateInboundReferenceCommand.Execute(inboundFromSlotGroup);
        Assert.Equal("test:wearables", vm.SelectedAsset!.Id);
        Assert.Equal(AssetKind.SlotGroup, vm.SelectedAsset.Kind);

        vm.NavigateBackCommand.Execute(null);
        Assert.Equal("item.wearable.helmet", vm.SelectedAsset!.Id);
        Assert.Equal(AssetKind.Tag, vm.SelectedAsset.Kind);

        //  Verify headless visual mount of MainWindow with SlotGroupFormView & SlotGroup Preview active.
        Assert.True(vm.SelectAssetById("test:wearables"));
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

        //  Verify duplicate and surgical delete inside multi-entry file.
        var dupResult = service.DuplicateAsset(
            AssetKind.SlotGroup,
            groupDoc.Id,
            groupDoc.AbsoluteFilePath,
            groupDoc.TableIndex);
        Assert.True(dupResult.Success, dupResult.Message);
        Assert.Equal("test:wearables_2", dupResult.Value.NewAssetId);

        var delResult = service.DeleteAsset(
            AssetKind.SlotGroup,
            dupResult.Value.NewAssetId,
            dupResult.Value.TargetFilePath,
            dupResult.Value.NewTableIndex,
            dupResult.Value.NewEntryIndex);
        Assert.True(delResult.Success, delResult.Message);

        vm.OpenWorkspace(WorkspaceMode.Pack, packDir, "slotgroup-pack");
        Assert.True(vm.SelectAssetById("test:wearables"));
        Assert.True(vm.SelectAssetById("test:cargo"));
        Assert.False(vm.SelectAssetById("test:wearables_2"));
    }

    private string CreateSlotGroupPack()
    {
        var packDir = Path.Combine(_tempRoot, "pack");
        var assetsDir = Path.Combine(packDir, "assets");
        Directory.CreateDirectory(assetsDir);

        WriteFile(Path.Combine(packDir, "manifest.toml"), "ID = \"slotgroup-pack\"\nName = \"Slot Group Pack\"\n");
        WriteFile(
            Path.Combine(assetsDir, "lang", "tags", "items.toml"),
            "\"Language\" = \"\"\n\n[Tags]\n" +
            "\"item.wearable\" = []\n" +
            "\"item.wearable.helmet\" = []\n" +
            "\"item.wearable.torso\" = []\n" +
            "\"item.wearable.suit\" = []\n");

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
