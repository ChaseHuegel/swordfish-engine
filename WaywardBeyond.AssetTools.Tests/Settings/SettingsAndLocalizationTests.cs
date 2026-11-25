using System.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Localization;
using WaywardBeyond.AssetTools.Settings;

namespace WaywardBeyond.AssetTools.Tests.Settings;

/// <summary>
///     Verifies portable mode path resolution, settings TOML persistence, and ResX tool localization.
/// </summary>
public sealed class SettingsAndLocalizationTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary directory for settings and portable marker tests.
    /// </summary>
    public SettingsAndLocalizationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-settings-tests-{Guid.NewGuid():N}");
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
    public void PortableMarkerSwitchesSettingsAndDraftsBesideExecutable()
    {
        string exeDir = Path.Combine(_tempRoot, "app");
        string userRoot = Path.Combine(_tempRoot, "user-data");
        Directory.CreateDirectory(exeDir);
        Directory.CreateDirectory(userRoot);

        ToolDataPaths standardPaths = ToolDataPaths.Resolve(exeDir, userRoot);
        Assert.False(standardPaths.IsPortable);
        Assert.Equal(Path.Combine(userRoot, "WaywardBeyond", "AssetStudio"), standardPaths.DataDirectory);
        Assert.Equal(Path.Combine(standardPaths.DataDirectory, "settings.toml"), standardPaths.SettingsFilePath);
        Assert.Equal(Path.Combine(standardPaths.DataDirectory, "drafts"), standardPaths.DraftsDirectory);

        File.WriteAllText(Path.Combine(exeDir, ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);

        ToolDataPaths portablePaths = ToolDataPaths.Resolve(exeDir, userRoot);
        Assert.True(portablePaths.IsPortable);
        Assert.Equal(exeDir, portablePaths.DataDirectory);
        Assert.Equal(Path.Combine(exeDir, "settings.toml"), portablePaths.SettingsFilePath);
        Assert.Equal(Path.Combine(exeDir, "drafts"), portablePaths.DraftsDirectory);
    }

    [Fact]
    public void ToolSettingsRoundTripViaTomlAndHandleMissingOrCorruptFiles()
    {
        string exeDir = Path.Combine(_tempRoot, "portable-app");
        Directory.CreateDirectory(exeDir);
        File.WriteAllText(Path.Combine(exeDir, ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);

        ToolDataPaths paths = ToolDataPaths.Resolve(exeDir);
        var store = new ToolSettingsStore(paths);

        Result<ToolSettings> missingLoad = store.Load();
        Assert.True(missingLoad.Success);
        Assert.Equal(ToolTheme.System, missingLoad.Value.Theme);
        Assert.Equal(ToolSettings.DEFAULT_UI_SCALE, missingLoad.Value.UiScale);
        Assert.Empty(missingLoad.Value.RecentWorkspaces);
        Assert.Empty(missingLoad.Value.WorkspaceDefaultNamespaces);

        string wsA = Path.Combine(_tempRoot, "ws-a");
        string wsB = Path.Combine(_tempRoot, "ws.with.dots");
        var settings = new ToolSettings
        {
            CultureOverride = "en-US",
            Theme = ToolTheme.Light,
            UiScale = 1.25,
            GlPreference = GlBackendPreference.Egl,
            LogLevel = ToolLogLevel.Debug
        };
        settings.AddRecentWorkspace(wsA);
        settings.AddRecentWorkspace(wsB);
        settings.AddRecentWorkspace(wsA);
        settings.SetDefaultNamespace(wsA + Path.DirectorySeparatorChar, "wb:");
        settings.SetDefaultNamespace(wsB, "colony");
        settings.PanelLayout.LeftWidth = 310.5;
        settings.PanelLayout.RightWidth = 400.0;
        settings.PanelLayout.BottomHeight = 180.0;
        settings.PanelLayout.IsValidationOpen = false;
        settings.PanelLayout.IsPreviewOpen = true;
        settings.PanelLayout.CollapsedSections["Brick.Colliders"] = true;
        settings.PanelLayout.CollapsedSections["Item.WorldModel"] = false;

        Assert.True(store.Save(settings).Success);

        Result<ToolSettings> loadedResult = store.Load();
        Assert.True(loadedResult.Success);
        ToolSettings loaded = loadedResult.Value;

        Assert.Equal("en-US", loaded.CultureOverride);
        Assert.Equal(ToolTheme.Light, loaded.Theme);
        Assert.Equal(1.25, loaded.UiScale);
        Assert.Equal(GlBackendPreference.Egl, loaded.GlPreference);
        Assert.Equal(ToolLogLevel.Debug, loaded.LogLevel);
        Assert.Equal(2, loaded.RecentWorkspaces.Count);
        Assert.Equal(Path.GetFullPath(wsA), loaded.RecentWorkspaces[0]);
        Assert.Equal("wb", loaded.GetDefaultNamespace(wsA));
        Assert.Equal("wb", loaded.GetDefaultNamespace(wsA + Path.DirectorySeparatorChar));
        Assert.Equal("colony", loaded.GetDefaultNamespace(wsB));
        Assert.Null(loaded.GetDefaultNamespace(Path.Combine(_tempRoot, "unset")));
        Assert.Equal(310.5, loaded.PanelLayout.LeftWidth);
        Assert.Equal(400.0, loaded.PanelLayout.RightWidth);
        Assert.Equal(180.0, loaded.PanelLayout.BottomHeight);
        Assert.False(loaded.PanelLayout.IsValidationOpen);
        Assert.True(loaded.PanelLayout.IsPreviewOpen);
        Assert.True(loaded.PanelLayout.CollapsedSections["Brick.Colliders"]);
        Assert.False(loaded.PanelLayout.CollapsedSections["Item.WorldModel"]);

        //  Clearing a workspace namespace removes the persisted entry cleanly.
        loaded.SetDefaultNamespace(wsB, string.Empty);
        Assert.Null(loaded.GetDefaultNamespace(wsB));
        Assert.True(store.Save(loaded).Success);
        ToolSettings reloadedAfterClear = store.Load().Value;
        Assert.Null(reloadedAfterClear.GetDefaultNamespace(wsB));
        Assert.Equal("wb", reloadedAfterClear.GetDefaultNamespace(wsA));

        //  Out-of-range UiScale values clamp to 0.75..1.75 both via setter and when loaded from disk.
        loaded.UiScale = 0.2;
        Assert.Equal(ToolSettings.MIN_UI_SCALE, loaded.UiScale);
        loaded.UiScale = 3.5;
        Assert.Equal(ToolSettings.MAX_UI_SCALE, loaded.UiScale);
        loaded.UiScale = double.NaN;
        Assert.Equal(ToolSettings.DEFAULT_UI_SCALE, loaded.UiScale);

        File.WriteAllText(paths.SettingsFilePath, "UiScale = 4.25\n");
        Assert.Equal(ToolSettings.MAX_UI_SCALE, store.Load().Value.UiScale);

        File.WriteAllText(paths.SettingsFilePath, "UiScale = 0.1\n");
        Assert.Equal(ToolSettings.MIN_UI_SCALE, store.Load().Value.UiScale);

        File.WriteAllText(paths.SettingsFilePath, "Theme = \"unterminated");
        Result<ToolSettings> corruptLoad = store.Load();
        Assert.False(corruptLoad.Success);
    }

    [Fact]
    public void ToolLocalizationUsesTypedResxOverrideAndEnglishFallback()
    {
        CultureInfo? previousCulture = Strings.Culture;
        try
        {
            CultureInfo resolvedDefault = ToolLocalization.ApplyCulture(string.Empty, CultureInfo.GetCultureInfo("fr-FR"));
            Assert.Equal("fr-FR", resolvedDefault.Name);
            Assert.Equal("Item", Strings.ItemForm_Title);
            Assert.Equal("Wayward Beyond Asset Studio", Strings.App_Title);
            Assert.Equal("Validation", ToolLocalization.GetString("Validation.PanelTitle"));

            CultureInfo resolvedOverride = ToolLocalization.ApplyCulture("en-US", CultureInfo.GetCultureInfo("de-DE"));
            Assert.Equal("en-US", resolvedOverride.Name);
            Assert.Equal("Brick", Strings.BrickForm_Title);

            CultureInfo invalidFallback = ToolLocalization.ResolveCulture("!!!invalid!!!", CultureInfo.GetCultureInfo("en-GB"));
            Assert.Equal("en-GB", invalidFallback.Name);

#if DEBUG
            Assert.True(ToolLocalization.IsPseudoLocaleAvailable());
            CultureInfo pseudo = ToolLocalization.ApplyCulture(ToolLocalization.PSEUDO_LOCALE_NAME);
            Assert.Equal(ToolLocalization.PSEUDO_LOCALE_NAME, pseudo.Name, ignoreCase: true);
            string pseudoTitle = Strings.ItemForm_Title;
            Assert.StartsWith("[!!", pseudoTitle, StringComparison.Ordinal);
            Assert.EndsWith("!!]", pseudoTitle, StringComparison.Ordinal);
            Assert.True(pseudoTitle.Length > "Item".Length);

            string[] keys =
            [
                "App.Title",
                "StartScreen.OpenRepo",
                "StartScreen.OpenPack",
                "StartScreen.OpenInstall",
                "StartScreen.NewPack",
                "StartScreen.RecentWorkspaces",
                "ItemForm.Title",
                "BrickForm.Title",
                "PaintForm.Title",
                "SkillForm.Title",
                "BodyForm.Title",
                "MaterialForm.Title",
                "BlueprintForm.Title",
                "Search.Placeholder",
                "Validation.PanelTitle",
                "Drafts.RecoveryTitle",
                "Save.ExternalConflict",
                "Settings.Title",
                "About.Title",
                "Actions.Save",
                "Actions.SaveAll",
                "Actions.Format",
                "Actions.Validate",
                "Actions.Cancel"
            ];

            for (int i = 0; i < keys.Length; i++)
            {
                string english = ToolLocalization.GetString(keys[i], ToolLocalization.FallbackCulture);
                string expanded = ToolLocalization.GetString(keys[i], pseudo);
                Assert.True(expanded.Length > english.Length, $"Pseudo-locale key '{keys[i]}' is not expanded.");
            }
#else
            Assert.False(ToolLocalization.IsPseudoLocaleAvailable());
#endif
        }
        finally
        {
            Strings.Culture = previousCulture;
        }
    }

    [Fact]
    public void RemoveRecentWorkspaceRemovesMatchingEntriesAndPersists()
    {
        string exeDir = Path.Combine(_tempRoot, "remove-recent-app");
        Directory.CreateDirectory(exeDir);
        File.WriteAllText(Path.Combine(exeDir, ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);

        var store = new ToolSettingsStore(ToolDataPaths.Resolve(exeDir));
        string wsA = Path.Combine(_tempRoot, "pack_a");
        string wsB = Path.Combine(_tempRoot, "pack_b");
        var settings = new ToolSettings();
        settings.AddRecentWorkspace(wsA);
        settings.AddRecentWorkspace(wsB);

        Assert.True(settings.RemoveRecentWorkspace(wsA + Path.DirectorySeparatorChar));
        Assert.False(settings.RemoveRecentWorkspace(wsA));
        Assert.False(settings.RemoveRecentWorkspace("   "));
        Assert.Single(settings.RecentWorkspaces);
        Assert.Equal(Path.GetFullPath(wsB), settings.RecentWorkspaces[0]);

        Assert.True(store.Save(settings).Success);
        ToolSettings reloaded = store.Load().Value;
        Assert.Single(reloaded.RecentWorkspaces);
        Assert.Equal(Path.GetFullPath(wsB), reloaded.RecentWorkspaces[0]);
    }
}
