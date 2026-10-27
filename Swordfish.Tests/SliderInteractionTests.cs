using System.Collections.Generic;
using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Components;
using Xunit;

namespace Swordfish.Tests;

public class SliderInteractionTests
{
    [Fact]
    public void DecrementButtonReportsTheStep()
    {
        var harness = new Harness();

        harness.Click("test.minus");

        Assert.Equal("test:-1", harness.Stepped);
    }

    [Fact]
    public void IncrementButtonReportsTheStep()
    {
        var harness = new Harness();

        harness.Click("test.plus");

        Assert.Equal("test:1", harness.Stepped);
    }

    [Fact]
    public void DraggingTheTrackReportsTheRatio()
    {
        var harness = new Harness();

        harness.Click("test.track");

        Assert.Equal("test", harness.DraggedId);
        Assert.InRange(harness.DraggedRatio, 0.4f, 0.6f);
    }

    [Fact]
    public void DisabledSliderReportsNoStep()
    {
        var harness = new Harness(enabled: false);

        harness.Click("test.minus");

        Assert.Null(harness.Stepped);
    }

    private sealed class Harness
    {
        private readonly UIController _controller = new();
        private readonly UIBuilder<Material> _ui;
        private readonly Slider _slider = new();
        private readonly bool _enabled;
        private List<RenderCommand<Material>> _last = [];
        private double _delta;

        public Harness(bool enabled = true)
        {
            _enabled = enabled;
            _ui = new UIBuilder<Material>(400, 100, new StubTextEngine(), _controller);
            Frame();
        }

        public string? Stepped { get; private set; }

        public string? DraggedId { get; private set; }

        public float DraggedRatio { get; private set; }

        public List<RenderCommand<Material>> Frame()
        {
            _delta += 0.05d;
            _slider.Render(
                _ui,
                parameters: new SliderParameters(
                    Id: "test",
                    Fill: new Relative(0.5f),
                    Display: "0.5",
                    Enabled: _enabled,
                    OnStep: (id, delta) => Stepped = $"{id}:{delta}",
                    OnDrag: (id, ratio) =>
                    {
                        DraggedId = id;
                        DraggedRatio = ratio;
                    }
                ),
                callerConstraints: new ConstraintsOverride
                {
                    Width = new Fixed(300),
                    Height = new Fixed(32),
                }
            );

            _last = _ui.Build(_delta);
            return _last;
        }

        public IntRect Find(string id)
        {
            for (var i = 0; i < _last.Count; i++)
            {
                if (_last[i].ID == id)
                {
                    return _last[i].Rect;
                }
            }

            throw new Xunit.Sdk.XunitException($"No render command found for \"{id}\".");
        }

        public void Click(string id)
        {
            IntRect target = Find(id);
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
        private static readonly TextLayout _layout = new(
            new TextConstraints(32, 18, 32, 18),
            [],
            ["OK"],
            lineHeight: 18
        );

        public ITypeface GetDefaultTypeface() => null!;

        public ITypeface[] GetTypefaces() => [];

        public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
        {
            typeface = null;
            return false;
        }

        public TextConstraints Measure(FontOptions fontOptions, string text) => new(32, 18, 32, 18);

        public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length) => new(32, 18, 32, 18);

        public TextLayout Layout(FontOptions fontOptions, string text) => _layout;

        public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => _layout;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length) => _layout;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth) => _layout;

        public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => ["OK"];

        public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth) => ["OK"];
    }
}
