using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.AssetStudio.ViewModels;
using WaywardBeyond.AssetStudio.Views;
using Xunit;

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>
///     Headless interaction and behavioral tests for Wayward Beyond Asset Studio view models and shell.
/// </summary>
public sealed class StudioInteractionTests
{
    /// <summary>
    ///     Verifies back and forward navigation history tracks visited assets accurately.
    /// </summary>
    [AvaloniaFact]
    public void NavigationHistoryMovesBackAndForward()
    {
        var vm = CreateViewModel();
        Assert.Equal("wb:thruster", vm.SelectedAsset?.Id);
        Assert.False(vm.CanNavigateBack);

        Assert.True(vm.SelectAssetById("wb:thruster_item"));
        Assert.True(vm.SelectAssetById("building"));
        Assert.True(vm.CanNavigateBack);
        Assert.False(vm.CanNavigateForward);

        vm.NavigateBackCommand.Execute(null);
        Assert.Equal("wb:thruster_item", vm.SelectedAsset?.Id);
        Assert.True(vm.CanNavigateForward);

        vm.NavigateBackCommand.Execute(null);
        Assert.Equal("wb:thruster", vm.SelectedAsset?.Id);
        Assert.False(vm.CanNavigateBack);

        vm.NavigateForwardCommand.Execute(null);
        Assert.Equal("wb:thruster_item", vm.SelectedAsset?.Id);
    }

    /// <summary>
    ///     Verifies toggling raw TOML token mode preserves invalid values without clearing them.
    /// </summary>
    [AvaloniaFact]
    public void RawTokenTogglePreservesInvalidFieldValues()
    {
        var vm = CreateViewModel();
        var brick = vm.SelectedAsset;
        Assert.NotNull(brick);

        var field = brick.BrickTexDefaultField;
        Assert.NotNull(field);
        Assert.Equal("thruster_nozzle", field.Value);
        Assert.True(field.IsDanger);

        field.ToggleRawModeCommand.Execute(null);
        Assert.True(field.IsRawMode);
        Assert.Equal("\"thruster_nozzle\"", field.RawTokenText);

        field.ToggleRawModeCommand.Execute(null);
        Assert.False(field.IsRawMode);
        Assert.Equal("thruster_nozzle", field.Value);
        Assert.True(field.IsDanger);
    }

    /// <summary>
    ///     Verifies tag reordering triggers the one-time semantic order warning and updates effective order live.
    /// </summary>
    [AvaloniaFact]
    public void TagEditorReorderingWarnsOnceAndUpdatesEffectiveOrder()
    {
        var vm = CreateViewModel();
        Assert.True(vm.SelectAssetById("brick.paintable"));
        var tagDoc = vm.SelectedAsset;
        Assert.NotNull(tagDoc);
        Assert.False(tagDoc.ShowTagOrderWarning);

        var initialCount = tagDoc.TagEffectiveEntries.Count;
        tagDoc.NewTagValue = "colony:airlock";
        tagDoc.AppendTagCommand.Execute(null);
        Assert.Equal(initialCount + 1, tagDoc.TagEffectiveEntries.Count);

        // Append duplicate and confirm duplicate flagging without auto-merge
        tagDoc.NewTagValue = "colony:airlock";
        tagDoc.AppendTagCommand.Execute(null);
        Assert.True(tagDoc.HasDuplicateTags);

        // Reorder entry -> warns once
        var secondEntry = tagDoc.TagFileEntries[1];
        tagDoc.MoveTagUpCommand.Execute(secondEntry);
        Assert.True(tagDoc.ShowTagOrderWarning);

        tagDoc.DismissTagOrderWarningCommand.Execute(null);
        Assert.False(tagDoc.ShowTagOrderWarning);

        // Second reorder does not re-trigger the one-time warning
        tagDoc.MoveTagDownCommand.Execute(tagDoc.TagFileEntries[0]);
        Assert.False(tagDoc.ShowTagOrderWarning);
    }

