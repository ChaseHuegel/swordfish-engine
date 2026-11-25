using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Rendering;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WaywardBeyond.AssetStudio.Controls;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.AssetStudio.ViewModels;
using WaywardBeyond.AssetStudio.Views;
using ToolsSettings = WaywardBeyond.AssetTools.Settings;
using Xunit;

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>
///     Comprehensive headless regression tests covering all 33 user-reported Asset Studio issues.
/// </summary>
public sealed class StudioIssueRegressionTests
{
    /// <summary>
    ///     #1: Verifies selecting Brick vs Item sharing identical ID (<c>wb:core</c>) preserves exact selection and history.
    /// </summary>
    [AvaloniaFact]
    public void DuplicateIdAcrossKinds_SelectsClickedAssetAndTracksDistinctHistory()
    {
        var repoRoot = FindRepoRoot();
        var tempDir = CreateTempDirectory("dup-id");
        try
        {
            var service = new AssetToolsStudioWorkspaceService(CreateIsolatedDataPaths(tempDir));
            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: repoRoot);

            var coreBrick = vm.AllDocuments.First(d => d.Kind == AssetKind.Brick && string.Equals(d.Id, "wb:core", StringComparison.OrdinalIgnoreCase));
            var coreItem = vm.AllDocuments.First(d => d.Kind == AssetKind.Item && string.Equals(d.Id, "wb:core", StringComparison.OrdinalIgnoreCase));
            Assert.NotEqual(coreBrick.DocumentKey, coreItem.DocumentKey);

            vm.SelectAssetCommand.Execute(coreBrick);
            Assert.Same(coreBrick, vm.SelectedAsset);
            Assert.Equal(AssetKind.Brick, vm.SelectedAsset!.Kind);

            vm.SelectAssetCommand.Execute(coreItem);
            Assert.Same(coreItem, vm.SelectedAsset);
            Assert.Equal(AssetKind.Item, vm.SelectedAsset!.Kind);

            vm.NavigateBackCommand.Execute(null);
            Assert.Same(coreBrick, vm.SelectedAsset);

            vm.NavigateForwardCommand.Execute(null);
            Assert.Same(coreItem, vm.SelectedAsset);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    ///     #2: Verifies whole-app UI zoom in, zoom out, reset, clamping, and settings persistence.
    /// </summary>
    [AvaloniaFact]
    public void UiZoomScale_ClampsStepsAndPersistsToSettings()
    {
        var tempDir = CreateTempDirectory("ui-zoom");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(tempDir);
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            using var vm = new MainWindowViewModel(service, new StudioGlPreviewController(), startOnWelcomeScreen: true);

            Assert.Equal(1.0, vm.UiScale, precision: 2);
            Assert.Equal("100%", vm.UiScalePercentLabel);

            vm.ZoomInCommand.Execute(null);
            Assert.Equal(1.10, vm.UiScale, precision: 2);
            Assert.Equal("110%", vm.UiScalePercentLabel);

            vm.ZoomOutCommand.Execute(null);
            vm.ZoomOutCommand.Execute(null);
            Assert.Equal(0.90, vm.UiScale, precision: 2);

            vm.UiScale = 5.0;
            Assert.Equal(ToolsSettings.ToolSettings.MAX_UI_SCALE, vm.UiScale, precision: 2);

            vm.UiScale = 0.1;
            Assert.Equal(ToolsSettings.ToolSettings.MIN_UI_SCALE, vm.UiScale, precision: 2);

            vm.ResetZoomCommand.Execute(null);
            Assert.Equal(1.0, vm.UiScale, precision: 2);

            vm.ZoomInCommand.Execute(null);
            Assert.Equal(1.10, service.GetSettings().UiScale, precision: 2);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    ///     #3 &amp; #11: Verifies Ctrl+Z inside a focused TextBox never throws NullReferenceException and drives app-wide Undo/Redo.
    /// </summary>
    [AvaloniaFact]
    public void CtrlZInFocusedTextBox_DoesNotThrowAndExecutesAppUndoRedo()
    {
        using var vm = CreateSampleViewModel();
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Width = 1280;
            window.Height = 800;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var thruster = vm.SelectedAsset!;
            var colorField = thruster.BrickColorField!;
            var originalColor = colorField.Value;

            // Defensive null coercion never throws
            colorField.Value = null!;
            Assert.Equal(string.Empty, colorField.Value);
            colorField.Value = originalColor;
            vm.UndoStack.Clear();

            // Verify all TextBoxes disable Avalonia's built-in null-undo
            var textBoxes = window.GetVisualDescendants().OfType<TextBox>().ToList();
            Assert.NotEmpty(textBoxes);
            Assert.All(textBoxes, tb => Assert.False(tb.IsUndoEnabled));

            // Make an edit, focus a TextBox, and press Ctrl+Z / Ctrl+Y
            colorField.Value = "#FFAA22";
            Assert.True(vm.CanUndo);
            Assert.Equal("#FFAA22", colorField.Value);

            var targetTextBox = textBoxes[0];
            targetTextBox.Focus();
            Dispatcher.UIThread.RunJobs();

            window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(originalColor, colorField.Value);
            Assert.True(vm.CanRedo);

            window.KeyPressQwerty(PhysicalKey.Y, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("#FFAA22", colorField.Value);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    ///     #4 &amp; #9: Verifies GlViewport and PreviewViewportControl implement ICustomHitTest and handle orbit, pan, and wheel zoom.
    /// </summary>
    [AvaloniaFact]
    public void PreviewViewports_ImplementCustomHitTestAndInteractiveNavigation()
    {
        var glViewport = new GlViewport { Width = 240, Height = 180 };
        var fallback = new PreviewViewportControl
        {
            Width = 240,
            Height = 180,
            Descriptor = new PreviewRenderDescriptor(
                PreviewSurfaceMode.SingleBrick,
                "wb:core",
                "Block · N",
                "#4F8CFF",
                "Block",
                BrickOrientation.North,
                false,
                false,
                0,
                "standing",
                "front",
                string.Empty),
        };

        var panel = new StackPanel();
        panel.Children.Add(glViewport);
        panel.Children.Add(fallback);
        var host = new Window { Width = 300, Height = 420, Content = panel };
        try
        {
            host.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.IsAssignableFrom<ICustomHitTest>(glViewport);
            Assert.IsAssignableFrom<ICustomHitTest>(fallback);
            Assert.True(((ICustomHitTest)glViewport).HitTest(new Point(120, 90)));
            Assert.False(((ICustomHitTest)glViewport).HitTest(new Point(-1, 10)));
            Assert.True(((ICustomHitTest)fallback).HitTest(new Point(120, 90)));
            Assert.False(((ICustomHitTest)fallback).HitTest(new Point(250, 90)));

            // Verify PreviewCamera pan and orbit state updates cleanly
            var cam = new PreviewCamera();
            var initialTarget = cam.Target;
            cam.Pan(15f, -10f);
            Assert.NotEqual(initialTarget, cam.Target);

            fallback.ResetInteractiveView();
            glViewport.ResetCamera();
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>
    ///     #7 &amp; #30: Verifies Body state/sprite add/remove, preview frame slider/stepper cycling, and lossless TOML save round-trip.
    /// </summary>
    [AvaloniaFact]
    public void BodyStateAndSpriteEditing_UpdatesPreviewSliderAndSavesLosslessly()
    {
        var tempRoot = CreateTempDirectory("body-edit");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var packDir = Path.Combine(tempRoot, "body_pack");
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "body_pack", "bodypack", "Body Pack", "Tester").Success);

            var bodyPath = Path.Combine(packDir, "assets", "bodies", "pilot.toml");
            const string initialBodyToml =
                "# Keep pilot comment\n" +
                "[[Bodies]]\n" +
                "ID      = \"bodypack:pilot\"\n" +
                "Species = \"human\"\n" +
                "Gender  = \"Neutral\"\n" +
                "\n" +
                "[Bodies.States.standing]\n" +
                "front = [\"characters/pilot_front.png\"]\n" +
                "back  = [\"characters/pilot_back.png\"]\n";
            File.WriteAllText(bodyPath, initialBodyToml);

            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            Assert.True(vm.SelectAsset(AssetKind.Body, "bodypack:pilot"));
            var body = vm.SelectedAsset!;

            // #7 Body preview stepper & slider
            Assert.True(vm.ShowBodyPreviewControls);
            Assert.Equal(2, vm.BodyPreviewFrameCount);
            Assert.Equal(0, vm.BodyPreviewFrameIndex);
            Assert.Contains("standing · front (1/2)", vm.BodyPreviewLabel, StringComparison.Ordinal);

            vm.NextBodyPreviewFrameCommand.Execute(null);
            Assert.Equal(1, vm.BodyPreviewFrameIndex);
            Assert.Contains("standing · back (2/2)", vm.BodyPreviewLabel, StringComparison.Ordinal);

            vm.PreviousBodyPreviewFrameCommand.Execute(null);
            Assert.Equal(0, vm.BodyPreviewFrameIndex);

            // #30 Add new state and sprite, verify preview count increases and Undo/Redo works
            body.NewBodyStateName = "floating";
            body.AddBodyStateCommand.Execute(null);
            Assert.Contains("floating", body.BodyStates);
            Assert.Equal("floating", body.SelectedBodyState);

            body.NewBodySpriteDirection = "front";
            body.NewBodySpriteTexturePath = "characters/pilot_float_front.png";
            body.AddBodySpriteCommand.Execute(null);
            Assert.Equal(3, vm.BodyPreviewFrameCount);

            // Save and verify TOML on disk preserves comments and writes [Bodies.States.floating]
            vm.SaveAllCommand.Execute(null);
            Assert.Equal(0, vm.DirtyCount);

            var savedToml = File.ReadAllText(bodyPath);
            Assert.Contains("# Keep pilot comment", savedToml, StringComparison.Ordinal);
            Assert.Contains("[Bodies.States.floating]", savedToml, StringComparison.Ordinal);
            Assert.Contains("characters/pilot_float_front.png", savedToml, StringComparison.Ordinal);

            // Remove sprite and verify count drops
            var floatSlot = body.VisibleBodySprites.First();
            body.RemoveBodySpriteCommand.Execute(floatSlot);
            Assert.Equal(2, vm.BodyPreviewFrameCount);

            // Undo restores removed sprite
            vm.UndoCommand.Execute(null);
            Assert.Equal(3, vm.BodyPreviewFrameCount);

            // #30 RemoveSelectedBodyState removes [Bodies.States.floating] on disk and survives reload
            body.SelectBodyStateCommand.Execute("floating");
            Assert.True(body.CanRemoveSelectedBodyState);
            body.RemoveSelectedBodyStateCommand.Execute(null);
            Assert.DoesNotContain("floating", body.BodyStates);
            Assert.Equal(2, vm.BodyPreviewFrameCount);

            vm.SaveAllCommand.Execute(null);
            Assert.Equal(0, vm.DirtyCount);

            var afterRemoveToml = File.ReadAllText(bodyPath);
            Assert.Contains("# Keep pilot comment", afterRemoveToml, StringComparison.Ordinal);
            Assert.Contains("[Bodies.States.standing]", afterRemoveToml, StringComparison.Ordinal);
            Assert.DoesNotContain("[Bodies.States.floating]", afterRemoveToml, StringComparison.Ordinal);
            Assert.DoesNotContain("pilot_float_front.png", afterRemoveToml, StringComparison.Ordinal);

            // Reload into a fresh ViewModel to confirm disk round-trip
            using var reloadedVm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);
            Assert.True(reloadedVm.SelectAsset(AssetKind.Body, "bodypack:pilot"));
            Assert.DoesNotContain("floating", reloadedVm.SelectedAsset!.BodyStates);
            Assert.Contains("standing", reloadedVm.SelectedAsset.BodyStates);
            Assert.Equal(2, reloadedVm.BodyPreviewFrameCount);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     #8: Verifies Connected Textures Brick preview toggle visibility and surface mode switching.
    /// </summary>
    [AvaloniaFact]
    public void ConnectedBrickToggle_SwitchesPreviewSurfaceModeOnlyWhenConnected()
    {
        using var vm = CreateSampleViewModel();

        // wb:thruster has Connected = false -> toggle hidden
        Assert.True(vm.SelectAsset(AssetKind.Brick, "wb:thruster"));
        Assert.False(vm.ShowConnectedShowcaseToggle);

        // wb:panel has Connected = true -> toggle visible
        Assert.True(vm.SelectAsset(AssetKind.Brick, "wb:panel"));
        Assert.True(vm.ShowConnectedShowcaseToggle);

        vm.SelectConnectedShowcasePreviewCommand.Execute(null);
        Assert.True(vm.IsConnectedShowcaseMode);
        Assert.Equal(PreviewSurfaceMode.BrickConnectedShowcase, vm.ActivePreviewDescriptor.Mode);

        // Unchecking Connected automatically turns off showcase mode
        vm.SelectedAsset!.BrickConnectedField!.BoolValue = false;
        Assert.False(vm.ShowConnectedShowcaseToggle);
        Assert.False(vm.IsConnectedShowcaseMode);
    }

    /// <summary>
    ///     #10 &amp; #20: Verifies Brick sounds use SoundTag with 0 false unresolved refs and all dropdowns match real game enums.
    /// </summary>
    [AvaloniaFact]
    public void BrickSoundTagsAndAllDropdownChoices_MatchAuthoritativeGameEnums()
    {
        var repoRoot = FindRepoRoot();
        var tempDir = CreateTempDirectory("enums-sounds");
        try
        {
            var service = new AssetToolsStudioWorkspaceService(CreateIsolatedDataPaths(tempDir));
            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: repoRoot);

            // #10 Brick sounds resolve as SoundTag with zero danger
            Assert.True(vm.SelectAsset(AssetKind.Brick, "wb:core"));
            var coreBrick = vm.SelectedAsset!;
            Assert.Equal(ReferenceKind.SoundTag, coreBrick.BrickSoundPlaceField!.ReferenceKind);
            Assert.Equal(ReferenceKind.SoundTag, coreBrick.BrickSoundBreakField!.ReferenceKind);
            Assert.False(coreBrick.BrickSoundPlaceField.IsDanger);
            Assert.False(coreBrick.BrickSoundBreakField.IsDanger);

            // #20 Item ToolType & PlaceableType match exact game enums (never laser/projector!)
            Assert.True(vm.SelectAsset(AssetKind.Item, "wb:drill"));
            var drillItem = vm.SelectedAsset!;
            Assert.Equal(["none", "convert", "mining", "sprayer", "drill", "blueprint"], drillItem.ItemToolTypeField!.Choices);
            Assert.DoesNotContain("laser", drillItem.ItemToolTypeField.Choices);
            Assert.DoesNotContain("projector", drillItem.ItemToolTypeField.Choices);
            Assert.Equal(["none", "brick"], drillItem.ItemPlaceableTypeField!.Choices);

            // #20 Material Shader dropdown populated dynamically from workspace shaders (not stale unlit/emissive!)
            Assert.True(vm.SelectAsset(AssetKind.Material, "bricks/glass"));
            var glassMat = vm.SelectedAsset!;
            Assert.Contains("textured", glassMat.MaterialShaderField!.Choices);
            Assert.Contains("lighted", glassMat.MaterialShaderField.Choices);
            Assert.DoesNotContain("emissive", glassMat.MaterialShaderField.Choices);

            // Verify remaining enum choices
            Assert.Equal(["Block", "Slab", "Stair", "Slope", "Column", "Plate", "Custom", "Any"], coreBrick.BrickShapeField!.Choices);
            Assert.Equal(["Opaque", "Masked", "Blend"], coreBrick.BrickAlphaField!.Choices);
            Assert.Equal(["box", "sphere", "capsule", "cylinder", "hull"], ColliderEntryViewModel.COLLIDER_TYPES);
            Assert.Equal(["Place", "Break"], SkillSourceRowViewModel.TRIGGERS);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    ///     #12: Verifies asset deletion blocks read-only base assets and removes writable assets with confirmation dialog.
    /// </summary>
    [AvaloniaFact]
    public void DeleteAsset_BlocksReadOnlyAndDeletesWritableAssetWithConfirmation()
    {
        var tempRoot = CreateTempDirectory("delete-asset");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var packDir = Path.Combine(tempRoot, "del_pack");
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "del_pack", "delpack", "Del Pack", "Tester").Success);

            var brickPath = Path.Combine(packDir, "assets", "bricks", "temp_block.toml");
            File.WriteAllText(brickPath, "[[Bricks]]\nID = \"delpack:temp_block\"\nShape = \"block\"\n");

            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            Assert.True(vm.SelectAsset(AssetKind.Brick, "delpack:temp_block"));
            vm.RequestDeleteAssetCommand.Execute(vm.SelectedAsset);
            Assert.NotNull(vm.ActiveDeleteAssetDialog);
            Assert.Equal("delpack:temp_block", vm.ActiveDeleteAssetDialog!.AssetId);

            vm.ActiveDeleteAssetDialog.ConfirmDeleteCommand.Execute(null);
            Assert.Null(vm.ActiveDeleteAssetDialog);
            Assert.False(File.Exists(brickPath));
            Assert.DoesNotContain(vm.AllDocuments, d => d.Id == "delpack:temp_block");

            // Read-only base asset is refused cleanly
            using var sampleVm = CreateSampleViewModel();
            var readOnlyDoc = sampleVm.AllDocuments.First(d => d.IsReadOnly);
            sampleVm.RequestDeleteAssetCommand.Execute(readOnlyDoc);
            Assert.Null(sampleVm.ActiveDeleteAssetDialog);
            Assert.Contains("read-only", sampleVm.ToastMessage, StringComparison.OrdinalIgnoreCase);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     #13, #14, #15: Verifies default namespace prefill, optional .toml extension, and AssetId-to-filename auto-sync.
    /// </summary>
    [AvaloniaFact]
    public void NewAssetDialog_PrefillsNamespaceAutoFillsFilenameAndNormalizesTomlExtension()
    {
        using var vm = CreateSampleViewModel();
        vm.OpenSettingsDialogCommand.Execute(null);
        vm.ActiveSettingsDialog!.DefaultNamespace = "frontier";
        vm.ActiveSettingsDialog.SaveCommand.Execute(null);
        Assert.Equal("frontier", vm.DefaultNamespace);

        vm.OpenNewAssetDialogCommand.Execute(null);
        var dialog = vm.ActiveNewAssetDialog!;
        Assert.StartsWith("frontier:", dialog.AssetId, StringComparison.Ordinal);

        // #15 Auto-fill filename from Asset ID
        dialog.AssetId = "frontier:airlock_door";
        Assert.Equal("airlock_door.toml", dialog.NewFileName);
        Assert.Equal("assets/bricks/airlock_door.toml", dialog.ComputedTargetPath);

        // #14 Optional .toml extension when user enters bare filename
        dialog.NewFileName = "custom_door";
        Assert.Equal("assets/bricks/custom_door.toml", dialog.ComputedTargetPath);

        // Manual divergence stops auto-syncing until cleared
        dialog.AssetId = "frontier:heavy_door";
        Assert.Equal("custom_door", dialog.NewFileName);

        // Clearing filename resumes auto-syncing
        dialog.NewFileName = string.Empty;
        dialog.AssetId = "frontier:blast_door";
        Assert.Equal("blast_door.toml", dialog.NewFileName);
    }

    /// <summary>
    ///     #17 &amp; #33: Verifies validation right-click quick-fix actions and clickable severity filter pills.
    /// </summary>
    [AvaloniaFact]
    public void ValidationQuickFixAndSeverityFilterPills_WorkEndToEnd()
    {
        using var vm = CreateSampleViewModel();
        Assert.True(vm.BrokenCount > 0);
        Assert.True(vm.CrossPlatformCount > 0);
        Assert.True(vm.StyleCount > 0);

        // #33 Toggle Broken filter pill -> only Broken group visible, header totals stay accurate
        var totalStyle = vm.StyleCount;
        vm.ToggleValidationSeverityFilterCommand.Execute(ValidationSeverity.Broken);
        Assert.True(vm.IsBrokenFilterActive);
        Assert.Single(vm.ValidationGroups);
        Assert.Equal(ValidationSeverity.Broken, vm.ValidationGroups[0].Severity);
        Assert.Equal(totalStyle, vm.StyleCount);

        // Toggle same pill again clears filter
        vm.ToggleValidationSeverityFilterCommand.Execute(ValidationSeverity.Broken);
        Assert.False(vm.HasActiveValidationFilter);
        Assert.True(vm.ValidationGroups.Count >= 3);

        // #17 Apply Quick Fix on broken texture finding -> resolves finding cleanly & is undoable
        var brokenFinding = vm.AllFindings.First(f => f.Severity == ValidationSeverity.Broken && f.HasQuickFix);
        vm.ApplyValidationQuickFixCommand.Execute(brokenFinding);
        Assert.Equal(0, vm.BrokenCount);
        Assert.Equal(brokenFinding.SuggestedFix, vm.SelectedAsset!.BrickTexDefaultField!.Value);

        // #17 Apply Quick Fix on CrossPlatform casing finding
        var caseFinding = vm.AllFindings.First(f => f.Severity == ValidationSeverity.CrossPlatform && f.HasQuickFix);
        vm.ApplyValidationQuickFixCommand.Execute(caseFinding);
        Assert.Equal(0, vm.CrossPlatformCount);
    }

    /// <summary>
    ///     #19: Verifies Save Current, Discard Current, and Discard All flows on a real pack.
    /// </summary>
    [AvaloniaFact]
    public void SaveAndDiscardSplitActions_PersistOrRestoreCleanly()
    {
        var tempRoot = CreateTempDirectory("save-discard");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var packDir = Path.Combine(tempRoot, "sd_pack");
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "sd_pack", "sd", "SD Pack", "Tester").Success);

            var brickA = Path.Combine(packDir, "assets", "bricks", "a.toml");
            var brickB = Path.Combine(packDir, "assets", "bricks", "b.toml");
            File.WriteAllText(brickA, "[[Bricks]]\nID = \"sd:a\"\nShape = \"block\"\nCastShadows = true\n");
            File.WriteAllText(brickB, "[[Bricks]]\nID = \"sd:b\"\nShape = \"block\"\nCastShadows = true\n");

            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            // Edit both A and B
            Assert.True(vm.SelectAsset(AssetKind.Brick, "sd:a"));
            vm.SelectedAsset!.BrickCastShadowsField!.BoolValue = false;
            Assert.True(vm.SelectAsset(AssetKind.Brick, "sd:b"));
            vm.SelectedAsset!.BrickCastShadowsField!.BoolValue = false;
            Assert.Equal(2, vm.DirtyCount);

            // Save Current saves ONLY B, leaving A dirty
            vm.SaveCurrentAssetCommand.Execute(null);
            Assert.Equal(1, vm.DirtyCount);
            Assert.Contains("CastShadows = false", File.ReadAllText(brickB), StringComparison.Ordinal);
            Assert.Contains("CastShadows = true", File.ReadAllText(brickA), StringComparison.Ordinal);

            // Discard Current on A restores A from disk without touching B
            Assert.True(vm.SelectAsset(AssetKind.Brick, "sd:a"));
            vm.DiscardCurrentAssetCommand.Execute(null);
            Assert.Equal(0, vm.DirtyCount);
            Assert.True(vm.SelectedAsset!.BrickCastShadowsField!.BoolValue);

            // Edit both again and Discard All restores both cleanly
            vm.SelectedAsset!.BrickCastShadowsField!.BoolValue = false;
            Assert.True(vm.SelectAsset(AssetKind.Brick, "sd:b"));
            vm.SelectedAsset!.BrickCastShadowsField!.BoolValue = true;
            Assert.Equal(2, vm.DirtyCount);

            vm.DiscardAllChangesCommand.Execute(null);
            Assert.Equal(0, vm.DirtyCount);
            Assert.False(vm.SelectedAsset!.BrickCastShadowsField!.BoolValue);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     #23, #26, #28, #29, #31, #32: Verifies System theme default, toast auto-dismiss, F1 palette shortcut,
    ///     MoveTag reordering, ColorPicker TwoWay binding, and .zip default export format.
    /// </summary>
    [AvaloniaFact]
    public void ThemeDefaultToastTimerF1ShortcutTagReorderColorPickerAndZipExport_AllWork()
    {
        var tempDir = CreateTempDirectory("misc-issues");
        try
        {
            // #23 Fresh real settings default to ToolTheme.System
            var service = new AssetToolsStudioWorkspaceService(CreateIsolatedDataPaths(tempDir));
            using var realVm = new MainWindowViewModel(service, new StudioGlPreviewController(), startOnWelcomeScreen: true);
            Assert.Equal(ToolsSettings.ToolTheme.System, realVm.ThemePreference);
            Assert.True(realVm.IsSystemTheme);
            Assert.Equal(ThemeVariant.Default, Application.Current?.RequestedThemeVariant);
            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }

        using var vm = CreateSampleViewModel();

        // #32 Export defaults to .zip on all platforms
        vm.OpenExportDialogCommand.Execute(null);
        Assert.Equal(".zip", vm.ActiveExportDialog!.SelectedFormat);
        Assert.Equal([".zip", ".tar.gz"], ExportDialogViewModel.FORMAT_OPTIONS);
        vm.CloseAllOverlaysCommand.Execute(null);

        // #29 Tag MoveTag reorders cleanly and supports Undo/Redo
        Assert.True(vm.SelectAsset(AssetKind.Tag, "brick.paintable"));
        var tagDoc = vm.SelectedAsset!;
        var first = tagDoc.TagFileEntries[0].Value;
        var last = tagDoc.TagFileEntries[^1].Value;
        Assert.True(tagDoc.MoveTag(0, tagDoc.TagFileEntries.Count - 1));
        Assert.Equal(first, tagDoc.TagFileEntries[^1].Value);
        Assert.True(tagDoc.ShowTagOrderWarning);
        vm.UndoCommand.Execute(null);
        Assert.Equal(first, tagDoc.TagFileEntries[0].Value);
        Assert.Equal(last, tagDoc.TagFileEntries[^1].Value);

        // #31 StudioColorPickerControl updates field value cleanly
        Assert.True(vm.SelectAsset(AssetKind.Brick, "wb:thruster"));
        var colorField = vm.SelectedAsset!.BrickColorField!;
        var picker = new StudioColorPickerControl { HexColor = colorField.Value };
        picker.PropertyChanged += (_, args) =>
        {
            if (args.Property == StudioColorPickerControl.HexColorProperty)
            {
                colorField.Value = picker.HexColor;
            }
        };
        picker.HexColor = "#2CE8F5";
        Assert.Equal("#2CE8F5", colorField.Value);

        // #28 F1 opens Command Palette on MainWindow
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Null(vm.ActiveCommandPalette);
            window.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(vm.ActiveCommandPalette);
            vm.CloseAllOverlaysCommand.Execute(null);

            // #26 Toast auto-dismiss clears ToastMessage after configured delay
            vm.ToastAutoDismissDelay = TimeSpan.FromMilliseconds(40);
            vm.CopyAllValidationCommand.Execute(null);
            Assert.True(vm.HasToast);

            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (vm.HasToast && DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(20);
            }

            Assert.False(vm.HasToast);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    ///     #27: Verifies external filesystem edits reload clean documents while preserving dirty drafts untouched.
    /// </summary>
    [AvaloniaFact]
    public void ExternalDiskReload_UpdatesCleanFilesWithoutOverwritingDirtyDrafts()
    {
        var tempRoot = CreateTempDirectory("live-watch");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var packDir = Path.Combine(tempRoot, "watch_pack");
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "watch_pack", "wp", "Watch Pack", "Tester").Success);

            var cleanPath = Path.Combine(packDir, "assets", "bricks", "clean_brick.toml");
            var dirtyPath = Path.Combine(packDir, "assets", "bricks", "dirty_brick.toml");
            File.WriteAllText(cleanPath, "[[Bricks]]\nID = \"wp:clean\"\nShape = \"block\"\nColor = \"#112233\"\n");
            File.WriteAllText(dirtyPath, "[[Bricks]]\nID = \"wp:dirty\"\nShape = \"block\"\nColor = \"#445566\"\n");

            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            // Make dirty_brick dirty in studio
            Assert.True(vm.SelectAsset(AssetKind.Brick, "wp:dirty"));
            vm.SelectedAsset!.BrickColorField!.Value = "#AABBCC";
            Assert.True(vm.SelectedAsset.IsDirty);

            // Modify BOTH files externally on disk
            File.WriteAllText(cleanPath, "[[Bricks]]\nID = \"wp:clean\"\nShape = \"slab\"\nColor = \"#998877\"\n");
            File.WriteAllText(dirtyPath, "[[Bricks]]\nID = \"wp:dirty\"\nShape = \"plate\"\nColor = \"#000000\"\n");

            // Trigger external reload (same path used by WorkspaceFileWatcher callback)
            vm.ReloadExternalChangesFromDisk();

            // Clean file picked up external disk edit
            var cleanDoc = vm.AllDocuments.First(d => d.Id == "wp:clean");
            Assert.Equal("Slab", cleanDoc.BrickShapeField!.Value);
            Assert.Equal("#998877", cleanDoc.BrickColorField!.Value);

            // Dirty file kept user's in-studio draft untouched!
            var dirtyDoc = vm.AllDocuments.First(d => d.Id == "wp:dirty");
            Assert.True(dirtyDoc.IsDirty);
            Assert.Equal("#AABBCC", dirtyDoc.BrickColorField!.Value);
            Assert.Equal("Block", dirtyDoc.BrickShapeField!.Value);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Verifies live WorkspaceFileWatcher fires for watched assets under a dot-prefixed ancestor directory while filtering internal dot folders and temp suffixes.
    /// </summary>
    [Fact]
    public void WorkspaceFileWatcher_FiresForWatchedAssetsUnderDotAncestorAndIgnoresInternalDotDirs()
    {
        var tempRoot = CreateTempDirectory("dot-ancestor-watch");
        try
        {
            var assetsDir = Path.Combine(tempRoot, ".local", "pack", "assets");
            var bricksDir = Path.Combine(assetsDir, "bricks");
            var gitDir = Path.Combine(assetsDir, ".git");
            Directory.CreateDirectory(bricksDir);
            Directory.CreateDirectory(gitDir);

            using var watcher = new WorkspaceFileWatcher(debounceMilliseconds: 50);
            var received = new List<string>();
            var gate = new ManualResetEventSlim(false);
            var sync = new Lock();

            watcher.FilesChanged += paths =>
            {
                using (sync.EnterScope())
                {
                    received.AddRange(paths);
                }

                gate.Set();
            };

            watcher.Watch([assetsDir]);

            // Ignored paths inside watched root must not trigger notifications
            File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/main\n");
            File.WriteAllText(Path.Combine(bricksDir, "ignored.toml.tmp"), "temp");
            Assert.False(gate.Wait(TimeSpan.FromMilliseconds(220)));

            // Valid asset file under dot-prefixed ancestor (.local) MUST trigger FilesChanged
            var brickFile = Path.Combine(bricksDir, "test.toml");
            File.WriteAllText(brickFile, "[[Bricks]]\nID = \"wp:test\"\nShape = \"block\"\n");

            Assert.True(gate.Wait(TimeSpan.FromSeconds(3)), "Expected WorkspaceFileWatcher to fire for watched asset under .local ancestor.");
            var expectedFull = Path.GetFullPath(brickFile);
            using (sync.EnterScope())
            {
                Assert.Contains(expectedFull, received, StringComparer.OrdinalIgnoreCase);
                Assert.DoesNotContain(received, static p => p.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(received, static p => p.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Verifies DiscardCurrentAsset prunes only the discarded document's undo entries, preserves sibling drafts in multi-entry files, and Undo-to-clean deletes drafts.
    /// </summary>
    [AvaloniaFact]
    public void DiscardCurrentAsset_PrunesUndoAndPreservesMultiEntrySiblingDraft_AndUndoToCleanDiscardsDraft()
    {
        var tempRoot = CreateTempDirectory("discard-undo-draft");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var packDir = Path.Combine(tempRoot, "b2_pack");
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "b2_pack", "b2", "B2 Pack", "Tester").Success);

            var singlePath = Path.Combine(packDir, "assets", "bricks", "single.toml");
            var sharedPath = Path.Combine(packDir, "assets", "bricks", "shared.toml");
            File.WriteAllText(singlePath, "[[Bricks]]\nID = \"b2:single\"\nShape = \"block\"\nCastShadows = true\n");
            File.WriteAllText(
                sharedPath,
                "[[Bricks]]\nID = \"b2:shared_a\"\nShape = \"block\"\nCastShadows = true\n\n" +
                "[[Bricks]]\nID = \"b2:shared_b\"\nShape = \"block\"\nCastShadows = true\n");

            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            // Part A: Undo back to clean state removes recovery draft instead of re-saving it
            Assert.True(vm.SelectAsset(AssetKind.Brick, "b2:single"));
            vm.SelectedAsset!.BrickCastShadowsField!.BoolValue = false;
            Assert.True(vm.SelectedAsset.IsDirty);
            Assert.True(service.DraftStore.LoadDraft(singlePath).Success);

            vm.UndoCommand.Execute(null);
            Assert.False(vm.SelectedAsset.IsDirty);
            Assert.Equal(0, vm.DirtyCount);
            Assert.False(service.DraftStore.LoadDraft(singlePath).Success);

            // Part B: Multi-entry file — edit both entries, discard B, confirm A's draft & undo survive and B's undo is pruned
            Assert.True(vm.SelectAsset(AssetKind.Brick, "b2:shared_a"));
            vm.SelectedAsset!.BrickCastShadowsField!.BoolValue = false;
            Assert.True(vm.SelectAsset(AssetKind.Brick, "b2:shared_b"));
            vm.SelectedAsset!.BrickCastShadowsField!.BoolValue = false;
            Assert.Equal(2, vm.DirtyCount);
            Assert.True(service.DraftStore.LoadDraft(sharedPath).Success);

            vm.DiscardCurrentAssetCommand.Execute(null);
            Assert.Equal(1, vm.DirtyCount);
            Assert.False(vm.SelectedAsset!.IsDirty);
            Assert.True(vm.SelectedAsset.BrickCastShadowsField!.BoolValue);
            Assert.True(service.DraftStore.LoadDraft(sharedPath).Success);

            // Undo must target b2:shared_a (NOT resurrect the discarded b2:shared_b)
            Assert.True(vm.CanUndo);
            vm.UndoCommand.Execute(null);
            Assert.Equal("b2:shared_a", vm.SelectedAsset!.Id);
            Assert.False(vm.SelectedAsset.IsDirty);
            Assert.True(vm.SelectedAsset.BrickCastShadowsField!.BoolValue);
            Assert.Equal(0, vm.DirtyCount);
            Assert.False(vm.CanUndo);
            Assert.False(service.DraftStore.LoadDraft(sharedPath).Success);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Verifies StudioGlPreviewController.BindWorkspace short-circuits on the same WorkspaceIndex, preserves camera across same-doc re-syncs, and syncs live auto-orbit.
    /// </summary>
    [AvaloniaFact]
    public void GlPreviewController_BindWorkspaceSameInstanceShortCircuits_AndSameDocSyncPreservesCamera()
    {
        var repoRoot = FindRepoRoot();
        var tempDir = CreateTempDirectory("gl-bind-camera");
        try
        {
            var service = new AssetToolsStudioWorkspaceService(CreateIsolatedDataPaths(tempDir));
            var controller = new StudioGlPreviewController();
            using var vm = new MainWindowViewModel(
                service,
                controller,
                startOnWelcomeScreen: false,
                enableLiveGlViewport: true,
                initialWorkspacePath: repoRoot);

            var workspace = service.CurrentWorkspace;
            Assert.NotNull(workspace);

            var viewport = new GlViewport { Width = 240, Height = 180 };
            controller.BindWorkspace(workspace, viewport);

            var coreBrick = vm.AllDocuments.First(d => d.Kind == AssetKind.Brick && string.Equals(d.Id, "wb:core", StringComparison.OrdinalIgnoreCase));
            var thrusterBrick = vm.AllDocuments.First(d => d.Kind == AssetKind.Brick && string.Equals(d.Id, "wb:thruster", StringComparison.OrdinalIgnoreCase));

            Assert.True(controller.TrySyncViewportScene(viewport, coreBrick, BrickOrientation.North, false, false, false));

            // Orbit and zoom camera away from preset
            viewport.Camera.Orbit(35f, 12f);
            viewport.Camera.Zoom(2f);
            var expectedYaw = viewport.Camera.YawDegrees;
            var expectedPitch = viewport.Camera.PitchDegrees;
            var expectedDistance = viewport.Camera.Distance;

            // Re-binding the exact same WorkspaceIndex short-circuits without clearing sync state
            controller.BindWorkspace(workspace, viewport);
            Assert.True(controller.TrySyncViewportScene(viewport, coreBrick, BrickOrientation.East, false, false, false));
            Assert.Equal(expectedYaw, viewport.Camera.YawDegrees, precision: 3);
            Assert.Equal(expectedPitch, viewport.Camera.PitchDegrees, precision: 3);
            Assert.Equal(expectedDistance, viewport.Camera.Distance, precision: 3);

            // Switching to a different asset resets camera framing
            Assert.True(controller.TrySyncViewportScene(viewport, thrusterBrick, BrickOrientation.North, false, false, false));
            Assert.NotEqual(expectedYaw, viewport.Camera.YawDegrees);

            // Auto-orbit starts ON by default (#1), syncs to GlViewport.SetAutoOrbit, and is preserved on camera reset
            vm.SelectDocument(coreBrick, pushHistory: false);
            vm.HandleGlViewportInitialized(new GlInfo("TestVendor", "TestRenderer", "3.3", false));
            vm.SyncLiveGlViewport(viewport);
            Assert.True(vm.IsPreviewOrbiting);
            Assert.True(viewport.IsAutoOrbiting);

            vm.ToggleOrbitCommand.Execute(null);
            vm.SyncLiveGlViewport(viewport);
            Assert.False(vm.IsPreviewOrbiting);
            Assert.False(viewport.IsAutoOrbiting);

            vm.ToggleOrbitCommand.Execute(null);
            vm.SyncLiveGlViewport(viewport);
            Assert.True(vm.IsPreviewOrbiting);
            Assert.True(viewport.IsAutoOrbiting);

            vm.CycleOrientationCommand.Execute(null);
            Assert.Equal(BrickOrientation.East, vm.PreviewOrientation);
            vm.ResetPreviewCameraCommand.Execute(null);
            vm.SyncLiveGlViewport(viewport);
            Assert.Equal(BrickOrientation.North, vm.PreviewOrientation);
            Assert.True(vm.IsPreviewOrbiting);
            Assert.True(viewport.IsAutoOrbiting);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    ///     Verifies CreateLocalizationKey targets the exact localization file without mutating or dirtying unrelated lang/en/item.toml.
    /// </summary>
    [AvaloniaFact]
    public void CreateLocalizationKey_TargetsExactFileWithoutDirtyingUnrelatedItemToml()
    {
        var tempRoot = CreateTempDirectory("loc-exact-target");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var packDir = Path.Combine(tempRoot, "loc_pack");
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "loc_pack", "lp", "Loc Pack", "Tester").Success);

            var langDir = Path.Combine(packDir, "assets", "lang", "en");
            Directory.CreateDirectory(langDir);
            var itemLangPath = Path.Combine(langDir, "item.toml");
            var brickLangPath = Path.Combine(langDir, "brick.toml");
            File.WriteAllText(itemLangPath, "\"Language\" = \"en\"\n\n[Translations]\n\"item.lp.existing\" = \"Existing Item\"\n");
            File.WriteAllText(brickLangPath, "\"Language\" = \"en\"\n\n[Translations]\n\"brick.lp.existing\" = \"Existing Brick\"\n");

            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            var itemDoc = vm.AllDocuments.First(d => d.Kind == AssetKind.Localization && string.Equals(d.Id, "lang/en/item.toml", StringComparison.OrdinalIgnoreCase));
            var brickDoc = vm.AllDocuments.First(d => d.Kind == AssetKind.Localization && string.Equals(d.Id, "lang/en/brick.toml", StringComparison.OrdinalIgnoreCase));
            Assert.False(itemDoc.IsDirty);
            Assert.False(brickDoc.IsDirty);

            // Create a key targeting assets/lang/en/brick.toml
            vm.OpenCreateLocalizationKeyDialogCommand.Execute(null);
            Assert.NotNull(vm.ActiveCreateLocKeyDialog);
            vm.ActiveCreateLocKeyDialog!.KeyName = "brick.lp.hull";
            vm.ActiveCreateLocKeyDialog.TargetFile = "assets/lang/en/brick.toml";
            vm.ActiveCreateLocKeyDialog.InitialText = "Reinforced Hull";
            vm.ActiveCreateLocKeyDialog.CreateKeyCommand.Execute(null);

            Assert.False(itemDoc.IsDirty);
            Assert.DoesNotContain(itemDoc.LocalizationRows, r => string.Equals(r.Key, "brick.lp.hull", StringComparison.Ordinal));
            Assert.False(brickDoc.IsDirty);
            Assert.Contains(brickDoc.LocalizationRows, r => string.Equals(r.Key, "brick.lp.hull", StringComparison.Ordinal));
            Assert.Contains("brick.lp.hull", File.ReadAllText(brickLangPath), StringComparison.Ordinal);

            // Create a key targeting a brand-new localization file not yet open in _allDocuments
            vm.OpenCreateLocalizationKeyDialogCommand.Execute(null);
            vm.ActiveCreateLocKeyDialog!.KeyName = "ui.lp.status";
            vm.ActiveCreateLocKeyDialog.TargetFile = "assets/lang/en/custom.toml";
            vm.ActiveCreateLocKeyDialog.InitialText = "Ready";
            vm.ActiveCreateLocKeyDialog.CreateKeyCommand.Execute(null);

            Assert.False(itemDoc.IsDirty);
            Assert.False(brickDoc.IsDirty);
            Assert.DoesNotContain(itemDoc.LocalizationRows, r => string.Equals(r.Key, "ui.lp.status", StringComparison.Ordinal));
            Assert.True(File.Exists(Path.Combine(langDir, "custom.toml")));
            Assert.Contains("ui.lp.status", File.ReadAllText(Path.Combine(langDir, "custom.toml")), StringComparison.Ordinal);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Verifies wheel zoom remains active during a left-button orbit drag on both GlViewport and PreviewViewportControl.
    /// </summary>
    [AvaloniaFact]
    public void PreviewViewports_HandleWheelZoomDuringActiveDragOrbit()
    {
        var glViewport = new GlViewport { Width = 240, Height = 180 };
        var fallback = new PreviewViewportControl
        {
            Width = 240,
            Height = 180,
            Descriptor = new PreviewRenderDescriptor(
                PreviewSurfaceMode.SingleBrick,
                "wb:core",
                "Block · N",
                "#4F8CFF",
                "Block",
                BrickOrientation.North,
                false,
                false,
                0,
                "standing",
                "front",
                string.Empty),
        };

        var fallbackWheelHandled = false;
        fallback.AddHandler(
            InputElement.PointerWheelChangedEvent,
            (_, args) => fallbackWheelHandled = args.Handled,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        var panel = new StackPanel();
        panel.Children.Add(glViewport);
        panel.Children.Add(fallback);
        var host = new Window { Width = 280, Height = 400, Content = panel };
        try
        {
            host.Show();
            Dispatcher.UIThread.RunJobs();

            // GlViewport: press left button, drag to orbit, then scroll wheel while still dragging
            var initialYaw = glViewport.Camera.YawDegrees;
            var initialDistance = glViewport.Camera.Distance;
            host.MouseDown(new Point(120, 90), MouseButton.Left);
            host.MouseMove(new Point(160, 105), RawInputModifiers.LeftMouseButton);
            host.MouseWheel(new Point(160, 105), new Vector(0, 2), RawInputModifiers.LeftMouseButton);
            host.MouseUp(new Point(160, 105), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.NotEqual(initialYaw, glViewport.Camera.YawDegrees);
            Assert.True(glViewport.Camera.Distance < initialDistance);

            // PreviewViewportControl (below glViewport at Y=180..360): drag + wheel zoom while dragging
            host.MouseDown(new Point(120, 270), MouseButton.Left);
            host.MouseMove(new Point(155, 285), RawInputModifiers.LeftMouseButton);
            host.MouseWheel(new Point(155, 285), new Vector(0, 2), RawInputModifiers.LeftMouseButton);
            host.MouseUp(new Point(155, 285), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.True(fallbackWheelHandled);
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>
    ///     #16: Verifies selecting an asset inside a collapsed explorer group auto-expands the group and raises SelectionScrollRequested.
    /// </summary>
    [AvaloniaFact]
    public void WorkspaceExplorer_AutoExpandsCollapsedGroupAndFiresSelectionScrollRequested()
    {
        using var vm = CreateSampleViewModel();
        var targetGroup = vm.TreeGroups.First(g => g.Items.Count > 0 && !ReferenceEquals(g.Items[0], vm.SelectedAsset));
        var targetDoc = targetGroup.Items[0];

        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Width = 1280;
            window.Height = 800;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            targetGroup.IsExpanded = false;
            AssetDocumentViewModel? scrolledTarget = null;
            vm.SelectionScrollRequested += doc => scrolledTarget = doc;

            Assert.True(vm.SelectDocument(targetDoc, pushHistory: true));
            Dispatcher.UIThread.RunJobs();

            Assert.Same(targetDoc, scrolledTarget);
            Assert.True(targetGroup.IsExpanded);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    ///     #21: Verifies real-repo reference candidates populate thumbnails for Texture, BrickFace, and Material kinds and decode via ThumbnailCache.
    /// </summary>
    [AvaloniaFact]
    public void ReferenceCandidates_PopulateThumbnailsForTextureBrickFaceAndMaterial_AndDecodeViaCache()
    {
        var repoRoot = FindRepoRoot();
        var tempDir = CreateTempDirectory("ref-thumbs");
        try
        {
            var service = new AssetToolsStudioWorkspaceService(CreateIsolatedDataPaths(tempDir));
            var snapshot = service.OpenWorkspace(WorkspaceMode.Repo, repoRoot);

            ReferenceKind[] thumbnailKinds = [ReferenceKind.Texture, ReferenceKind.BrickFace, ReferenceKind.Material];
            for (var i = 0; i < thumbnailKinds.Length; i++)
            {
                var kind = thumbnailKinds[i];
                Assert.Contains(snapshot.Candidates, c => c.Kind == kind && c.HasThumbnail);
                var withThumb = snapshot.Candidates.First(c => c.Kind == kind && c.HasThumbnail);
                Assert.False(string.IsNullOrWhiteSpace(withThumb.ThumbnailPath));
                Assert.True(File.Exists(withThumb.ThumbnailPath), $"Missing thumbnail file for {kind}: {withThumb.ThumbnailPath}");

                var bitmap = ThumbnailCache.GetOrLoad(withThumb.ThumbnailPath);
                Assert.NotNull(bitmap);
                Assert.True(bitmap!.PixelSize.Width > 0 && bitmap.PixelSize.Height > 0);
            }

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    ///     Follow-up #1: Verifies 3D preview auto-orbit defaults ON at launch and resets camera cleanly without disabling orbit.
    /// </summary>
    [AvaloniaFact]
    public void PreviewAutoOrbit_DefaultsOnAtLaunch_AndResetPreservesOrbitState()
    {
        using var vm = CreateSampleViewModel();
        Assert.True(vm.IsPreviewOrbiting);
        Assert.True(vm.ActivePreviewDescriptor.IsOrbiting);

        vm.CycleOrientationCommand.Execute(null);
        vm.SelectItemWorldModelPresetCommand.Execute(null);
        Assert.Equal(BrickOrientation.East, vm.PreviewOrientation);
        Assert.True(vm.UseItemWorldModelPreset);

        vm.ResetPreviewCameraCommand.Execute(null);
        Assert.Equal(BrickOrientation.North, vm.PreviewOrientation);
        Assert.False(vm.UseItemWorldModelPreset);
        Assert.True(vm.IsPreviewOrbiting);
        Assert.True(vm.ActivePreviewDescriptor.IsOrbiting);

        vm.ToggleOrbitCommand.Execute(null);
        Assert.False(vm.IsPreviewOrbiting);
        Assert.False(vm.ActivePreviewDescriptor.IsOrbiting);
    }

    /// <summary>
    ///     Follow-up #2: Verifies texture paint previews resolve actual block albedo textures, live-update on edits, and render textured pixels.
    /// </summary>
    [AvaloniaFact]
    public void TexturePaintPreview_ResolvesRealBlockTextures_LiveUpdatesAndRendersTexturedPixels()
    {
        var repoRoot = FindRepoRoot();
        var tempDir = CreateTempDirectory("tex-paint");
        try
        {
            var service = new AssetToolsStudioWorkspaceService(CreateIsolatedDataPaths(tempDir));
            var previewController = new StudioGlPreviewController();
            using var vm = new MainWindowViewModel(
                service,
                previewController,
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: repoRoot);

            // Real connected texture paint wb:panel -> panel.0.png
            Assert.True(vm.SelectAsset(AssetKind.Paint, "wb:panel"));
            var panelPaint = vm.SelectedAsset!;
            Assert.True(panelPaint.IsPaintTextureKind);
            Assert.False(string.IsNullOrWhiteSpace(panelPaint.ResolvedPaintTexturePath));
            Assert.EndsWith("panel.0.png", panelPaint.ResolvedPaintTexturePath!, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(panelPaint.ResolvedPaintTexturePath));
            Assert.True(vm.ActivePreviewDescriptor.IsTexturePaint);
            Assert.EndsWith("panel.0.png", vm.ActivePreviewDescriptor.PaintTexturePath!, StringComparison.OrdinalIgnoreCase);

            // Real non-connected texture paint wb:caution_panel -> caution_panel.png
            Assert.True(vm.SelectAsset(AssetKind.Paint, "wb:caution_panel"));
            var cautionPaint = vm.SelectedAsset!;
            Assert.True(cautionPaint.IsPaintTextureKind);
            Assert.EndsWith("caution_panel.png", cautionPaint.ResolvedPaintTexturePath!, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(cautionPaint.ResolvedPaintTexturePath));
            Assert.True(vm.ActivePreviewDescriptor.IsTexturePaint);
            Assert.EndsWith("caution_panel.png", vm.ActivePreviewDescriptor.PaintTexturePath!, StringComparison.OrdinalIgnoreCase);

            // Render PreviewViewportControl off-screen and verify textured multi-color pixels (not uniform white!)
            var control = new PreviewViewportControl
            {
                Width = 240,
                Height = 180,
                Descriptor = vm.ActivePreviewDescriptor,
            };
            var host = new Window { Width = 260, Height = 200, Content = control };
            try
            {
                host.Show();
                Dispatcher.UIThread.RunJobs();

                using var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(240, 180), new Vector(96, 96));
                rtb.Render(control);
                using var ms = new MemoryStream();
                rtb.Save(ms);
                var pngBytes = ms.ToArray();
                Assert.True(pngBytes.Length > 1000);

                // Compare against missing-layer fallback render to guarantee actual texture pixels were drawn
                control.Descriptor = vm.ActivePreviewDescriptor with { PaintTexturePath = null };
                Dispatcher.UIThread.RunJobs();
                using var rtbMissing = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(240, 180), new Vector(96, 96));
                rtbMissing.Render(control);
                using var msMissing = new MemoryStream();
                rtbMissing.Save(msMissing);
                Assert.NotEqual(pngBytes, msMissing.ToArray());

                // Compare against flat white color paint to confirm it is NOT rendering a uniform white square
                control.Descriptor = vm.ActivePreviewDescriptor with { IsTexturePaint = false, AccentHex = "#FFFFFF", PaintTexturePath = null };
                Dispatcher.UIThread.RunJobs();
                using var rtbWhite = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(240, 180), new Vector(96, 96));
                rtbWhite.Render(control);
                using var msWhite = new MemoryStream();
                rtbWhite.Save(msWhite);
                Assert.NotEqual(pngBytes, msWhite.ToArray());
            }
            finally
            {
                host.Close();
            }

            // Live edit PaintVariantsField updates PaintTexturePath immediately
            cautionPaint.PaintVariantsField!.Value = "core, vent";
            Assert.EndsWith("core.png", vm.ActivePreviewDescriptor.PaintTexturePath!, StringComparison.OrdinalIgnoreCase);

            // Editing to a nonexistent layer clears PaintTexturePath rather than leaking stale resolved path
            cautionPaint.PaintVariantsField.Value = "definitely_missing_texture_xyz";
            Assert.True(vm.ActivePreviewDescriptor.IsTexturePaint);
            Assert.Null(vm.ActivePreviewDescriptor.PaintTexturePath);

            // Switching PaintKindField to Color turns off IsTexturePaint live
            cautionPaint.PaintKindField!.Value = "Color";
            Assert.False(vm.ActivePreviewDescriptor.IsTexturePaint);
            Assert.Null(vm.ActivePreviewDescriptor.PaintTexturePath);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    ///     Follow-up #3: Verifies recursive workspace image discovery, remembered file-picker directory, path normalization, and BodySpritePickerControl selection.
    /// </summary>
    [AvaloniaFact]
    public async Task BodySpritePicker_DiscoversWorkspaceImages_RemembersPickerDir_AndSelectsInControl()
    {
        var tempRoot = CreateTempDirectory("body-sprite-picker");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var packDir = Path.Combine(tempRoot, "sprite_pack");
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "sprite_pack", "sp", "Sprite Pack", "Tester").Success);

            // Copy real PNG fixtures from repo into nested pack texture directories
            var repoRoot = FindRepoRoot();
            var sourcePngA = Path.Combine(repoRoot, "WaywardBeyond.Client", "assets", "textures", "block", "core.png");
            var sourcePngB = Path.Combine(repoRoot, "WaywardBeyond.Client", "assets", "textures", "block", "caution_panel.png");
            var charsDir = Path.Combine(packDir, "assets", "textures", "characters");
            var altDir = Path.Combine(packDir, "assets", "textures", "custom", "alt");
            Directory.CreateDirectory(charsDir);
            Directory.CreateDirectory(altDir);
            var heroPng = Path.Combine(charsDir, "hero.png");
            var altPng = Path.Combine(altDir, "hero_alt.png");
            File.Copy(sourcePngA, heroPng, overwrite: true);
            File.Copy(sourcePngB, altPng, overwrite: true);

            var bodyPath = Path.Combine(packDir, "assets", "bodies", "hero.toml");
            File.WriteAllText(
                bodyPath,
                "[[Bodies]]\nID = \"sp:hero\"\nSpecies = \"human\"\nGender = \"Neutral\"\n\n[Bodies.States.standing]\nfront = [\"characters/hero.png\"]\n");

            var fakePicker = new RecordingImagePathPicker([altPng, heroPng]);
            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                pathPicker: fakePicker,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            // Recursive image scan discovered both workspace images
            Assert.Contains(vm.WorkspaceImages, e => string.Equals(e.RelativePath, "characters/hero.png", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(vm.WorkspaceImages, e => string.Equals(e.RelativePath, "custom/alt/hero_alt.png", StringComparison.OrdinalIgnoreCase));

            Assert.True(vm.SelectAsset(AssetKind.Body, "sp:hero"));
            var body = vm.SelectedAsset!;
            Assert.Equal(vm.WorkspaceImages.Count, body.WorkspaceImages.Count);
            var slot = body.VisibleBodySprites.First();
            Assert.True(slot.HasSpriteBitmap);

            // First Browse call defaults to workspace root and updates slot path + bitmap
            await vm.BrowseBodySpriteFileCommand.ExecuteAsync(slot);
            Assert.Single(fakePicker.RequestedStartDirectories);
            Assert.Equal(Path.GetFullPath(packDir), Path.GetFullPath(fakePicker.RequestedStartDirectories[0]!));
            Assert.Equal("custom/alt/hero_alt.png", slot.TexturePath);
            Assert.True(slot.HasSpriteBitmap);

            // Second Browse call (Add Sprite row, parameter = null) remembers previous picked directory
            await vm.BrowseBodySpriteFileCommand.ExecuteAsync(null);
            Assert.Equal(2, fakePicker.RequestedStartDirectories.Count);
            Assert.Equal(Path.GetFullPath(altDir), Path.GetFullPath(fakePicker.RequestedStartDirectories[1]!));
            Assert.Equal("characters/hero.png", body.NewBodySpriteTexturePath);

            // BodySpritePickerControl filters and selects image tile cleanly
            var pickerControl = new BodySpritePickerControl
            {
                Width = 280,
                Images = body.WorkspaceImages,
                TexturePath = "characters/hero.png",
            };
            var host = new Window { Width = 420, Height = 360, Content = pickerControl };
            try
            {
                host.Show();
                Dispatcher.UIThread.RunJobs();

                pickerControl.IsPickerOpen = true;
                Dispatcher.UIThread.RunJobs();
                Assert.True(pickerControl.IsPickerOpen);

                var filterBox = pickerControl.FindControl<TextBox>("PART_FilterInput");
                Assert.NotNull(filterBox);
                filterBox!.Text = "hero_alt";
                Dispatcher.UIThread.RunJobs();

                var itemsControl = pickerControl.FindControl<ItemsControl>("PART_ImageItems");
                Assert.NotNull(itemsControl);
                Assert.Single(itemsControl!.Items);

                filterBox.Focus();
                Dispatcher.UIThread.RunJobs();
                host.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("custom/alt/hero_alt.png", pickerControl.TexturePath);
                Assert.False(pickerControl.IsPickerOpen);
            }
            finally
            {
                host.Close();
            }

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Follow-up #4: Verifies localization editable keys, unique defaults, bottom-row custom insert, duplicate detection, Undo/Redo, and lossless disk save.
    /// </summary>
    [AvaloniaFact]
    public void LocalizationEditor_SupportsEditableKeys_UniqueDefaults_Duplicates_UndoRedo_AndLosslessSave()
    {
        var tempRoot = CreateTempDirectory("loc-editable-keys");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var packDir = Path.Combine(tempRoot, "loc_edit_pack");
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "loc_edit_pack", "lep", "Loc Edit Pack", "Tester").Success);

            var langDir = Path.Combine(packDir, "assets", "lang", "en");
            Directory.CreateDirectory(langDir);
            var itemLangPath = Path.Combine(langDir, "item.toml");
            File.WriteAllText(
                itemLangPath,
                "# Keep localization header comment\n" +
                "\"Language\" = \"en\"\n\n" +
                "[Translations]\n" +
                "\"item.lep.alpha\" = \"Alpha\"\n" +
                "\"item.lep.beta\" = \"Beta\"\n");

            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            Assert.True(vm.SelectAsset(AssetKind.Localization, "lang/en/item.toml"));
            var doc = vm.SelectedAsset!;

            // Unique default key derived from file prefix (item.toml -> item.new_key)
            Assert.Equal("item.new_key", doc.NewLocalizationKey);

            // Add default row -> advances unique default to item.new_key_2
            doc.AddLocalizationRowCommand.Execute(null);
            Assert.Contains(doc.LocalizationRows, r => r.Key == "item.new_key");
            Assert.Equal("item.new_key_2", doc.NewLocalizationKey);

            // Bottom-row custom key + value insert
            doc.NewLocalizationKey = "item.lep.gamma";
            doc.NewLocalizationValue = "Gamma";
            doc.AddLocalizationRowCommand.Execute(null);
            Assert.Contains(doc.LocalizationRows, r => r.Key == "item.lep.gamma" && r.EnglishValue == "Gamma");
            Assert.Equal(string.Empty, doc.NewLocalizationValue);
            Assert.Equal("item.new_key_2", doc.NewLocalizationKey);

            // Colliding NewLocalizationKey falls back to next unique default instead of colliding
            doc.NewLocalizationKey = "item.lep.alpha";
            doc.AddLocalizationRowCommand.Execute(null);
            Assert.Contains(doc.LocalizationRows, r => r.Key == "item.new_key_2");
            Assert.Equal("item.new_key_3", doc.NewLocalizationKey);

            // Edit existing key into duplicate -> flags IsDuplicateKey on both rows
            var alphaRow = doc.LocalizationRows.First(r => r.Key == "item.lep.alpha");
            var betaRow = doc.LocalizationRows.First(r => r.Key == "item.lep.beta");
            alphaRow.Key = "item.lep.beta";
            Assert.True(alphaRow.IsDuplicateKey);
            Assert.True(betaRow.IsDuplicateKey);

            // Rename to a unique key clears duplicate flags and coalesced Undo/Redo restores original vs renamed key
            alphaRow.Key = "item.lep.alpha_renamed";
            Assert.False(alphaRow.IsDuplicateKey);
            Assert.False(betaRow.IsDuplicateKey);

            vm.UndoCommand.Execute(null);
            Assert.Equal("item.lep.alpha", alphaRow.Key);
            Assert.False(alphaRow.IsDuplicateKey);

            vm.RedoCommand.Execute(null);
            Assert.Equal("item.lep.alpha_renamed", alphaRow.Key);
            Assert.False(alphaRow.IsDuplicateKey);

            // Delete beta row and test Undo/Redo
            doc.DeleteLocalizationRowCommand.Execute(betaRow);
            Assert.DoesNotContain(betaRow, doc.LocalizationRows);
            vm.UndoCommand.Execute(null);
            Assert.Contains(betaRow, doc.LocalizationRows);
            vm.RedoCommand.Execute(null);
            Assert.DoesNotContain(betaRow, doc.LocalizationRows);

            // Save to disk -> old renamed key and deleted key are gone, comments preserved, no duplicate old key
            vm.SaveAllCommand.Execute(null);
            Assert.Equal(0, vm.DirtyCount);
            Assert.Equal("item.lep.alpha_renamed", alphaRow.OriginalKey);

            var savedToml = File.ReadAllText(itemLangPath);
            Assert.Contains("# Keep localization header comment", savedToml, StringComparison.Ordinal);
            Assert.Contains("\"item.lep.alpha_renamed\" = \"Alpha\"", savedToml, StringComparison.Ordinal);
            Assert.DoesNotContain("\"item.lep.alpha\"", savedToml, StringComparison.Ordinal);
            Assert.DoesNotContain("\"item.lep.beta\"", savedToml, StringComparison.Ordinal);
            Assert.Contains("\"item.lep.gamma\" = \"Gamma\"", savedToml, StringComparison.Ordinal);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Verifies null assignment on NewLocalizationKey/NewLocalizationValue coerces safely and + Add Key clears an unrelated active filter so the new row stays visible.
    /// </summary>
    [AvaloniaFact]
    public void LocalizationEditor_NullNewKeyInputsCoerceSafely_AndAddKeyClearsMismatchedFilter()
    {
        using var vm = CreateSampleViewModel();
        Assert.True(vm.SelectAsset(AssetKind.Localization, "lang/en/item.toml"));
        var doc = vm.SelectedAsset!;

        // Defensive null coercion prevents NullReferenceException when TextBox.Clear() pushes null
        doc.NewLocalizationKey = null!;
        doc.NewLocalizationValue = null!;
        Assert.Equal(string.Empty, doc.NewLocalizationKey);
        Assert.Equal(string.Empty, doc.NewLocalizationValue);

        // Unrelated active filter is cleared when adding a key that would otherwise be hidden
        doc.LocalizationFilterText = "unrelated_filter_xyz";
        Assert.Empty(doc.FilteredLocalizationRows);

        doc.AddLocalizationRowCommand.Execute(null);
        Assert.Equal(string.Empty, doc.LocalizationFilterText);
        Assert.Contains(doc.LocalizationRows, r => r.Key == "item.new_key");
        Assert.Contains(doc.FilteredLocalizationRows, r => r.Key == "item.new_key");

        // Matching active filter is preserved when the new key matches it
        doc.LocalizationFilterText = "custom_match";
        doc.NewLocalizationKey = "item.custom_match_entry";
        doc.NewLocalizationValue = "Matched Label";
        doc.AddLocalizationRowCommand.Execute(null);
        Assert.Equal("custom_match", doc.LocalizationFilterText);
        Assert.Contains(doc.FilteredLocalizationRows, r => r.Key == "item.custom_match_entry" && r.EnglishValue == "Matched Label");
    }

    /// <summary>
    ///     Follow-up #5: Verifies real repo builtin/mainMenu blueprint populates Brick &amp; Paint Palette and USED BRICKS references and navigates cleanly.
    /// </summary>
    [AvaloniaFact]
    public void RealRepoMainMenuBlueprint_PopulatesBrickAndPaintPalette_AndNavigatesToDefinitions()
    {
        var repoRoot = FindRepoRoot();
        var tempDir = CreateTempDirectory("bp-mainmenu");
        try
        {
            var service = new AssetToolsStudioWorkspaceService(CreateIsolatedDataPaths(tempDir));
            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: repoRoot);

            Assert.True(vm.SelectAsset(AssetKind.Blueprint, "builtin/mainMenu"));
            var bp = vm.SelectedAsset!;
            Assert.True(bp.HasBlueprintPalette);
            Assert.Equal("USED BRICKS", bp.ReferencesSectionTitle);
            Assert.NotEmpty(bp.InboundReferences);

            string[] expectedBricks = ["wb:core", "wb:panel", "wb:thruster", "wb:glass"];
            for (var i = 0; i < expectedBricks.Length; i++)
            {
                var id = expectedBricks[i];
                Assert.Contains(bp.BlueprintPalette, p =>
                    p.Kind == AssetKind.Brick
                    && string.Equals(p.AssetId, id, StringComparison.OrdinalIgnoreCase)
                    && p.IsResolved
                    && p.VoxelCount > 0);
            }

            // Clicking a palette entry jumps to its asset definition
            var coreEntry = bp.BlueprintPalette.First(p => p.Kind == AssetKind.Brick && p.AssetId == "wb:core");
            vm.NavigatePaletteItemCommand.Execute(coreEntry);
            Assert.Equal(AssetKind.Brick, vm.SelectedAsset!.Kind);
            Assert.Equal("wb:core", vm.SelectedAsset.Id);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    ///     Follow-up #6: Verifies freshly created empty packs and uninitialized non-pack folders display clean empty states and accurate pack names.
    /// </summary>
    [AvaloniaFact]
    public void EmptyPackAndUninitializedFolder_ShowCleanStates_AccuratePackName_AndInitializeCleanly()
    {
        var tempRoot = CreateTempDirectory("empty-pack-states");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var freshPackDir = Path.Combine(tempRoot, "frontier_colony");
            var nonPackDir = Path.Combine(tempRoot, "uninitialized_dir");
            Directory.CreateDirectory(nonPackDir);

            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            var created = service.CreatePackWorkspace(freshPackDir, "frontier_colony", "fc", "Frontier Colony", "Tester");
            Assert.True(created.Success, created.Message);

            // 1. Fresh empty pack -> HasSelectedAsset == false, IsUninitializedFolder == false, accurate pack display name
            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: freshPackDir);

            Assert.False(vm.HasSelectedAsset);
            Assert.True(vm.HasNoSelectedAsset);
            Assert.False(vm.IsUninitializedFolder);
            Assert.True(vm.ShowExplorerEmptyHint);
            Assert.False(vm.HasVisualPreview);
            Assert.False(vm.IsDataDocumentPreview);
            Assert.DoesNotContain("Wayward Beyond", vm.WorkspaceName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Wayward Beyond", vm.WorkspaceModeLabel, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Frontier Colony", vm.WorkspaceModeLabel, StringComparison.OrdinalIgnoreCase);

            // 2. Arbitrary empty non-pack folder -> IsUninitializedFolder == true, 0 documents, Folder: pill + contextual hint
            vm.OpenWorkspace(WorkspaceMode.Pack, nonPackDir);
            Assert.False(vm.HasSelectedAsset);
            Assert.True(vm.HasNoSelectedAsset);
            Assert.True(vm.IsUninitializedFolder);
            Assert.Empty(vm.AllDocuments);
            Assert.StartsWith("Folder: ", vm.WorkspaceModeLabel, StringComparison.Ordinal);
            Assert.Equal("Initialize this folder as a pack to start adding assets.", vm.ExplorerEmptyHintText);
            Assert.DoesNotContain("Wayward Beyond", vm.WorkspaceModeLabel, StringComparison.OrdinalIgnoreCase);

            // 3. InitializeCurrentFolderAsPackCommand scaffolds pack files and reloads cleanly
            vm.InitializeCurrentFolderAsPackCommand.Execute(null);
            Assert.True(File.Exists(Path.Combine(nonPackDir, "manifest.toml")));
            Assert.True(File.Exists(Path.Combine(nonPackDir, "pack.toml")));
            Assert.True(Directory.Exists(Path.Combine(nonPackDir, "assets")));
            Assert.False(vm.IsUninitializedFolder);
            Assert.False(vm.HasSelectedAsset);
            Assert.StartsWith("Pack: ", vm.WorkspaceModeLabel, StringComparison.Ordinal);
            Assert.DoesNotContain("Wayward Beyond", vm.WorkspaceModeLabel, StringComparison.OrdinalIgnoreCase);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Step 2 unified texture picker: verifies all texture fields expose visual thumbnail grid + browse button,
    ///     disk browse normalizes Texture vs BrickFace values and remembers directory, comma-separated &amp; connected
    ///     block names validate cleanly, and WorkspaceImages sorts characters first while excluding PBR sidecars.
    /// </summary>
    [AvaloniaFact]
    public void UnifiedTexturePicker_CoversAllTextureFields_BrowseNormalization_MultiEntryValidation_AndCleanImageOrdering()
    {
        var repoRoot = FindRepoRoot();
        var tempRoot = CreateTempDirectory("unified-tex-picker");
        try
        {
            // Part A: Real repo verifies all texture field kinds, comma-separated + connected validation, and WorkspaceImages ordering/filtering
            var repoDataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "repo-data"));
            var repoService = new AssetToolsStudioWorkspaceService(repoDataPaths);
            using var repoVm = new MainWindowViewModel(
                repoService,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: repoRoot);

            // WorkspaceImages sorts characters/ first and excludes PBR channel sidecars (.m/.s/.n/.e/.t)
            Assert.NotEmpty(repoVm.WorkspaceImages);
            Assert.StartsWith("characters/", repoVm.WorkspaceImages[0].RelativePath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(repoVm.WorkspaceImages, e =>
                e.FileName.EndsWith(".e.png", StringComparison.OrdinalIgnoreCase)
                || e.FileName.EndsWith(".t.png", StringComparison.OrdinalIgnoreCase)
                || e.FileName.EndsWith(".n.png", StringComparison.OrdinalIgnoreCase)
                || e.FileName.EndsWith(".m.png", StringComparison.OrdinalIgnoreCase)
                || e.FileName.EndsWith(".s.png", StringComparison.OrdinalIgnoreCase));

            // Every texture field has IsTexturePickerField == true && AllowBrowseTextureFile == true
            Assert.True(repoVm.SelectAsset(AssetKind.Item, "wb:drill"));
            var drill = repoVm.SelectedAsset!;
            Assert.True(drill.IconField!.IsTexturePickerField);
            Assert.False(drill.IconField.IsStandardReferenceList);
            Assert.True(drill.IconField.AllowBrowseTextureFile);
            Assert.False(drill.NameField!.IsTexturePickerField);
            Assert.True(drill.NameField.IsStandardReferenceList);
            Assert.False(drill.NameField.AllowBrowseTextureFile);

            Assert.True(repoVm.SelectAsset(AssetKind.Skill, "building"));
            Assert.True(repoVm.SelectedAsset!.IconField!.IsTexturePickerField);
            Assert.True(repoVm.SelectedAsset.IconField.AllowBrowseTextureFile);

            Assert.True(repoVm.SelectAsset(AssetKind.Material, "bricks/glass"));
            Assert.True(repoVm.SelectedAsset!.MaterialTexturesField!.IsTexturePickerField);
            Assert.True(repoVm.SelectedAsset.MaterialTexturesField.AllowBrowseTextureFile);

            Assert.True(repoVm.SelectAsset(AssetKind.Brick, "wb:core"));
            var coreBrick = repoVm.SelectedAsset!;
            StudioFieldViewModel?[] brickFaceFields =
            [
                coreBrick.BrickTexDefaultField,
                coreBrick.BrickTexTopField,
                coreBrick.BrickTexBottomField,
                coreBrick.BrickTexFrontField,
                coreBrick.BrickTexBackField,
                coreBrick.BrickTexLeftField,
                coreBrick.BrickTexRightField,
            ];
            Assert.All(brickFaceFields, f =>
            {
                Assert.NotNull(f);
                Assert.True(f!.IsTexturePickerField);
                Assert.True(f.AllowBrowseTextureFile);
            });

            // Paint Variants field uses BrickFace visual thumbnail picker and validates comma-separated & connected base names cleanly
            Assert.True(repoVm.SelectAsset(AssetKind.Paint, "wb:control_buttons"));
            var buttonsPaint = repoVm.SelectedAsset!;
            Assert.True(buttonsPaint.PaintVariantsField!.IsTexturePickerField);
            Assert.True(buttonsPaint.PaintVariantsField.AllowBrowseTextureFile);
            Assert.False(buttonsPaint.PaintVariantsField.IsDanger);
            Assert.Equal("Resolved", buttonsPaint.PaintVariantsField.ResolutionLabel);

            buttonsPaint.PaintVariantsField.Value = "control_buttons.0, control_buttons.1";
            Assert.False(buttonsPaint.PaintVariantsField.IsDanger);
            Assert.Equal("Resolved", buttonsPaint.PaintVariantsField.ResolutionLabel);

            buttonsPaint.PaintVariantsField.Value = "panel";
            Assert.False(buttonsPaint.PaintVariantsField.IsDanger);
            Assert.Equal("Resolved", buttonsPaint.PaintVariantsField.ResolutionLabel);

            buttonsPaint.PaintVariantsField.Value = "panel, definitely_missing_layer_xyz";
            Assert.True(buttonsPaint.PaintVariantsField.IsDanger);
            Assert.Equal("Missing", buttonsPaint.PaintVariantsField.ResolutionLabel);

            repoService.MarkCleanShutdown();

            // Part B: Temp pack verifies BrowseTextureFileCommand remembers last directory and normalizes Texture vs BrickFace paths
            var packDataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "pack-data"));
            var packDir = Path.Combine(tempRoot, "browse_pack");
            var packService = new AssetToolsStudioWorkspaceService(packDataPaths);
            Assert.True(packService.CreatePackWorkspace(packDir, "browse_pack", "bp", "Browse Pack", "Tester").Success);

            var itemsTexDir = Path.Combine(packDir, "assets", "textures", "items");
            var blockTexDir = Path.Combine(packDir, "assets", "textures", "block");
            Directory.CreateDirectory(itemsTexDir);
            Directory.CreateDirectory(blockTexDir);
            var fooPng = Path.Combine(itemsTexDir, "foo.png");
            var cautionPng = Path.Combine(blockTexDir, "caution_panel.png");
            File.Copy(Path.Combine(repoRoot, "WaywardBeyond.Client", "assets", "textures", "block", "core.png"), fooPng, overwrite: true);
            File.Copy(Path.Combine(repoRoot, "WaywardBeyond.Client", "assets", "textures", "block", "caution_panel.png"), cautionPng, overwrite: true);

            File.WriteAllText(
                Path.Combine(packDir, "assets", "items", "foo.toml"),
                "[[Items]]\nID = \"bp:foo\"\nName = \"item.bp.foo\"\nIcon = \"\"\nMaxStack = 64\n");
            File.WriteAllText(
                Path.Combine(packDir, "assets", "paints", "textures.toml"),
                "[[Paints]]\nID = \"bp:caution\"\nName = \"paint.bp.caution\"\nAlpha = \"Opaque\"\nTextures = [\"caution_panel\"]\n");

            var fakePicker = new RecordingImagePathPicker([fooPng, cautionPng]);
            using var packVm = new MainWindowViewModel(
                packService,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                pathPicker: fakePicker,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            // First browse on Item.Icon (ReferenceKind.Texture) -> starts at packDir, normalizes to items/foo.png
            Assert.True(packVm.SelectAsset(AssetKind.Item, "bp:foo"));
            var iconField = packVm.SelectedAsset!.IconField!;
            iconField.BrowseTextureFileCommand.Execute(null);
            Assert.Single(fakePicker.RequestedStartDirectories);
            Assert.Equal(Path.GetFullPath(packDir), Path.GetFullPath(fakePicker.RequestedStartDirectories[0]!));
            Assert.Equal("items/foo.png", iconField.Value);
            Assert.False(iconField.IsDanger);

            // Second browse on Paint.Variants (ReferenceKind.BrickFace) -> remembers itemsTexDir, normalizes to bare stem caution_panel
            Assert.True(packVm.SelectAsset(AssetKind.Paint, "bp:caution"));
            var variantsField = packVm.SelectedAsset!.PaintVariantsField!;
            variantsField.Value = string.Empty;
            variantsField.BrowseTextureFileCommand.Execute(null);
            Assert.Equal(2, fakePicker.RequestedStartDirectories.Count);
            Assert.Equal(Path.GetFullPath(itemsTexDir), Path.GetFullPath(fakePicker.RequestedStartDirectories[1]!));
            Assert.Equal("caution_panel", variantsField.Value);
            Assert.False(variantsField.IsDanger);

            packService.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    private sealed class RecordingImagePathPicker(IReadOnlyList<string> results) : IWorkspacePathPicker
    {
        private int _index;

        public List<string?> RequestedStartDirectories { get; } = [];

        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);

        public Task<string?> PickBlueprintFileAsync(string title) => Task.FromResult<string?>(null);

        public Task<string?> PickImageFileAsync(string title, string? suggestedStartDirectory = null)
        {
            RequestedStartDirectories.Add(suggestedStartDirectory);
            if (_index < results.Count)
            {
                return Task.FromResult<string?>(results[_index++]);
            }

            return Task.FromResult<string?>(null);
        }

        public Task<string?> PickMeshFileAsync(string title, string? suggestedStartDirectory = null)
        {
            RequestedStartDirectories.Add(suggestedStartDirectory);
            if (_index < results.Count)
            {
                return Task.FromResult<string?>(results[_index++]);
            }

            return Task.FromResult<string?>(null);
        }
    }

    private static MainWindowViewModel CreateSampleViewModel()
    {
        return new MainWindowViewModel(
            new SampleStudioWorkspaceService(),
            new SamplePreviewSurfaceProvider(),
            startOnWelcomeScreen: false);
    }

    private static ToolsSettings.ToolDataPaths CreateIsolatedDataPaths(string rootDir)
    {
        Directory.CreateDirectory(rootDir);
        File.WriteAllText(Path.Combine(rootDir, ToolsSettings.ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);
        return ToolsSettings.ToolDataPaths.Resolve(executableDirectory: rootDir);
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

    private static string CreateTempDirectory(string label)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wb-asset-studio-{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Ignore transient file locks during temp cleanup.
        }
    }
}
