#nullable enable
using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Swordfish.Graphics;
using Swordfish.Settings;
using Swordfish.Tests;
using WaywardBeyond.Client.UI.Settings;
using Xunit;

namespace WaywardBeyond.Tests;

public class DisplaySettingsTests
{
    [Fact]
    public void ResolveFullscreenVideoMode_MatchesSupportedResolutionPreferringCurrentRefreshRate()
    {
        VideoMode[] supported =
        [
            new VideoMode(new Vector2D<int>(1280, 720), 60),
            new VideoMode(new Vector2D<int>(1920, 1080), 60),
            new VideoMode(new Vector2D<int>(1920, 1080), 144),
        ];
        var current = new VideoMode(new Vector2D<int>(1920, 1080), 60);

        SilkWindowContext.FullscreenModeResolution result = SilkWindowContext.ResolveFullscreenVideoMode(
            1920,
            1080,
            supported,
            current
        );

        Assert.True(result.Status.Success);
        Assert.Equal(new Vector2D<int>(1920, 1080), result.Mode.Resolution);
        Assert.Equal(60, result.Mode.RefreshRate);
    }

    [Fact]
    public void ResolveFullscreenVideoMode_PicksHighestRefreshRateWhenCurrentRateUnavailable()
    {
        VideoMode[] supported =
        [
            new VideoMode(new Vector2D<int>(1280, 720), 60),
            new VideoMode(new Vector2D<int>(1280, 720), 120),
            new VideoMode(new Vector2D<int>(1920, 1080), 144),
        ];
        var current = new VideoMode(new Vector2D<int>(1920, 1080), 144);

        SilkWindowContext.FullscreenModeResolution result = SilkWindowContext.ResolveFullscreenVideoMode(
            1280,
            720,
            supported,
            current
        );

        Assert.True(result.Status.Success);
        Assert.Equal(new Vector2D<int>(1280, 720), result.Mode.Resolution);
        Assert.Equal(120, result.Mode.RefreshRate);
    }

