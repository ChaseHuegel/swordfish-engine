using System.Collections.ObjectModel;
using System.Numerics;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;

namespace Reef.Tests;

public class UIBuilderZOrderTests
{
    private const int WIDTH = 10;
    private const int HEIGHT = 10;

    [Fact]
    public void LaterSiblingPaintsOnTop()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder();
        var red = new Vector4(1f, 0f, 0f, 1f);
        var blue = new Vector4(0f, 0f, 1f, 1f);

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = new Vector4(0f, 0f, 0f, 1f);
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(WIDTH),
                Height = new Fixed(HEIGHT),
            };

            using (ui.Element())
            {
                ui.Color = red;
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(WIDTH),
                    Height = new Fixed(HEIGHT),
                };
            }

            using (ui.Element())
            {
                ui.Color = blue;
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(WIDTH),
                    Height = new Fixed(HEIGHT),
                };
            }
        }

        ReadOnlyCollection<Vector4> pixels = Render(ui);

        AssertColor(blue, GetPixel(pixels, 5, 5));
    }

    [Fact]
    public void ParentBackgroundPaintsBehindChildren()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder();
        var black = new Vector4(0f, 0f, 0f, 1f);
        var red = new Vector4(1f, 0f, 0f, 1f);

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = black;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(WIDTH),
                Height = new Fixed(HEIGHT),
            };

            using (ui.Element())
            {
                ui.Color = red;
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(5),
                    Height = new Fixed(5),
                };
            }
        }

        ReadOnlyCollection<Vector4> pixels = Render(ui);

        AssertColor(red, GetPixel(pixels, 2, 2));
        AssertColor(black, GetPixel(pixels, 8, 8));
    }

    [Fact]
    public void LaterRootPaintsOnTop()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder();
        var red = new Vector4(1f, 0f, 0f, 1f);
        var blue = new Vector4(0f, 0f, 1f, 1f);

        using (ui.Element())
        {
            ui.Color = red;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(WIDTH),
                Height = new Fixed(HEIGHT),
            };
        }

        using (ui.Element())
        {
            ui.Color = blue;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(WIDTH),
                Height = new Fixed(HEIGHT),
            };
        }

        ReadOnlyCollection<Vector4> pixels = Render(ui);

        AssertColor(blue, GetPixel(pixels, 5, 5));
    }

    [Fact]
    public void LaterSiblingReceivesInteractionFirst()
    {
        var controller = new UIController();
        var ui = CreateBuilder(controller);
        var clickedA = false;
        var clickedB = false;

        controller.UpdateMouse(5, 5, UIController.MouseButtons.None);
        RenderInteractionChildren(ui, ref clickedA, ref clickedB);
        ui.Build(delta: 0f);

        controller.UpdateMouse(5, 5, UIController.MouseButtons.Left);
        RenderInteractionChildren(ui, ref clickedA, ref clickedB);
        ui.Build(delta: 0.1f);

        controller.UpdateMouse(5, 5, UIController.MouseButtons.None);
        RenderInteractionChildren(ui, ref clickedA, ref clickedB);
        ui.Build(delta: 0.1f);

        Assert.False(clickedA);
        Assert.True(clickedB);
    }

    private static void RenderInteractionChildren(UIBuilder<PixelTexture> ui, ref bool clickedA, ref bool clickedB)
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(WIDTH),
                Height = new Fixed(HEIGHT),
            };

            using (ui.Element("a"))
            {
                ui.Color = new Vector4(1f, 0f, 0f, 1f);
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(WIDTH),
                    Height = new Fixed(HEIGHT),
                };

                clickedA = ui.Clicked();
            }

            using (ui.Element("b"))
            {
                ui.Color = new Vector4(0f, 0f, 1f, 1f);
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(WIDTH),
                    Height = new Fixed(HEIGHT),
                };

                clickedB = ui.Clicked();
            }
        }
    }

    private static UIBuilder<PixelTexture> CreateBuilder()
    {
        return CreateBuilder(new UIController());
    }

    private static UIBuilder<PixelTexture> CreateBuilder(UIController controller)
    {
        return new UIBuilder<PixelTexture>(WIDTH, HEIGHT, new StubTextEngine(), controller);
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