    /// <summary>
    ///     Verifies clicking a validation finding jumps to the asset and highlights the offending field, and Fix Case resolves casing issues.
    /// </summary>
    [AvaloniaFact]
    public void ValidationFocusHighlightsFieldAndFixCaseResolvesMismatch()
    {
        var vm = CreateViewModel();
        Assert.True(vm.CrossPlatformCount >= 1);

        var caseFinding = vm.AllFindings.First(f => f.Severity == ValidationSeverity.CrossPlatform);
        vm.FocusFindingCommand.Execute(caseFinding);

        Assert.Equal("wb:thruster_item", vm.SelectedAsset?.Id);
        Assert.NotNull(vm.SelectedAsset?.IconField);
        Assert.True(vm.SelectedAsset.IconField.IsHighlighted);
        Assert.True(vm.SelectedAsset.IconField.IsDanger);

        vm.FixCaseIssuesCommand.Execute(null);
        Assert.Equal(0, vm.CrossPlatformCount);
        Assert.Equal("items/thruster.png", vm.SelectedAsset.IconField.Value);
        Assert.False(vm.SelectedAsset.IconField.IsDanger);
    }

    /// <summary>
    ///     Verifies command palette ranks queries, applies scope/kind filters, and opens winning or shadowed files.
    /// </summary>
    [AvaloniaFact]
    public void CommandPaletteFiltersAndOpensWinningOrShadowed()
    {
        var vm = CreateViewModel();
        vm.OpenCommandPaletteCommand.Execute(null);
        var palette = vm.ActiveCommandPalette;
        Assert.NotNull(palette);

        palette.Query = "thruster";
        Assert.NotEmpty(palette.Results);
        Assert.Equal("wb:thruster", palette.Results[0].Title);

        palette.SetScopeCommand.Execute("Actions");
        Assert.All(palette.Results, r => Assert.Equal("Actions", r.Category));

        palette.SetScopeCommand.Execute("All");
        palette.SetKindFilterCommand.Execute("Skill");
        palette.Query = string.Empty;
        Assert.Single(palette.Results);
        Assert.Equal("building", palette.Results[0].Title);

        palette.ExecuteWinningCommand.Execute(palette.Results[0]);
        Assert.Null(vm.ActiveCommandPalette);
        Assert.Equal("building", vm.SelectedAsset?.Id);
    }

    /// <summary>
    ///     Verifies System, Light, and Dark theme switching updates the Avalonia application theme variant cleanly.
    /// </summary>
    [AvaloniaFact]
    public void ThemeSwitchingUpdatesApplicationRequestedThemeVariant()
    {
        var vm = CreateViewModel();
        vm.SelectSystemThemeCommand.Execute(null);
        Assert.True(vm.IsSystemTheme);
        Assert.Equal(ThemeVariant.Default, Application.Current?.RequestedThemeVariant);

        vm.ToggleThemeCommand.Execute(null);
        Assert.True(vm.IsLightTheme);
        Assert.Equal(ThemeVariant.Light, Application.Current?.RequestedThemeVariant);

        vm.ToggleThemeCommand.Execute(null);
        Assert.True(vm.IsDarkTheme);
        Assert.Equal(ThemeVariant.Dark, Application.Current?.RequestedThemeVariant);

        vm.ToggleThemeCommand.Execute(null);
        Assert.True(vm.IsSystemTheme);
        Assert.Equal(ThemeVariant.Default, Application.Current?.RequestedThemeVariant);
    }