    [Fact]
    public void ResolveFullscreenVideoMode_FallsBackToCurrentSupportedModeWhenUnsupported()
    {
        VideoMode[] supported =
        [
            new VideoMode(new Vector2D<int>(1280, 720), 60),
            new VideoMode(new Vector2D<int>(1920, 1080), 144),
        ];
        var current = new VideoMode(new Vector2D<int>(1920, 1080), 144);

        SilkWindowContext.FullscreenModeResolution result = SilkWindowContext.ResolveFullscreenVideoMode(
            1111,
            777,
            supported,
            current
        );

        Assert.False(result.Status.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Status.Message));
        Assert.Equal(new Vector2D<int>(1920, 1080), result.Mode.Resolution);
        Assert.Equal(144, result.Mode.RefreshRate);
    }

    [Fact]
    public void ResolveFullscreenVideoMode_FallsBackWhenDimensionsAreNonPositive()
    {
        VideoMode[] supported =
        [
            new VideoMode(new Vector2D<int>(1280, 720), 60),
            new VideoMode(new Vector2D<int>(1920, 1080), 60),
        ];
        var current = new VideoMode(new Vector2D<int>(1920, 1080), 60);

        SilkWindowContext.FullscreenModeResolution result = SilkWindowContext.ResolveFullscreenVideoMode(
            0,
            -100,
            supported,
            current
        );

        Assert.False(result.Status.Success);
        Assert.Equal(new Vector2D<int>(1920, 1080), result.Mode.Resolution);
    }

    [Fact]
    public void ResolveFullscreenVideoMode_UsesCurrentModeWhenRequestedSizeIsNull()
    {
        VideoMode[] supported =
        [
            new VideoMode(new Vector2D<int>(1280, 720), 60),
            new VideoMode(new Vector2D<int>(1920, 1080), 120),
        ];
        var current = new VideoMode(new Vector2D<int>(1920, 1080), 120);

        SilkWindowContext.FullscreenModeResolution result = SilkWindowContext.ResolveFullscreenVideoMode(
            null,
            null,
            supported,
            current
        );

        Assert.True(result.Status.Success);
        Assert.Equal(new Vector2D<int>(1920, 1080), result.Mode.Resolution);
        Assert.Equal(120, result.Mode.RefreshRate);
    }

    [Fact]
    public void ResolveFullscreenVideoMode_ReportsFailureWhenNoSupportedModesExist()
    {
        SilkWindowContext.FullscreenModeResolution result = SilkWindowContext.ResolveFullscreenVideoMode(
            1920,
            1080,
            [],
            VideoMode.Default
        );

        Assert.False(result.Status.Success);
        Assert.Null(result.Mode.Resolution);
    }

    [Fact]
    public void DisplaySettingsCategory_EnablesResolutionInWindowedAndExclusiveFullscreen()
    {
        var windowContext = new FakeWindowContext
        {
            Resolution = new Vector2(1920, 1080),
            MonitorResolution = new Vector2(1920, 1080),
        };
        SettingsDraft draft = SettingsDraft.CreateDefault(windowContext);
        SettingsCategory category = DisplaySettingsCategory.Build(draft, windowContext, new TestLocalization(), () => { });
        SettingsEntry resolutionRow = FindResolutionRow(category);

        draft.DisplayMode = SettingsDisplayMode.Windowed;
        Assert.True(resolutionRow.CanEdit);

        draft.DisplayMode = SettingsDisplayMode.BorderlessWindow;
        Assert.True(resolutionRow.CanEdit);

        draft.DisplayMode = SettingsDisplayMode.Fullscreen;
        Assert.True(resolutionRow.CanEdit);

        draft.DisplayMode = SettingsDisplayMode.Maximized;
        Assert.False(resolutionRow.CanEdit);

        draft.DisplayMode = SettingsDisplayMode.BorderlessMaximized;
        Assert.False(resolutionRow.CanEdit);

        draft.DisplayMode = SettingsDisplayMode.BorderlessFullscreen;
        Assert.False(resolutionRow.CanEdit);
    }

    [Fact]
    public void GetWindowResolution_UsesPersistedFullscreenResolutionInExclusiveFullscreen()
    {
        var windowSettings = new WindowSettings();
        windowSettings.Mode.Set(WindowMode.Fullscreen);
        windowSettings.Borderless.Set(false);
        windowSettings.Width.Set(1280);
        windowSettings.Height.Set(720);
        windowSettings.FullscreenWidth.Set(2560);
        windowSettings.FullscreenHeight.Set(1440);

        var windowContext = new FakeWindowContext
        {
            Resolution = new Vector2(1920, 1080),
            MonitorResolution = new Vector2(1920, 1080),
        };

        SettingsResolution resolution = SettingsDraft.GetWindowResolution(windowSettings, windowContext);

        Assert.Equal(new SettingsResolution(2560, 1440), resolution);
    }

    [Fact]
    public void ComputeOverlapArea_PicksMonitorWithLargestIntersection()
    {
        //  Window straddles primary monitor (0..1920) and secondary monitor (1920..3840), mostly on secondary.
        long primaryOverlap = SilkWindowContext.ComputeOverlapArea(1700, 100, 1280, 720, 0, 0, 1920, 1080);
        long secondaryOverlap = SilkWindowContext.ComputeOverlapArea(1700, 100, 1280, 720, 1920, 0, 1920, 1080);

        Assert.True(secondaryOverlap > primaryOverlap);
        Assert.Equal(220L * 720L, primaryOverlap);
        Assert.Equal(1060L * 720L, secondaryOverlap);
    }

    [Fact]
    public void ComputeOverlapArea_ReturnsZeroForDisjointRectangles()
    {
        long overlap = SilkWindowContext.ComputeOverlapArea(-1920, 0, 1920, 1080, 0, 0, 1920, 1080);
        Assert.Equal(0L, overlap);
    }

    [Fact]
    public void FormatMonitorNames_DisambiguatesDuplicatesAndBlankNames()
    {
        var names = SilkWindowContext.FormatMonitorNames(["DP-1", "DP-1", "", "HDMI-A-1"]);

        Assert.Equal(["DP-1", "DP-1 (2)", "Monitor 3", "HDMI-A-1"], names);
    }

    [Fact]
    public void DisplaySettingsCategory_MonitorRowStartsAtAutoAndStepsThroughMonitors()
    {
        var windowContext = new FakeWindowContext();
        windowContext.Monitors.AddRange(["DP-1", "HDMI-A-1"]);
        SettingsDraft draft = SettingsDraft.CreateDefault(windowContext);
        SettingsCategory category = DisplaySettingsCategory.Build(draft, windowContext, new TestLocalization(), () => { });
        StepperEntry<string> monitorRow = FindMonitorRow(category);

        Assert.Equal("ui.settings.value.auto", monitorRow.Snapshot().Value);
        Assert.Equal(1, monitorRow.Step(1));
        Assert.Equal("DP-1", draft.Monitor);
        Assert.Equal(1, monitorRow.Step(1));
        Assert.Equal("HDMI-A-1", draft.Monitor);
    }

    [Fact]
    public void DisplaySettingsCategory_MonitorRowKeepsUnavailableSelection()
    {
        var windowContext = new FakeWindowContext();
        windowContext.Monitors.Add("DP-1");
        SettingsDraft draft = SettingsDraft.CreateDefault(windowContext);
        draft.Monitor = "HDMI-A-1";
        SettingsCategory category = DisplaySettingsCategory.Build(draft, windowContext, new TestLocalization(), () => { });
        StepperEntry<string> monitorRow = FindMonitorRow(category);

        Assert.Equal("HDMI-A-1 (Unavailable)", monitorRow.Snapshot().Value);
    }

    [Fact]
    public void DisplaySettingsCategory_MonitorRowIsEnabledOnlyInFullscreen()
    {
        var windowContext = new FakeWindowContext();
        SettingsDraft draft = SettingsDraft.CreateDefault(windowContext);
        SettingsCategory category = DisplaySettingsCategory.Build(draft, windowContext, new TestLocalization(), () => { });
        StepperEntry<string> monitorRow = FindMonitorRow(category);

        draft.DisplayMode = SettingsDisplayMode.Windowed;
        Assert.False(monitorRow.CanEdit);

        draft.DisplayMode = SettingsDisplayMode.BorderlessWindow;
        Assert.False(monitorRow.CanEdit);

        draft.DisplayMode = SettingsDisplayMode.Maximized;
        Assert.False(monitorRow.CanEdit);

        draft.DisplayMode = SettingsDisplayMode.BorderlessMaximized;
        Assert.False(monitorRow.CanEdit);

        draft.DisplayMode = SettingsDisplayMode.BorderlessFullscreen;
        Assert.False(monitorRow.CanEdit);

        draft.DisplayMode = SettingsDisplayMode.Fullscreen;
        Assert.True(monitorRow.CanEdit);
    }

    private static StepperEntry<string> FindMonitorRow(SettingsCategory category)
    {
        for (var i = 0; i < category.Entries.Length; i++)
        {
            if (category.Entries[i] is StepperEntry<string> row && row.Id == "Settings.Monitor")
            {
                return row;
            }
        }

        throw new Xunit.Sdk.XunitException("No monitor row was found.");
    }

    private static SettingsEntry FindResolutionRow(SettingsCategory category)
    {
        for (var i = 0; i < category.Entries.Length; i++)
        {
            if (category.Entries[i].Id == "Settings.Resolution")
            {
                return category.Entries[i];
            }
        }

        throw new Xunit.Sdk.XunitException("No resolution row was found.");
    }
}
