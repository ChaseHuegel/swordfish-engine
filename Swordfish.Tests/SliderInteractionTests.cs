using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Theme;
using Xunit;

namespace Swordfish.Tests;

public class SliderInteractionTests
{
    [Fact]
    public void DecrementButtonCommitsTheLowerValue()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(400, 100, new StubTextEngine(), controller);
        const float initialValue = 0.5f;

        controller.UpdateMouse(12, 16, UIController.MouseButtons.None);
        RenderSlider(ui, initialValue);
        ui.Build(0f);

        controller.UpdateMouse(12, 16, UIController.MouseButtons.Left);
        RenderSlider(ui, initialValue);
        ui.Build(0.1f);

        ControlResult<float> result = RenderSlider(ui, initialValue);
        ui.Build(0.2f);

        Assert.True(result.Changed);
        Assert.Equal(0.4f, result.Value, precision: 3);

        controller.UpdateMouse(12, 16, UIController.MouseButtons.None);
        result = RenderSlider(ui, result.Value);
        ui.Build(0.3f);

        Assert.False(result.Changed);
        Assert.Equal(0.4f, result.Value, precision: 3);
    }

    private static ControlResult<float> RenderSlider(UIBuilder<Material> ui, float value)
    {
        return ui.Slider(
            "test",
            new Constraints
            {
                Width = new Fixed(300),
                Height = new Fixed(UiMetrics.CONTROL_HEIGHT),
            },
            value,
            min: 0f,
            max: 1f,
            step: 0.1f,
            format: value => value.ToString("0.0"),
            enabled: true
        );
    }

    private sealed class StubTextEngine : ITextEngine
    {
        private static readonly TextLayout _cachedLayout = new(
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

        public TextLayout Layout(FontOptions fontOptions, string text) => _cachedLayout;

        public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => _cachedLayout;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length) => _cachedLayout;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth) => _cachedLayout;

        public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => ["OK"];

        public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth) => ["OK"];
    }
}
