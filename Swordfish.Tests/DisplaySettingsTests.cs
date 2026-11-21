#nullable enable
using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Swordfish.Graphics;
using Swordfish.Settings;
using WaywardBeyond.Client.UI.Settings;
using Xunit;

namespace Swordfish.Tests;

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
