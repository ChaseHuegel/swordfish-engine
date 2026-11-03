using System.Collections.ObjectModel;
using System.Numerics;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;

namespace Reef.Tests;

public class UIBuilderBorderTests
{
    private const int WIDTH = 64;
    private const int HEIGHT = 64;

    [Fact]
    public void BorderAndCornerCapsRenderInsideElement()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder();
        var background = new Vector4(0f, 0f, 1f, 1f);
        var border = new Vector4(1f, 0f, 0f, 1f);
        var caps = new Vector4(0f, 1f, 0f, 1f);

        using (ui.Element())
        {
            ui.Color = background;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(WIDTH),
                Height = new Fixed(HEIGHT),
            };
            ui.Border = new BorderStyle
            {
                Thickness = 2,
                Color = border,
            };
            ui.CornerCaps = new CornerCapStyle
            {
                Length = 8,
                Thickness = 2,
                Color = caps,
            };
        }

        ReadOnlyCollection<Vector4> pixels = Render(ui);

        AssertColor(background, GetPixel(pixels, 32, 32));
        AssertColor(border, GetPixel(pixels, 32, 0));
        AssertColor(border, GetPixel(pixels, 0, 32));
        AssertColor(caps, GetPixel(pixels, 5, 0));
        AssertColor(caps, GetPixel(pixels, 0, 5));
        AssertColor(border, GetPixel(pixels, 10, 0));
        AssertColor(border, GetPixel(pixels, 0, 10));
        AssertColor(caps, GetPixel(pixels, WIDTH - 5, HEIGHT - 1));
        AssertColor(caps, GetPixel(pixels, WIDTH - 1, HEIGHT - 5));
    }

    [Fact]
    public void BottomOnlyBorderDoesNotDrawOtherSides()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder();
        var background = new Vector4(0f, 0f, 1f, 1f);
        var border = new Vector4(1f, 0f, 0f, 1f);

        using (ui.Element())
        {
            ui.Color = background;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(WIDTH),
                Height = new Fixed(HEIGHT),
            };
            ui.Border = new BorderStyle
            {
                BottomThickness = 2,
                BottomColor = border,
            };
        }

        ReadOnlyCollection<Vector4> pixels = Render(ui);

        AssertColor(background, GetPixel(pixels, 32, 0));
        AssertColor(background, GetPixel(pixels, 0, 32));
        AssertColor(border, GetPixel(pixels, 32, HEIGHT - 1));
    }

    [Fact]
    public void ZeroBorderAndCapsDoNotChangeOutput()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder();
        var background = new Vector4(0.25f, 0.5f, 0.75f, 1f);

        using (ui.Element())
        {
            ui.Color = background;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(WIDTH),
                Height = new Fixed(HEIGHT),
            };
        }

        ReadOnlyCollection<Vector4> pixels = Render(ui);

        AssertColor(background, GetPixel(pixels, 0, 0));
        AssertColor(background, GetPixel(pixels, 32, 32));
        AssertColor(background, GetPixel(pixels, WIDTH - 1, HEIGHT - 1));
    }

    private static UIBuilder<PixelTexture> CreateBuilder()
    {
        return new UIBuilder<PixelTexture>(WIDTH, HEIGHT, new StubTextEngine(), new UIController());
    }

    private static ReadOnlyCollection<Vector4> Render(UIBuilder<PixelTexture> ui)
    {
        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0f);
        var renderer = new PixelRenderer(WIDTH, HEIGHT, ui.TextEngine, new Dictionary<string, PixelFontSDF>());
        renderer.Render(commands);
        return renderer.GetPixels();
    }

    private static Vector4 GetPixel(ReadOnlyCollection<Vector4> pixels, int x, int y)
    {
        return pixels[y * WIDTH + x];
    }

    private static void AssertColor(Vector4 expected, Vector4 actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 3);
        Assert.Equal(expected.Y, actual.Y, precision: 3);
        Assert.Equal(expected.Z, actual.Z, precision: 3);
        Assert.Equal(expected.W, actual.W, precision: 3);
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
