using System.Collections.Generic;
using Reef;
using Reef.Constraints;
using Reef.MSDF;
using Reef.Text;
using Reef.UI;
using Xunit;

namespace Reef.Tests;

[Collection("TextEngine")]
public class UIBuilderQueryTests
{
    /// <summary>A query must not rename the element that asks it.</summary>
    [Fact]
    public void IdQueriesDoNotChangeTheCurrentElementId()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder(100, 100);

        using (ui.Element("target"))
        {
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(40),
                Height = new Fixed(40),
            };

            ui.Hovering("other");
            ui.Entered("other");
            ui.Exited("other");
            ui.Scrolled("other");
            ui.Held("other");
            ui.Clicked("other");
            ui.Released("other");
            ui.Focused("other");
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);
        RenderCommand<PixelTexture> command = Assert.Single(commands, command => command.ID != null);

        Assert.Equal("target", command.ID);
    }

    private static UIBuilder<PixelTexture> CreateBuilder(int width, int height)
    {
        return new UIBuilder<PixelTexture>(width, height, new StubTextEngine(), new UIController());
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
