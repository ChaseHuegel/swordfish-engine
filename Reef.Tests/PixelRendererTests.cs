using System.Collections.ObjectModel;
using System.Numerics;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Xunit;

namespace Reef.Tests;

public class PixelRendererTests
{
    private const int SIZE = 8;

    [Fact]
    public void EmptyFrameClearsThePreviousFrame()
    {
        PixelRenderer renderer = CreateRenderer();
        renderer.Render(CreateFrame(new Vector4(1f, 0f, 0f, 1f)));
        renderer.Render([]);

        ReadOnlyCollection<Vector4> pixels = renderer.GetPixels();
        for (var i = 0; i < pixels.Count; i++)
        {
            Assert.Equal(Vector4.Zero, pixels[i]);
        }
    }

    [Fact]
    public void OpaqueFullScreenFrameReplacesThePreviousFrame()
    {
        var blue = new Vector4(0f, 0f, 1f, 1f);

        PixelRenderer renderer = CreateRenderer();
        renderer.Render(CreateFrame(new Vector4(1f, 0f, 0f, 1f)));
        renderer.Render(CreateFrame(blue));

        ReadOnlyCollection<Vector4> pixels = renderer.GetPixels();
        for (var i = 0; i < pixels.Count; i++)
        {
            AssertColor(blue, pixels[i]);
        }
    }

    [Fact]
    public void TranslucentFrameDrawsOverAClearedTarget()
    {
        var translucentBlue = new Vector4(0f, 0f, 1f, 0.5f);

        PixelRenderer renderer = CreateRenderer();
        renderer.Render(CreateFrame(new Vector4(1f, 0f, 0f, 1f)));
        renderer.Render(CreateFrame(translucentBlue));

        ReadOnlyCollection<Vector4> pixels = renderer.GetPixels();
        for (var i = 0; i < pixels.Count; i++)
        {
            AssertColor(translucentBlue, pixels[i]);
        }
    }

    private static PixelRenderer CreateRenderer()
    {
        return new PixelRenderer(SIZE, SIZE, new StubTextEngine(), new Dictionary<string, PixelFontSDF>());
    }

    private static List<RenderCommand<PixelTexture>> CreateFrame(Vector4 color)
    {
        var ui = new UIBuilder<PixelTexture>(SIZE, SIZE, new StubTextEngine(), new UIController());

        using (ui.Element())
        {
            ui.Color = color;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(SIZE),
                Height = new Fixed(SIZE),
            };
        }

        return ui.Build(delta: 0f);
    }

    private static void AssertColor(Vector4 expected, Vector4 actual)
    {
        Assert.Equal(ToByte(expected.X), ToByte(actual.X));
        Assert.Equal(ToByte(expected.Y), ToByte(actual.Y));
        Assert.Equal(ToByte(expected.Z), ToByte(actual.Z));
        Assert.Equal(ToByte(expected.W), ToByte(actual.W));
    }

    private static byte ToByte(float value)
    {
        return (byte)(Math.Clamp(value, 0f, 1f) * 255f + 0.5f);
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
