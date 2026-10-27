using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Numerics;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;

namespace Reef.Tests;

public class BorderDashTests
{
    private const int WIDTH = 64;
    private const int HEIGHT = 64;

    [Fact]
    public void SegmentsFollowTheLegacyDividerPitch()
    {
        var segments = new DashSegments(264, dash: 2, gap: 4);

        var offsets = new List<int>();
        while (segments.MoveNext())
        {
            Assert.Equal(2, segments.SegmentLength);
            offsets.Add(segments.Offset);
        }

        Assert.Equal(44, offsets.Count);
        Assert.Equal(0, offsets[0]);
        Assert.Equal(6, offsets[1]);
        Assert.Equal(258, offsets[^1]);
    }

    [Fact]
    public void SegmentsTruncateTheLastDash()
    {
        var segments = new DashSegments(7, dash: 2, gap: 4);

        var lengths = new List<int>();
        while (segments.MoveNext())
        {
            lengths.Add(segments.SegmentLength);
        }

        Assert.Equal([2, 1], lengths);
    }

    [Fact]
    public void CountUsesTheDashPitch()
    {
        Assert.Equal(0, BorderDash.Count(264, dash: 0, gap: 4));
        Assert.Equal(1, BorderDash.Count(1, dash: 2, gap: 4));
        Assert.Equal(44, BorderDash.Count(264, dash: 2, gap: 4));
        Assert.Equal(50, BorderDash.Count(300, dash: 2, gap: 4));
    }

    [Fact]
    public void ExplicitPatternWinsOverLineStyle()
    {
        var border = new BorderStyle
        {
            LineStyle = BorderLineStyle.Dotted,
            Dash = 2,
            Gap = 4,
        };

        BorderDash.Resolve(border, thickness: 3, out int dash, out int gap);

        Assert.Equal(2, dash);
        Assert.Equal(4, gap);
    }

    [Fact]
    public void LineStyleDerivesThePatternFromThickness()
    {
        BorderDash.Resolve(new BorderStyle { LineStyle = BorderLineStyle.Dashed }, thickness: 1, out int dashed, out int dashedGap);
        BorderDash.Resolve(new BorderStyle { LineStyle = BorderLineStyle.Dotted }, thickness: 2, out int dotted, out int dottedGap);
        BorderDash.Resolve(new BorderStyle { LineStyle = BorderLineStyle.Solid }, thickness: 2, out int solid, out int solidGap);

        Assert.Equal(2, dashed);
        Assert.Equal(2, dashedGap);
        Assert.Equal(2, dotted);
        Assert.Equal(2, dottedGap);
        Assert.Equal(0, solid);
        Assert.Equal(0, solidGap);
    }

    [Fact]
    public void DashedBorderDrawsOnAOnePixelTallSide()
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
                Height = new Fixed(1),
            };
            ui.Border = new BorderStyle
            {
                BottomThickness = 1,
                BottomColor = border,
                Dash = 2,
                Gap = 4,
            };
        }

        ReadOnlyCollection<Vector4> pixels = Render(ui);

        AssertColor(border, GetPixel(pixels, 0, 0));
        AssertColor(border, GetPixel(pixels, 1, 0));
        AssertColor(background, GetPixel(pixels, 2, 0));
        AssertColor(background, GetPixel(pixels, 5, 0));
        AssertColor(border, GetPixel(pixels, 6, 0));
        AssertColor(border, GetPixel(pixels, 7, 0));
    }

    [Fact]
    public void SolidBottomBorderUsesTheFullHeightOfAThinElement()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder();
        var border = new Vector4(1f, 0f, 0f, 1f);

        using (ui.Element())
        {
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(WIDTH),
                Height = new Fixed(1),
            };
            ui.Border = new BorderStyle
            {
                BottomThickness = 1,
                BottomColor = border,
            };
        }

        ReadOnlyCollection<Vector4> pixels = Render(ui);

        AssertColor(border, GetPixel(pixels, 0, 0));
        AssertColor(border, GetPixel(pixels, WIDTH - 1, 0));
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
