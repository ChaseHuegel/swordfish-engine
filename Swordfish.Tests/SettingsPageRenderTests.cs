#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Settings;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using WaywardBeyond.Client.UI.Settings;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Config;
using Xunit;

namespace Swordfish.Tests;

public class SettingsPageRenderTests
{
    [Fact]
    public void WidePageCentersAndKeepsControlsInsideList()
    {
        const int windowWidth = 1920;
        const int windowHeight = 1080;
        SettingsLayout layout = SettingsLayout.Compute(windowWidth, windowHeight);
        List<RenderCommand<Material>> commands = RenderSettingsPage(windowWidth, windowHeight);

        IntRect pageRect = FindRequired(commands, "SettingsPage").Rect;
        Assert.Equal((windowWidth - layout.PageWidth) / 2, pageRect.Left);
        Assert.Equal((windowHeight - layout.PageHeight) / 2, pageRect.Top);
        Assert.Equal(layout.PageWidth, pageRect.Size.X);
        Assert.Equal(layout.PageHeight, pageRect.Size.Y);

        var listRect = new IntRect(
            new IntVector2(pageRect.Left + layout.List.Left, pageRect.Top + layout.List.Top),
            layout.List.Size
        );

        string[] controlIds =
        [
            "Settings.Category.display",
            "Settings.DisplayMode.next",
            "Settings.VSync.0",
            "Settings.VSync.1",
            "Settings.Fov.track",
            "Settings.AntiAliasing.0",
            "Settings.AntiAliasing.1",
            "Settings.Cancel",
        ];

        for (var i = 0; i < controlIds.Length; i++)
        {
            IntRect rect = FindRequired(commands, controlIds[i]).Rect;
            Assert.True(Contains(pageRect, rect), $"{controlIds[i]} escaped the page.");
        }

        for (var i = 1; i < controlIds.Length - 1; i++)
        {
            IntRect rect = FindRequired(commands, controlIds[i]).Rect;
            Assert.True(Contains(listRect, rect), $"{controlIds[i]} escaped the list.");
        }
    }

    [Fact]
    public void ResetRowUsesDangerStyling()
    {
        List<RenderCommand<Material>> commands = RenderSettingsPage(1920, 1080);

        RenderCommand<Material> reset = FindRequired(commands, "Settings.Reset.display");

        Assert.Equal(1, reset.Border.Thickness);
        Assert.Equal(UiTheme.Danger, reset.Border.Color);
    }

    [Fact]
    public void NarrowPageUsesTabs()
    {
        const int windowWidth = 800;
        const int windowHeight = 600;
        SettingsLayout layout = SettingsLayout.Compute(windowWidth, windowHeight);
        Assert.True(layout.UseTabs);
        Assert.Equal(64, layout.Pane.Top);
        List<RenderCommand<Material>> commands = RenderSettingsPage(windowWidth, windowHeight);

        Assert.True(
            commands.Exists(command => command.ID == "Settings.Tab.display"),
            string.Join(
                "\n",
                commands.ConvertAll(command =>
                    $"{command.ID ?? "<null>"} {command.Rect.Left},{command.Rect.Top} {command.Rect.Size.X}x{command.Rect.Size.Y}"
                )
            )
        );
        Assert.DoesNotContain(commands, command => command.ID == "Settings.Category.display");
        Assert.Contains(commands, command => command.ID == "Settings.Cancel");
    }

    private static List<RenderCommand<Material>> RenderSettingsPage(int width, int height)
    {
        var page = new MainMenuSettingsPage(
            configManager: null!,
            controlSettings: new ControlSettings(),
            windowSettings: new WindowSettings(),
            renderSettings: new RenderSettings(),
            volumeSettings: new VolumeSettings(),
            gameplayConfig: new GameplayConfig(),
            uiSettings: new UISettings(),
            soundEffectService: null!,
            windowContext: new FakeWindowContext(),
            inputService: new FakeInputService(),
            localization: new StubLocalization()
        );

        page.OnEnter();

        var ui = new UIBuilder<Material>(width, height, new StubTextEngine(), new UIController());
        page.RenderPage(delta: 0d, ui, menu: null!);
        return ui.Build(delta: 0d);
    }

    private static RenderCommand<Material> FindRequired(List<RenderCommand<Material>> commands, string id)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            if (commands[i].ID == id)
            {
                return commands[i];
            }
        }

        throw new Xunit.Sdk.XunitException($"No render command found for \"{id}\".");
    }

    private static bool Contains(IntRect outer, IntRect inner)
    {
        return inner.Left >= outer.Left
            && inner.Top >= outer.Top
            && inner.Right <= outer.Right
            && inner.Bottom <= outer.Bottom;
    }

    private sealed class StubLocalization : ILocalization
    {
        public string? GetString(string value) => value;

        public string? GetString(string value, string cultureName) => value;

        public string? GetString(string value, System.Globalization.CultureInfo cultureInfo) => value;
    }

    private sealed class StubTextEngine : ITextEngine
    {
        public ITypeface GetDefaultTypeface() => null!;

        public ITypeface[] GetTypefaces() => [];

        public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
        {
            typeface = null;
            return false;
        }

        public TextConstraints Measure(FontOptions fontOptions, string text) => default;

        public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length) => default;

        public TextLayout Layout(FontOptions fontOptions, string text) => TextLayout.Empty;

        public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => TextLayout.Empty;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length) => TextLayout.Empty;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth) => TextLayout.Empty;

        public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => [];

        public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth) => [];
    }
}
