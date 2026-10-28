using System.Collections.ObjectModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using Reef;
using Reef.MSDF;
using Reef.Text;
using Reef.UI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Reef.Markup.Tests;

public class VisualGalleryTests
{
    private const int WIDTH = 1100;
    private const int HEIGHT = 1200;

    [Fact]
    public void GalleryNormalRendersAndMatchesGolden()
    {
        GalleryState state = CreateState();
        using var harness = new GalleryHarness();
        List<RenderCommand<PixelTexture>> commands = harness.Render(new GalleryView(), state, out ReadOnlyCollection<Vector4> pixels);

        Assert.Contains(commands, command => command.Text == "Reef Markup Style Gallery");
        Assert.Contains(commands, command => command.Text == "Ada");
        Assert.Contains(commands, command => command.Text == "Score: 99");
        Assert.Contains(commands, command => command.Text == "Gallery Dialog");

        RenderCommand<PixelTexture> red = commands.Single(command => command.ID == "gallery_red");
        AssertColor(new Vector4(1f, 0f, 0f, 1f), Pixel(pixels, red.Rect.Left + 4, red.Rect.Top + 4));

        RenderCommand<PixelTexture> clip = commands.Single(command => command.ID == "clip_parent");
        Assert.NotEqual(new Vector4(0.9019608f, 0.49411765f, 0.13333334f, 1f), Pixel(pixels, clip.Rect.Left + 220, clip.Rect.Top + 20));

        CompareGolden("gallery-normal.png", pixels);
    }

    [Fact]
    public void GalleryHoverFrameMatchesGolden()
    {
        GalleryState state = CreateState();
        using var harness = new GalleryHarness();
        harness.Warmup(new GalleryView(), state);
        ElementBounds bounds = harness.Ui.GetElementBounds("hover");
        int x = bounds.Rect.Left + 4;
        int y = bounds.Rect.Top + 4;
        harness.Controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        harness.Render(new GalleryView(), state, out ReadOnlyCollection<Vector4> pixels);
        harness.Controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        harness.Render(new GalleryView(), state, out pixels);
        CompareGolden("gallery-hover.png", pixels);
    }

    [Fact]
    public void GalleryDisabledFrameMatchesGolden()
    {
        GalleryState state = CreateState();
        state.Disabled = true;
        using var harness = new GalleryHarness();
        harness.Render(new GalleryView(), state, out ReadOnlyCollection<Vector4> pixels);
        CompareGolden("gallery-disabled.png", pixels);
    }

    private static GalleryState CreateState()
    {
        return new GalleryState
        {
            Texture = GalleryHarness.LoadPixelTexture("TestFiles/Images/swordfish.png"),
        };
    }

    private static void CompareGolden(string name, ReadOnlyCollection<Vector4> pixels)
    {
        string goldenPath = Path.Combine(SourceDirectory(), "TestFiles", "Golden", name);
        string outputDirectory = Path.Combine(AppContext.BaseDirectory, "TestResults", "reef-markup");
        Directory.CreateDirectory(outputDirectory);
        string actualPath = Path.Combine(outputDirectory, name);
        SavePixels(actualPath, WIDTH, HEIGHT, pixels);

        bool update = string.Equals(Environment.GetEnvironmentVariable("REEF_MARKUP_UPDATE_GOLDEN"), "1", StringComparison.Ordinal);
        if (update || !File.Exists(goldenPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.Copy(actualPath, goldenPath, overwrite: true);
            return;
        }

        using Image<Rgba32> expected = Image.Load<Rgba32>(goldenPath);
        var mismatches = 0;
        var firstX = -1;
        var firstY = -1;
        for (var y = 0; y < HEIGHT; y++)
        {
            if (y >= expected.Height)
            {
                mismatches += WIDTH;
                continue;
            }

            for (var x = 0; x < WIDTH; x++)
            {
                if (x >= expected.Width)
                {
                    mismatches++;
                    continue;
                }

                Rgba32 expectedColor = expected[x, y];
                Vector4 actualColor = pixels[y * WIDTH + x];
                if (expectedColor.R != ToByte(actualColor.X)
                    || expectedColor.G != ToByte(actualColor.Y)
                    || expectedColor.B != ToByte(actualColor.Z)
                    || expectedColor.A != ToByte(actualColor.W))
                {
                    mismatches++;
                    if (firstX < 0)
                    {
                        firstX = x;
                        firstY = y;
                    }
                }
            }
        }

        Assert.True(
            mismatches == 0,
            "Golden image mismatch for " + name + ": " + mismatches + " pixels. First at "
            + firstX + "," + firstY
            + ". Actual: " + actualPath + ". Golden: " + goldenPath);
    }

    private static string SourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }

    private static Vector4 Pixel(ReadOnlyCollection<Vector4> pixels, int x, int y)
    {
        return pixels[y * WIDTH + x];
    }

    private static void AssertColor(Vector4 expected, Vector4 actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
        Assert.Equal(expected.Z, actual.Z, 3);
        Assert.Equal(expected.W, actual.W, 3);
    }

    private static Vector4 ToVector(Rgba32 color)
    {
        return new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }

    private static byte ToByte(float value)
    {
        return (byte)(Math.Clamp(value, 0f, 1f) * 255f);
    }

    private static void SavePixels(string path, int width, int height, ReadOnlyCollection<Vector4> pixels)
    {
        using var image = new Image<Rgba32>(width, height);
        for (var i = 0; i < pixels.Count; i++)
        {
            int x = i % width;
            int y = i / width;
            Vector4 pixel = pixels[i];
            image[x, y] = new Rgba32(
                (byte)(pixel.X * 255),
                (byte)(pixel.Y * 255),
                (byte)(pixel.Z * 255),
                (byte)(pixel.W * 255));
        }

        image.Save(path);
    }

    private sealed class GalleryHarness : IDisposable
    {
        public readonly TextEngine TextEngine;
        public readonly Dictionary<string, PixelFontSDF> Fonts;
        public readonly UIController Controller = new();
        public readonly UIBuilder<PixelTexture> Ui;

        public GalleryHarness()
        {
            var salmonFont = new FontInfo("salmon-mono-9-regular", "TestFiles/Fonts/Salmon Mono 9 Regular.ttf");
            var awesomeFont = new FontInfo("fa-6-free-solid", "TestFiles/Fonts/Font Awesome 6 Free Solid.otf");
            TextEngine = new TextEngine([salmonFont, awesomeFont]);

            TextEngine.TryGetTypeface(salmonFont, out ITypeface? typeface);
            AtlasInfo salmonAtlas = typeface!.GetAtlasInfo();
            PixelTexture salmonTexture = LoadPixelTexture(salmonAtlas.Path);

            TextEngine.TryGetTypeface(awesomeFont, out typeface);
            AtlasInfo awesomeAtlas = typeface!.GetAtlasInfo();
            PixelTexture awesomeTexture = LoadPixelTexture(awesomeAtlas.Path);

            Fonts = new Dictionary<string, PixelFontSDF>
            {
                [salmonFont.ID] = new PixelFontSDF(salmonTexture, 0.02f),
                [awesomeFont.ID] = new PixelFontSDF(awesomeTexture, 1f),
            };

            Ui = new UIBuilder<PixelTexture>(WIDTH, HEIGHT, TextEngine, Controller);
        }

        public List<RenderCommand<PixelTexture>> Render(
            GalleryView view,
            GalleryState state,
            out ReadOnlyCollection<Vector4> pixels)
        {
            view.Render(Ui, state, null, null, null);
            List<RenderCommand<PixelTexture>> commands = Ui.Build(0d);
            var renderer = new PixelRenderer(WIDTH, HEIGHT, TextEngine, Fonts);
            renderer.Render(commands);
            pixels = renderer.GetPixels();
            return commands;
        }

        public void Warmup(GalleryView view, GalleryState state)
        {
            view.Render(Ui, state, null, null, null);
            Ui.Build(0d);
        }

        public static PixelTexture LoadPixelTexture(string path)
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(path);
            var texture = new PixelTexture(image.Width, image.Height);
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                Rgba32 color = image[x, y];
                texture.SetPixel(x, y, new Vector4(
                    color.R / 255f,
                    color.G / 255f,
                    color.B / 255f,
                    color.A / 255f));
            }

            return texture;
        }

        public void Dispose()
        {
        }
    }
}
