using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WaywardBeyond.AssetStudio.Controls;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.AssetStudio.ViewModels;
using WaywardBeyond.AssetStudio.Views;
using ToolsSettings = WaywardBeyond.AssetTools.Settings;
using Xunit;

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>
///     Headless visual capture harness rendering 1280x800 PNG previews of every studio screen, form, and dialog.
/// </summary>
public sealed class VisualCaptureTests
{
    /// <summary>
    ///     Captures start screen, all workbench forms, raw TOML views, new Wave 2 surfaces, and every popup dialog.
    /// </summary>
    [AvaloniaFact]
    public void CaptureAllStudioScreensAndDialogs()
    {
        using var vm = new MainWindowViewModel(
            new SampleStudioWorkspaceService(),
            new SamplePreviewSurfaceProvider(),
            startOnWelcomeScreen: true);

        var window = new MainWindow { DataContext = vm };
        try
        {
            // 1. Start Screen (Dark & Light)
            vm.IsDarkTheme = true;
            vm.IsStartScreenVisible = true;
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "01-start-screen-dark.png"));

            vm.IsDarkTheme = false;
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "01b-start-screen-light.png"));

            // 2. Main Workbench — Brick (Dark & Light) with expanded validation dock
            vm.IsStartScreenVisible = false;
            vm.SelectAsset(AssetKind.Brick, "wb:thruster", pushHistory: true, highlightFieldKey: "TextureDefault");
            vm.ExpandAllValidationFiles();
            vm.IsDarkTheme = true;
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "02-workbench-brick-dark.png"));

            vm.IsDarkTheme = false;
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "02b-workbench-brick-light.png"));

            // Return to Dark for consistent workbench series
            vm.IsDarkTheme = true;

            // 3. Item form with raw-token {} toggles + WorldModel Informational badge
            vm.SelectAsset(AssetKind.Item, "wb:thruster_item", pushHistory: true);
            vm.IsTomlViewActive = false;
            if (vm.SelectedAsset?.ItemPlaceableIdField != null)
            {
                vm.SelectedAsset.ItemPlaceableIdField.IsRawMode = true;
            }

            if (vm.SelectedAsset?.ItemMaxStackField != null)
            {
                vm.SelectedAsset.ItemMaxStackField.IsRawMode = true;
            }

            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "03-workbench-item-and-raw-toml.png"));

            // 4. Skill form with custom Sparkline + Paint form
            vm.SelectAsset(AssetKind.Skill, "building", pushHistory: true);
            vm.IsTomlViewActive = false;
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "04-workbench-paint-and-skill-sparkline.png"));

            vm.SelectAsset(AssetKind.Paint, "wb:red", pushHistory: true);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "04b-workbench-paint.png"));

            // 5. Body sprite grid + Material + Blueprint metadata toolbar
            vm.SelectAsset(AssetKind.Body, "wb:human_m", pushHistory: true);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "05-workbench-body-material-blueprint.png"));

            vm.SelectAsset(AssetKind.Material, "bricks/glass", pushHistory: true);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "05b-workbench-material.png"));

            vm.SelectAsset(AssetKind.Blueprint, "ships/scout_mk1.wbp", pushHistory: true);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "05c-workbench-blueprint.png"));

            // 5d. Brick Shape = Any multi-shape preview grid
            vm.SelectAsset(AssetKind.Brick, "wb:panel", pushHistory: true);
            vm.IsShapeGridMode = true;
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "05d-workbench-brick-shape-any-grid.png"));
            vm.IsShapeGridMode = false;

            // 6. Two-pane Tag Editor with live Effective Order + drag grips + semantic order warning
            vm.SelectAsset(AssetKind.Tag, "brick.paintable", pushHistory: true);
            if (vm.SelectedAsset != null && vm.SelectedAsset.TagFileEntries.Count > 1)
            {
                vm.SelectedAsset.MoveTagUpCommand.Execute(vm.SelectedAsset.TagFileEntries[1]);
            }

            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "06-tag-editor-two-pane.png"));

            // 7. Localization table editor + Syntax-highlighted TOML source view + Syntax-error raw banner
            vm.SelectAsset(AssetKind.Localization, "lang/en/item.toml", pushHistory: true);
            vm.IsTomlViewActive = false;
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "07-localization-table-and-toml-view.png"));

            vm.SelectAsset(AssetKind.Brick, "wb:thruster", pushHistory: true);
            vm.IsTomlViewActive = true;
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "07b-toml-source-view.png"));

            vm.SelectAsset(AssetKind.Brick, "colony:broken_hull", pushHistory: true);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "07c-syntax-error-raw-mode.png"));

            // 8. Command Palette overlay (F1)
            vm.SelectAsset(AssetKind.Brick, "wb:thruster", pushHistory: true);
            vm.IsTomlViewActive = false;
            vm.OpenCommandPaletteCommand.Execute(null);
            if (vm.ActiveCommandPalette != null)
            {
                vm.ActiveCommandPalette.Query = "wb:";
            }

            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "08-command-palette-overlay.png"));
            vm.CloseAllOverlaysCommand.Execute(null);

            // 9. New Asset, Rename ID Preview, and Create Localization Key dialogs
            vm.OpenNewAssetDialogCommand.Execute(null);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "09-dialogs-new-asset-and-rename.png"));

            vm.OpenRenameDialogCommand.Execute(null);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "09b-dialog-rename-preview.png"));

            vm.CloseAllOverlaysCommand.Execute(null);
            vm.RequestDeleteAssetCommand.Execute(vm.SelectedAsset);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "09d-dialog-delete-asset.png"));

            vm.CloseAllOverlaysCommand.Execute(null);
            vm.SelectAsset(AssetKind.Item, "wb:thruster_item", pushHistory: false);
            vm.OpenCreateLocalizationKeyDialogCommand.Execute(null);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "09c-dialog-create-loc-key.png"));
            vm.CloseAllOverlaysCommand.Execute(null);
            vm.SelectAsset(AssetKind.Brick, "wb:thruster", pushHistory: false);

            // 10. Settings, About/Diagnostics, Export Pack, and Draft Recovery dialogs
            vm.OpenSettingsDialogCommand.Execute(null);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "10-dialogs-settings-about-export.png"));

            vm.OpenAboutDialogCommand.Execute(null);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "10b-dialog-about-diagnostics.png"));

            vm.OpenExportDialogCommand.Execute(null);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "10c-dialog-export-pack.png"));

            vm.OpenDraftRecoveryDialogCommand.Execute(null);
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "10d-dialog-draft-recovery.png"));
            vm.CloseAllOverlaysCommand.Execute(null);

            // 16. Save SplitButton + Active Undo/Redo + Clickable Validation Severity Filter Pill
            vm.SelectAsset(AssetKind.Brick, "wb:thruster", pushHistory: false);
            vm.SelectedAsset!.BrickColorField!.Value = "#3B82F6";
            vm.ToggleValidationSeverityFilterCommand.Execute(ValidationSeverity.Broken);
            vm.ExpandAllValidationFiles();
            AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "16-save-split-and-validation-filter.png"));
            vm.ToggleValidationSeverityFilterCommand.Execute(ValidationSeverity.Broken);
        }
        finally
        {
            window.Close();
        }

        // Capture real-repo Wave 2 visual gallery shots (#13, #14, #15, #17)
        CaptureRealRepoWave2Visuals();
    }

    private static void CaptureRealRepoWave2Visuals()
    {
        var repoRoot = FindRepoRoot();
        var tempDataRoot = Path.Combine(Path.GetTempPath(), $"wb-asset-studio-gallery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDataRoot);
        try
        {
            File.WriteAllText(Path.Combine(tempDataRoot, ToolsSettings.ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);
            var dataPaths = ToolsSettings.ToolDataPaths.Resolve(executableDirectory: tempDataRoot);
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            var preview = new StudioGlPreviewController();

            using var vm = new MainWindowViewModel(
                service,
                preview,
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: repoRoot);
            vm.IsDarkTheme = true;

            var window = new MainWindow { DataContext = vm };
            try
            {
                // 13. Connected brick showcase + native Color Picker flyout open
                Assert.True(vm.SelectAsset(AssetKind.Brick, "wb:panel"));
                vm.SelectConnectedShowcasePreviewCommand.Execute(null);
                window.Width = 1280;
                window.Height = 800;
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var swatchButton = window.GetVisualDescendants()
                    .OfType<Button>()
                    .FirstOrDefault(b => b.Classes.Contains("studio-color-swatch") && b.IsEffectivelyVisible);
                if (swatchButton?.Flyout != null)
                {
                    swatchButton.Flyout.ShowAt(swatchButton);
                    Dispatcher.UIThread.RunJobs();
                }

                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "13-connected-brick-and-color-picker.png"));
                swatchButton?.Flyout?.Hide();
                vm.SelectStandardBrickPreviewCommand.Execute(null);

                // 14. Body state & sprite editor + Body preview slider/stepper
                Assert.True(vm.SelectAsset(AssetKind.Body, "wb:human_m"));
                vm.NextBodyPreviewFrameCommand.Execute(null);
                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "14-body-editor-and-preview-slider.png"));

                // 15. Texture dropdown with inline 22x22 thumbnails
                Assert.True(vm.SelectAsset(AssetKind.Item, "wb:drill"));
                vm.SelectedAsset!.IconField!.IsPickerOpen = true;
                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "15-texture-dropdown-thumbnails.png"));
                vm.SelectedAsset.IconField.IsPickerOpen = false;

                // 17. Material real texture preview + real workspace shader dropdown
                Assert.True(vm.SelectAsset(AssetKind.Material, "bricks/glass"));
                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "17-item-and-material-gl-preview.png"));

                // 19. Body editor with recursive workspace thumbnail grid popup open + Browse button
                Assert.True(vm.SelectAsset(AssetKind.Body, "wb:human_m"));
                Dispatcher.UIThread.RunJobs();
                var spritePicker = window.GetVisualDescendants()
                    .OfType<BodySpritePickerControl>()
                    .FirstOrDefault(p => p.IsEffectivelyVisible);
                if (spritePicker != null)
                {
                    spritePicker.IsPickerOpen = true;
                    Dispatcher.UIThread.RunJobs();
                }

                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "19-body-sprite-grid-picker.png"));
                if (spritePicker != null)
                {
                    spritePicker.IsPickerOpen = false;
                    Dispatcher.UIThread.RunJobs();
                }

                // 20. Localization editor showing editable keys + bottom + Add Key row (skills.toml fits cleanly above fold)
                var locDoc = vm.AllDocuments.First(d => d.Kind == AssetKind.Localization && d.Id.Contains("skills", StringComparison.OrdinalIgnoreCase));
                Assert.True(vm.SelectDocument(locDoc, pushHistory: false));
                vm.IsTomlViewActive = false;
                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "20-localization-editable-keys.png"));

                // 21. Real repo texture paint preview (wb:caution_panel) showing Paint Texture Variants visual picker + real texture swatch
                Assert.True(vm.SelectAsset(AssetKind.Paint, "wb:caution_panel"));
                vm.IsTomlViewActive = false;
                vm.SelectedAsset!.PaintVariantsField!.IsPickerOpen = true;
                Dispatcher.UIThread.RunJobs();
                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "21-texture-paint-preview.png"));
                vm.SelectedAsset.PaintVariantsField.IsPickerOpen = false;

                // 22. Real repo builtin/mainMenu blueprint showing populated Brick & Paint Palette + USED BRICKS
                Assert.True(vm.SelectAsset(AssetKind.Blueprint, "builtin/mainMenu"));
                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "22-blueprint-mainmenu-palette.png"));

                // 23–25. Item Form 'Edit Visually' buttons + full-workbench ViewModel & WorldModel Visual Editors
                Assert.True(vm.SelectAsset(AssetKind.Item, "wb:drill"));
                vm.IsTomlViewActive = false;
                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "25-item-form-edit-visually.png"));

                vm.OpenViewModelEditorCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "23-item-viewmodel-editor.png"));

                vm.IsDarkTheme = false;
                Dispatcher.UIThread.RunJobs();
                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "24b-item-viewmodel-editor-light.png"));
                vm.IsDarkTheme = true;

                vm.ActiveModelEditor!.SelectWorldModelModeCommand.Execute(null);
                vm.ActiveModelEditor.SetGizmoModeCommand.Execute(Rendering.TransformGizmoMode.Rotate);
                Dispatcher.UIThread.RunJobs();
                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(window, "24-item-worldmodel-editor.png"));

                vm.CloseModelEditorCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
            }
            finally
            {
                window.Close();
            }

            // 18 & 18b. Freshly created empty pack & uninitialized non-pack folder states
            var emptyPackDir = Path.Combine(tempDataRoot, "frontier_pack");
            var nonPackDir = Path.Combine(tempDataRoot, "uninitialized_folder");
            Directory.CreateDirectory(nonPackDir);
            Assert.True(service.CreatePackWorkspace(emptyPackDir, "frontier_pack", "frontier", "Frontier Pack", "Tester").Success);

            using var emptyVm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: emptyPackDir);
            emptyVm.IsDarkTheme = true;

            var emptyWindow = new MainWindow { DataContext = emptyVm };
            try
            {
                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(emptyWindow, "18-empty-pack-workspace.png"));

                emptyVm.OpenWorkspace(WorkspaceMode.Pack, nonPackDir);
                AssertCaptured(HeadlessRenderHelper.CaptureWindowPng(emptyWindow, "18b-non-pack-folder-state.png"));
            }
            finally
            {
                emptyWindow.Close();
            }

            service.MarkCleanShutdown();
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDataRoot))
                {
                    Directory.Delete(tempDataRoot, recursive: true);
                }
            }
            catch (IOException)
            {
                // Ignore transient cleanup locks.
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "Swordfish.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Swordfish.sln.");
    }

    private static void AssertCaptured(string path)
    {
        Assert.True(File.Exists(path), $"Expected rendered PNG at {path}");
        var length = new FileInfo(path).Length;
        Assert.True(length > 4096, $"PNG at {path} was unexpectedly small ({length} bytes)");
    }
}
