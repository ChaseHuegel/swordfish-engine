using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using Swordfish.Settings;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.UI.Layers.Menus;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using WaywardBeyond.Config;
using Xunit;

namespace Swordfish.Tests;

public class SettingsInteractionTests
{
    private static readonly Vector4 PRIMARY = new(242f / 255f, 153f / 255f, 74f / 255f, 1f);
    private static readonly Vector4 ACCENT_DARK = new(22f / 255f, 40f / 255f, 56f / 255f, 1f);
    private static readonly Vector4 SELECTION = new(23f / 255f, 35f / 255f, 54f / 255f, 1f);

    [Fact]
    public void ClickingAChoiceCellChangesTheDraft()
    {
        var harness = new Harness();

        harness.Click(harness.Find("Settings.VSync.0"));
        harness.Frame();

        Assert.Equal(PRIMARY, harness.FindCommand("Settings.Apply").Border.Color);
        Assert.Equal(ACCENT_DARK, harness.FindCommand("Settings.VSync.0").Color);
    }

    [Fact]
    public void ClickingATrayCategorySelectsIt()
    {
        var harness = new Harness();

        harness.Click(harness.Find("Settings.Category.audio"));
        harness.Frame();

        Assert.Equal(SELECTION, harness.FindCommand("Settings.Category.audio").Color);
    }

    [Fact]
    public void StepperArrowChangesTheDraft()
    {
        var harness = new Harness();

        IntRect previous = harness.Find("Settings.DisplayMode.next");
        harness.Click(previous);
        harness.Frame();

        Assert.Equal(PRIMARY, harness.FindCommand("Settings.Apply").Border.Color);
    }

    [Fact]
    public void PlaybackDeviceChangeRefreshesTheOutputDeviceRow()
    {
        var harness = new Harness();

        harness.Click(harness.Find("Settings.Category.audio"));
        harness.Frame();

        harness.PlaybackDeviceService.SetDevices("Speakers");
        harness.Frame();

        harness.Click(harness.Find("Settings.OutputDevice.next"));
        harness.Frame();

        Assert.True(harness.HasText("Speakers"));
    }

    [Fact]
    public void UnsupportedDisplayFallbackUpdatesResolutionAndShowsStatus()
    {
        var harness = new Harness();
        harness.WindowContext.Resolution = new Vector2(1920, 1080);
        harness.WindowContext.MonitorResolution = new Vector2(1920, 1080);
        harness.WindowContext.OnApplyDisplaySettings = (mode, borderless, _, _, _) =>
        {
            harness.WindowSettings.Mode.Set(mode);
            harness.WindowSettings.Borderless.Set(borderless);
            harness.WindowSettings.FullscreenWidth.Set(1920);
            harness.WindowSettings.FullscreenHeight.Set(1080);
            return Result.FromFailure("Fullscreen resolution is not supported; fell back to 1920x1080.");
        };

        //  Step from Windowed to Fullscreen (4 steps: BorderlessWindow, Maximized, BorderlessMaximized, Fullscreen).
        for (var i = 0; i < 4; i++)
        {
            harness.Click(harness.Find("Settings.DisplayMode.next"));
            harness.Frame();
        }

        //  Step resolution down to 1280 x 720, then apply so the fake triggers fallback to 1920 x 1080.
        harness.Click(harness.Find("Settings.Resolution.previous"));
        harness.Frame();
        Assert.True(harness.HasText("1280 x 720"));

        harness.Click(harness.Find("Settings.Apply"));
        harness.Frame();

        Assert.True(harness.HasText("1920 x 1080"));
        Assert.True(harness.HasText("ui.settings.status.displayFallback"));
    }

    private sealed class Harness
    {
        private readonly UIController _controller = new();
        private readonly UIBuilder<Material> _ui;
        private readonly SettingsPage<MenuPage> _page;
        private List<RenderCommand<Material>> _last = [];
        private double _delta;

        public FakePlaybackDeviceService PlaybackDeviceService { get; } = new();
        public FakeWindowContext WindowContext { get; } = new();
        public WindowSettings WindowSettings { get; } = new();

        public Harness()
        {
            _ui = new UIBuilder<Material>(1920, 1080, new StubTextEngine(), _controller);
            _page = new SettingsPage<MenuPage>(
                MenuPage.Settings,
                configManager: null!,
                controlSettings: new ControlSettings(),
                windowSettings: WindowSettings,
                renderSettings: new RenderSettings(),
                volumeSettings: new VolumeSettings(),
                playbackDeviceService: PlaybackDeviceService,
                gameplayConfig: new GameplayConfig(),
                uiSettings: new UISettings(),
                soundEffectService: null!,
                windowContext: WindowContext,
                inputService: new FakeInputService(),
                localization: new TestLocalization()
            );

            _page.OnEnter();
            Frame();
        }

        public List<RenderCommand<Material>> Frame()
        {
            _delta += 0.05d;
            _page.RenderPage(0d, _ui, null!);
            _last = _ui.Build(_delta);
            return _last;
        }

        public RenderCommand<Material> FindCommand(string id)
        {
            for (var i = 0; i < _last.Count; i++)
            {
                if (_last[i].ID == id)
                {
                    return _last[i];
                }
            }

            throw new Xunit.Sdk.XunitException($"No render command found for \"{id}\".");
        }

        public IntRect Find(string id)
        {
            return FindCommand(id).Rect;
        }

        public bool HasText(string text)
        {
            for (var i = 0; i < _last.Count; i++)
            {
                if (_last[i].Text == text)
                {
                    return true;
                }
            }

            return false;
        }

        public void Click(IntRect target)
        {
            int x = target.Left + target.Size.X / 2;
            int y = target.Top + target.Size.Y / 2;

            _controller.UpdateMouse(x, y, UIController.MouseButtons.None);
            Frame();
            _controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
            Frame();
            _controller.UpdateMouse(x, y, UIController.MouseButtons.None);
            Frame();
        }
    }
}
