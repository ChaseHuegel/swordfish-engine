using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
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

        harness.Click(harness.Find("Settings.VSync.1"));
        harness.Frame();

        Assert.Equal(PRIMARY, harness.FindCommand("Settings.Apply").Border.Color);
        Assert.Equal(ACCENT_DARK, harness.FindCommand("Settings.VSync.1").Color);
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

    private sealed class Harness
    {
        private readonly UIController _controller = new();
        private readonly UIBuilder<Material> _ui;
        private readonly SettingsPage<MenuPage> _page;
        private List<RenderCommand<Material>> _last = [];
        private double _delta;

        public Harness()
        {
            _ui = new UIBuilder<Material>(1920, 1080, new StubTextEngine(), _controller);
            _page = new SettingsPage<MenuPage>(
                MenuPage.Settings,
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
