using System.Numerics;
using Reef;
using Reef.Text;
using Reef.UI;

namespace Reef.Tests;

public class UIBuilderTextColorTests
{
    [Fact]
    public void TextInheritsAncestorTextColor()
    {
        var ui = new UIBuilder<PixelTexture>(100, 100, new StubTextEngine(), new UIController());
        var color = new Vector4(0.25f, 0.5f, 0.75f, 1f);

        using (ui.Element())
        {
            ui.TextColor = color;
            ui.TextColor = color;
            using (ui.Element())
            {
                Assert.Equal(color, ui.TextColor);
                using (ui.Text("inherited"))
                {
                    ui.Constraints = new Reef.UI.Constraints
                    {
                        Width = new Reef.Constraints.Fixed(80),
                        Height = new Reef.Constraints.Fixed(20),
                    };
                }
            }
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(0f);
        RenderCommand<PixelTexture> text = commands.Find(command => command.Text == "inherited");
        Assert.Equal(color.X, text.Color.X, 3);
        Assert.Equal(color.Y, text.Color.Y, 3);
        Assert.Equal(color.Z, text.Color.Z, 3);
    }

    [Fact]
    public void ExplicitTextColorWinsOverInheritance()
    {
        var ui = new UIBuilder<PixelTexture>(100, 100, new StubTextEngine(), new UIController());
        var inherited = new Vector4(1f, 0f, 0f, 1f);
        var explicitColor = new Vector4(0f, 1f, 0f, 1f);

        using (ui.Element())
        {
            ui.TextColor = inherited;
            using (ui.Text("explicit"))
            {
                ui.Color = explicitColor;
                ui.Constraints = new Reef.UI.Constraints
                {
                    Width = new Reef.Constraints.Fixed(80),
                    Height = new Reef.Constraints.Fixed(20),
                };
            }
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(0f);
        RenderCommand<PixelTexture> text = commands.Find(command => command.Text == "explicit");
        Assert.Equal(explicitColor.X, text.Color.X, 3);
        Assert.Equal(explicitColor.Y, text.Color.Y, 3);
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
