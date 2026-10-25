using Reef;
using Reef.Text;
using Reef.UI;
using Xunit;

namespace Reef.Tests;

public class UIBuilderDefaultFontTests
{
    [Fact]
    public void DefaultFontAppliesToTextWithoutFontId()
    {
        var ui = new UIBuilder<object>(200, 100, new StubTextEngine(), new UIController())
        {
            DefaultFontID = "monogram-extended",
        };

        using (ui.Text("Hello")) { }

        RenderCommand<object> command = ui.Build(delta: 0f)[0];

        Assert.Equal("monogram-extended", command.FontOptions.ID);
    }

    [Fact]
    public void ExplicitFontIdOverridesDefault()
    {
        var ui = new UIBuilder<object>(200, 100, new StubTextEngine(), new UIController())
        {
            DefaultFontID = "monogram-extended",
        };

        using (ui.Text("Icon", new FontOptions { ID = "icons", Size = 16 })) { }

        RenderCommand<object> command = ui.Build(delta: 0f)[0];

        Assert.Equal("icons", command.FontOptions.ID);
    }

    private sealed class StubTextEngine : ITextEngine
    {
        private static readonly TextLayout CachedLayout = new(
            new TextConstraints(50, 20, 50, 20),
            [],
            ["Hello"],
            lineHeight: 20
        );

        public ITypeface GetDefaultTypeface() => null!;

        public ITypeface[] GetTypefaces() => [];

        public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
        {
            typeface = null;
            return false;
        }

        public TextConstraints Measure(FontOptions fontOptions, string text) => new(50, 20, 50, 20);

        public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length) => new(50, 20, 50, 20);

        public TextLayout Layout(FontOptions fontOptions, string text) => CachedLayout;

        public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => CachedLayout;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length) => CachedLayout;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth) => CachedLayout;

        public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => ["Hello"];

        public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth) => ["Hello"];
    }
}