    /// <summary>
    ///     Verifies dialogs for new asset, brick rename warning, export blocking, and blueprint two-step delete.
    /// </summary>
    [AvaloniaFact]
    public void DialogWorkflowsEnforceSpecContracts()
    {
        var vm = CreateViewModel();

        // Rename brick shows legacy hash warning
        vm.OpenRenameDialogCommand.Execute(null);
        Assert.NotNull(vm.ActiveRenameDialog);
        Assert.True(vm.ActiveRenameDialog.ShowBrickLegacyHashWarning);
        Assert.NotEmpty(vm.ActiveRenameDialog.AffectedSpans);
        vm.CloseAllOverlaysCommand.Execute(null);

        // Export blocks when Fatal/Broken findings exist
        vm.OpenExportDialogCommand.Execute(null);
        Assert.NotNull(vm.ActiveExportDialog);
        Assert.True(vm.ActiveExportDialog.IsBlocked);
        Assert.False(vm.ActiveExportDialog.CanExport);
        vm.CloseAllOverlaysCommand.Execute(null);

        // New Asset appends to existing multi-entry file
        vm.OpenNewAssetDialogCommand.Execute(null);
        Assert.NotNull(vm.ActiveNewAssetDialog);
        vm.ActiveNewAssetDialog.SelectedKind = AssetKind.Paint;
        vm.ActiveNewAssetDialog.AppendToExistingFile = true;
        vm.ActiveNewAssetDialog.AssetId = "colony:cobalt_accent";
        vm.ActiveNewAssetDialog.CreateCommand.Execute(null);
        Assert.Equal("colony:cobalt_accent", vm.SelectedAsset?.Id);

        // Blueprint 2-step delete confirmation and Used Bricks heading
        Assert.True(vm.SelectAssetById("ships/scout_mk1.wbp"));
        var bp = vm.SelectedAsset!;
        Assert.Equal("USED BRICKS", bp.ReferencesSectionTitle);
        Assert.False(bp.HasNamespaceBadge);
        Assert.False(bp.IsBlueprintDeleteConfirming);
        bp.BeginBlueprintDeleteCommand.Execute(null);
        Assert.True(bp.IsBlueprintDeleteConfirming);
        bp.CancelBlueprintDeleteCommand.Execute(null);
        Assert.False(bp.IsBlueprintDeleteConfirming);

        // Namespaced brick uses ns: prefix badge & clean enum hints
        Assert.True(vm.SelectAssetById("wb:thruster"));
        var thruster = vm.SelectedAsset!;
        Assert.True(thruster.HasNamespaceBadge);
        Assert.Equal("ns:wb", thruster.NamespaceBadge);
        Assert.Equal("INBOUND REFERENCES", thruster.ReferencesSectionTitle);
        Assert.False(thruster.BrickShapeField!.HasGrammarHint);
        Assert.True(thruster.BrickColorField!.HasGrammarHint);

        // Item WorldModel omits duplicate unread pill on child fields
        Assert.True(vm.SelectAssetById("wb:thruster_item"));
        var item = vm.SelectedAsset!;
        Assert.True(item.ItemWorldModelMeshField!.IsInformational);
        Assert.False(item.ItemWorldModelMeshField.ShowUnreadBadge);
    }

