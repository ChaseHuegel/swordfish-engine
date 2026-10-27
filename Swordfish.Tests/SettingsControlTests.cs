using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Components;
using Xunit;

namespace Swordfish.Tests;

public class SettingsControlTests
{
    private static readonly Vector4 ACCENT_DARK = new(22f / 255f, 40f / 255f, 56f / 255f, 1f);
    private static readonly Vector4 CONTROL = new(21f / 255f, 29f / 255f, 42f / 255f, 1f);

    [Fact]
    public void SelectedCellUsesTheSelectedFill()
    {
        var harness = new Harness(selected: 0);

        Assert.Equal(ACCENT_DARK, harness.Find("test.0").Color);
        Assert.Equal(CONTROL, harness.Find("test.1").Color);
    }

    [Fact]
    public void ClickingAnUnselectedCellReportsItsIndex()
    {
        var harness = new Harness(selected: 0);

        harness.Click("test.1");

        Assert.Equal("test:1", harness.Selected);
    }

    [Fact]
    public void ClickingTheSelectedCellReportsNothing()
    {
        var harness = new Harness(selected: 0);

        harness.Click("test.0");

        Assert.Null(harness.Selected);
    }

    [Fact]
    public void DisabledControlReportsNoSelection()
    {
        var harness = new Harness(selected: 0, enabled: false);

        harness.Click("test.1");

        Assert.Null(harness.Selected);
    }

    private sealed class Harness
    {
        private readonly UIController _controller = new();
        private readonly UIBuilder<Material> _ui;
        private readonly SegmentedControl _control = new();
        private readonly int _selected;
        private readonly bool _enabled;
        private List<RenderCommand<Material>> _last = [];
        private double _delta;

        public Harness(int selected, bool enabled = true)
        {
            _selected = selected;
            _enabled = enabled;
            _ui = new UIBuilder<Material>(400, 80, new StubTextEngine(), _controller);
            Frame();
        }

        public string? Selected { get; private set; }

        private List<RenderCommand<Material>> Frame()
        {
            _delta += 0.05d;
            _control.Render(
                _ui,
                parameters: new SegmentedControlParameters(
                    Id: "test",
                    Options: ["Off", "On"],
                    Selected: _selected,
                    Enabled: _enabled,
                    OnSelect: (id, index) => Selected = $"{id}:{index}"
                ),
                callerConstraints: new ConstraintsOverride
                {
                    X = new Fixed(20),
                    Y = new Fixed(24),
                    Width = new Fixed(360),
                    Height = new Fixed(32),
                }
            );

            _last = _ui.Build(_delta);
            return _last;
        }

        public RenderCommand<Material> Find(string id)
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

        public void Click(string id)
        {
            IntRect target = Find(id).Rect;
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

    private sealed class StubTextEngine : ITextEngine
    {
        public ITypeface GetDefaultTypeface() => null!;

        public ITypeface[] GetTypefaces() => [];

        public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
        {
            typeface = null;
            return false;
        }

        public Reef.Text.TextConstraints Measure(FontOptions fontOptions, string text) => new(32, 18, 32, 18);

        public Reef.Text.TextConstraints Measure(FontOptions fontOptions, string text, int start, int length) => new(32, 18, 32, 18);

        public Reef.Text.TextLayout Layout(FontOptions fontOptions, string text) => Reef.Text.TextLayout.Empty;

        public Reef.Text.TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => Reef.Text.TextLayout.Empty;

        public Reef.Text.TextLayout Layout(FontOptions fontOptions, string text, int start, int length) => Reef.Text.TextLayout.Empty;

        public Reef.Text.TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth) => Reef.Text.TextLayout.Empty;

        public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => [];

        public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth) => [];
    }
}
