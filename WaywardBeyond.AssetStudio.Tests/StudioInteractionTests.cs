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