    /// <summary>
    ///     Verifies brick and item statistics authoring, editing, removing, undo/redo, and lossless TOML persistence.
    /// </summary>
    [AvaloniaFact]
    public void BrickAndItemStatisticsEditUndoAndSaveLosslessly()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"wb-studio-stats-{Path.GetRandomFileName()}");
        try
        {
            var dataDir = Path.Combine(tempRoot, "tool-data");
            var packDir = Path.Combine(tempRoot, "stats_pack");
            var assetsDir = Path.Combine(packDir, "assets");
            Directory.CreateDirectory(Path.Combine(assetsDir, "bricks"));
            Directory.CreateDirectory(Path.Combine(assetsDir, "items"));
            Directory.CreateDirectory(Path.Combine(assetsDir, "lang", "en"));
            Directory.CreateDirectory(Path.Combine(assetsDir, "textures", "block"));

            File.WriteAllText(Path.Combine(packDir, "manifest.toml"), "ID = \"test.stats\"\nName = \"Stats Pack\"\n");
            File.WriteAllText(Path.Combine(packDir, "pack.toml"), "ID = \"test.stats\"\nNamespace = \"test\"\nContentVersion = 1\nToolVersion = \"0.1.0\"\n");
            File.WriteAllText(
                Path.Combine(assetsDir, "lang", "en", "items.toml"),
                "\"Language\" = \"en\"\n[Translations]\n\"item.laser.name\" = \"Mining Laser\"\n");
            File.WriteAllBytes(Path.Combine(assetsDir, "textures", "block", "stone.png"), [1, 2, 3, 4]);

            var brickPath = Path.Combine(assetsDir, "bricks", "stone.toml");
            File.WriteAllText(
                brickPath,
                "# preserve comment\n[[Bricks]]\nID = \"test:stone\"\nShape = \"block\"\n[Bricks.Textures]\nDefault = [\"stone\"]\n[Bricks.Statistics]\n\"wb:hardness\" = 2.5\n\"test:blast\" = 8.0\n");

            var itemPath = Path.Combine(assetsDir, "items", "laser.toml");
            File.WriteAllText(
                itemPath,
                "# preserve item comment\n[[Items]]\nID = \"test:laser\"\nName = \"item.laser.name\"\n[Items.Tool]\nType = \"mining\"\n[Items.Statistics]\n\"wb:mining_speed\" = 1.75\n\"test:heat\" = 0.5\n");

            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, WaywardBeyond.AssetTools.Settings.ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);
            var dataPaths = WaywardBeyond.AssetTools.Settings.ToolDataPaths.Resolve(executableDirectory: dataDir);
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            using var vm = new MainWindowViewModel(
                service,
                new SamplePreviewSurfaceProvider(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            //  Brick statistics load, edit, add custom stat, remove stat, undo/redo, and save losslessly.
            Assert.True(vm.SelectAssetById("test:stone"));
            var brickDoc = vm.SelectedAsset!;
            Assert.Equal(2, brickDoc.Statistics.Count);
            Assert.Contains(brickDoc.Statistics, static s => s.Key == "wb:hardness" && s.Value == "2.5");
            Assert.Contains(brickDoc.Statistics, static s => s.Key == "test:blast" && s.Value == "8");

            var hardnessRow = brickDoc.Statistics.First(static s => s.Key == "wb:hardness");
            hardnessRow.Value = "4.25";
            brickDoc.AddStatisticCommand.Execute(null);
            Assert.Equal(3, brickDoc.Statistics.Count);
            brickDoc.Statistics[^1].Key = "test:conductivity";
            brickDoc.Statistics[^1].Value = "0.85";

            var blastRow = brickDoc.Statistics.First(static s => s.Key == "test:blast");
            brickDoc.RemoveStatisticCommand.Execute(blastRow);
            Assert.Equal(2, brickDoc.Statistics.Count);

            vm.UndoCommand.Execute(null);
            Assert.Equal(3, brickDoc.Statistics.Count);
            vm.RedoCommand.Execute(null);
            Assert.Equal(2, brickDoc.Statistics.Count);

            vm.SaveAllCommand.Execute(null);
            var savedBrickToml = File.ReadAllText(brickPath);
            Assert.Contains("# preserve comment", savedBrickToml, StringComparison.Ordinal);
            Assert.Contains("[Bricks.Statistics]", savedBrickToml, StringComparison.Ordinal);
            Assert.Contains("\"wb:hardness\" = 4.25", savedBrickToml, StringComparison.Ordinal);
            Assert.Contains("\"test:conductivity\" = 0.85", savedBrickToml, StringComparison.Ordinal);
            Assert.DoesNotContain("test:blast", savedBrickToml, StringComparison.Ordinal);

            //  Item statistics load, edit, add custom stat, remove stat, undo/redo, and save losslessly.
            Assert.True(vm.SelectAssetById("test:laser"));
            var itemDoc = vm.SelectedAsset!;
            Assert.Equal(2, itemDoc.Statistics.Count);
            Assert.Contains(itemDoc.Statistics, static s => s.Key == "wb:mining_speed" && s.Value == "1.75");
            Assert.Contains(itemDoc.Statistics, static s => s.Key == "test:heat" && s.Value == "0.5");

            var speedRow = itemDoc.Statistics.First(static s => s.Key == "wb:mining_speed");
            speedRow.Value = "3.25";
            itemDoc.AddStatisticCommand.Execute(null);
            Assert.Equal(3, itemDoc.Statistics.Count);
            itemDoc.Statistics[^1].Key = "test:crit_chance";
            itemDoc.Statistics[^1].Value = "0.15";

            var heatRow = itemDoc.Statistics.First(static s => s.Key == "test:heat");
            itemDoc.RemoveStatisticCommand.Execute(heatRow);
            Assert.Equal(2, itemDoc.Statistics.Count);

            vm.UndoCommand.Execute(null);
            Assert.Equal(3, itemDoc.Statistics.Count);
            vm.RedoCommand.Execute(null);
            Assert.Equal(2, itemDoc.Statistics.Count);

            vm.SaveAllCommand.Execute(null);
            var savedItemToml = File.ReadAllText(itemPath);
            Assert.Contains("# preserve item comment", savedItemToml, StringComparison.Ordinal);
            Assert.Contains("[Items.Statistics]", savedItemToml, StringComparison.Ordinal);
            Assert.Contains("\"wb:mining_speed\" = 3.25", savedItemToml, StringComparison.Ordinal);
            Assert.Contains("\"test:crit_chance\" = 0.15", savedItemToml, StringComparison.Ordinal);
            Assert.DoesNotContain("test:heat", savedItemToml, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Verifies MainWindow instantiates and binds without errors in headless mode.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowBindsAndMeasuresCleanly()
    {
        var vm = CreateViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        window.Close();
        Assert.NotNull(vm.SelectedAsset);
    }

    private static MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel(
            new SampleStudioWorkspaceService(),
            new SamplePreviewSurfaceProvider(),
            startOnWelcomeScreen: false);
    }
}
