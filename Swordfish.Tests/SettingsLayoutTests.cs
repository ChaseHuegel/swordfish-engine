using Swordfish.Settings;
using WaywardBeyond.Client.UI.Settings;
using Xunit;

namespace Swordfish.Tests;

public class SettingsLayoutTests
{
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(1280, 720)]
    [InlineData(900, 720)]
    [InlineData(899, 720)]
    [InlineData(800, 600)]
    public void PageFitsInsideWindow(int windowWidth, int windowHeight)
    {
        SettingsLayout layout = SettingsLayout.Compute(windowWidth, windowHeight);

        Assert.True(layout.PageWidth > 0);
        Assert.True(layout.PageHeight > 0);
        Assert.True(layout.PageWidth <= windowWidth);
        Assert.True(layout.PageHeight <= windowHeight);
        Assert.True(layout.Footer.Bottom <= layout.PageHeight);
        Assert.True(layout.Pane.Size.X > 0);
        Assert.True(layout.Pane.Size.Y > 0);
        Assert.True(layout.LabelWidth > 0);
        Assert.True(layout.ControlWidth > 0);
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1280, 720)]
    [InlineData(900, 720)]
    public void WideLayoutUsesNonOverlappingTrayAndPane(int windowWidth, int windowHeight)
    {
        SettingsLayout layout = SettingsLayout.Compute(windowWidth, windowHeight);

        Assert.False(layout.UseTabs);
        Assert.Equal(12, layout.Tray.Top);
        Assert.True(layout.Tray.Right <= layout.Pane.Left);
        Assert.True(layout.Tray.Right <= layout.PageWidth);
        Assert.True(layout.Pane.Right <= layout.PageWidth);
        Assert.True(layout.Tray.Bottom <= layout.Footer.Top);
        Assert.True(layout.Pane.Bottom <= layout.Footer.Top);
    }

    [Theory]
    [InlineData(899, 720)]
    [InlineData(800, 600)]
    public void NarrowLayoutUsesTabs(int windowWidth, int windowHeight)
    {
        SettingsLayout layout = SettingsLayout.Compute(windowWidth, windowHeight);

        Assert.True(layout.UseTabs);
        Assert.Equal(0, layout.Tray.Size.X);
        Assert.True(layout.Tabs.Bottom <= layout.Pane.Top);
        Assert.True(layout.Pane.Right <= layout.PageWidth);
    }

    [Theory]
    [InlineData((int)SettingsDisplayMode.Windowed)]
    [InlineData((int)SettingsDisplayMode.BorderlessWindow)]
    [InlineData((int)SettingsDisplayMode.Maximized)]
    [InlineData((int)SettingsDisplayMode.BorderlessMaximized)]
    [InlineData((int)SettingsDisplayMode.Fullscreen)]
    [InlineData((int)SettingsDisplayMode.BorderlessFullscreen)]
    public void DisplayModeRoundTrips(int modeValue)
    {
        var mode = (SettingsDisplayMode)modeValue;
        WindowMode windowMode = mode.ToWindowMode();
        SettingsDisplayMode roundTripped = SettingsDisplayModeExtensions.FromWindowSettings(windowMode, mode.IsBorderless());

        Assert.Equal(mode, roundTripped);
    }

    [Theory]
    [InlineData((int)SettingsDisplayMode.Windowed, true)]
    [InlineData((int)SettingsDisplayMode.BorderlessWindow, true)]
    [InlineData((int)SettingsDisplayMode.Maximized, false)]
    [InlineData((int)SettingsDisplayMode.BorderlessMaximized, false)]
    [InlineData((int)SettingsDisplayMode.Fullscreen, false)]
    [InlineData((int)SettingsDisplayMode.BorderlessFullscreen, false)]
    public void ResolutionEnabledOnlyForWindowModes(int modeValue, bool expected)
    {
        var mode = (SettingsDisplayMode)modeValue;
        Assert.Equal(expected, mode.UsesWindowSize());
    }
}
