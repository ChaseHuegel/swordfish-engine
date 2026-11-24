using System.Collections.ObjectModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using Reef;
using Reef.MSDF;
using Reef.Text;
using Reef.UI;
using StbImageNext;
using StbImageNext.Encoders;

namespace Reef.Markup.Tests;

public class VisualGalleryTests(VisualGalleryTests.GalleryAssetsFixture assets) : IClassFixture<VisualGalleryTests.GalleryAssetsFixture>
{
    private const int WIDTH = 1100;
    private const int HEIGHT = 1200;

    private readonly GalleryAssetsFixture _assets = assets;

    [Fact]
    public void GalleryNormalRendersAndMatchesGolden()
    {
        GalleryState state = CreateState();
        using var harness = new GalleryHarness(_assets);
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
        using var harness = new GalleryHarness(_assets);
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
        using var harness = new GalleryHarness(_assets);
        harness.Render(new GalleryView(), state, out ReadOnlyCollection<Vector4> pixels);
        CompareGolden("gallery-disabled.png", pixels);
    }

    private GalleryState CreateState()
    {
        return new GalleryState
        {
            Texture = _assets.SwordfishTexture,
        };
    }

    private static void CompareGolden(string name, ReadOnlyCollection<Vector4> pixels)
    {
        string goldenPath = Path.Combine(SourceDirectory(), "TestFiles", "Golden", name);
        string outputDirectory = Path.Combine(AppContext.BaseDirectory, "TestResults", "reef-markup");
        string actualPath = Path.Combine(outputDirectory, name);

        bool update = string.Equals(Environment.GetEnvironmentVariable("REEF_MARKUP_UPDATE_GOLDEN"), "1", StringComparison.Ordinal);
        if (update || !File.Exists(goldenPath))
        {
            Directory.CreateDirectory(outputDirectory);
            SavePixels(actualPath, WIDTH, HEIGHT, pixels);
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.Copy(actualPath, goldenPath, overwrite: true);
            return;
        }

        ImageResult expected;
        using (FileStream stream = File.OpenRead(goldenPath))
        {
            expected = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        }

        var mismatches = 0;
        var firstX = -1;
        var firstY = -1;
        byte[] expectedData = expected.Data;
        for (var y = 0; y < HEIGHT; y++)
        {
            if (y >= expected.Height)
            {
                mismatches += WIDTH;
                continue;
            }

            int expectedRowOffset = y * expected.Width * 4;
            int rowOffset = y * WIDTH;
            for (var x = 0; x < WIDTH; x++)
            {
                if (x >= expected.Width)
                {
                    mismatches++;
                    continue;
                }

                int offset = expectedRowOffset + x * 4;
                Vector4 actualColor = pixels[rowOffset + x];
                if (expectedData[offset] != ToByte(actualColor.X)
                    || expectedData[offset + 1] != ToByte(actualColor.Y)
                    || expectedData[offset + 2] != ToByte(actualColor.Z)
                    || expectedData[offset + 3] != ToByte(actualColor.W))
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

        if (mismatches > 0)
        {
            Directory.CreateDirectory(outputDirectory);
            SavePixels(actualPath, WIDTH, HEIGHT, pixels);
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
        Assert.Equal(ToByte(expected.X), ToByte(actual.X));
        Assert.Equal(ToByte(expected.Y), ToByte(actual.Y));
        Assert.Equal(ToByte(expected.Z), ToByte(actual.Z));
        Assert.Equal(ToByte(expected.W), ToByte(actual.W));
    }

    private static byte ToByte(float value)
    {
        return (byte)(Math.Clamp(value, 0f, 1f) * 255f + 0.5f);
    }

    private static void SavePixels(string path, int width, int height, ReadOnlyCollection<Vector4> pixels)
    {
        var data = new byte[width * height * 4];
        for (var i = 0; i < pixels.Count; i++)
        {
            Vector4 pixel = pixels[i];
            int offset = i * 4;
            data[offset] = ToByte(pixel.X);
            data[offset + 1] = ToByte(pixel.Y);
            data[offset + 2] = ToByte(pixel.Z);
            data[offset + 3] = ToByte(pixel.W);
        }

        using FileStream stream = File.Create(path);
        PngEncoder.EncodeToStream(stream, data, width, height, ColorComponents.RedGreenBlueAlpha);
    }

    /// <summary>Loads the immutable font atlases and textures shared across gallery frames.</summary>
    public sealed class GalleryAssetsFixture
    {
        public readonly TextEngine TextEngine;
        public readonly Dictionary<string, PixelFontSDF> Fonts;
        public readonly PixelTexture SwordfishTexture;

        public GalleryAssetsFixture()
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

            SwordfishTexture = LoadPixelTexture("TestFiles/Images/swordfish.png");
        }

        private static PixelTexture LoadPixelTexture(string path)
        {
            using FileStream stream = File.OpenRead(path);
            ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            var texture = new PixelTexture(image.Width, image.Height);
            byte[] data = image.Data;
            for (var y = 0; y < image.Height; y++)
            {
                int rowOffset = y * image.Width * 4;
                for (var x = 0; x < image.Width; x++)
                {
                    int offset = rowOffset + x * 4;
                    texture.SetPixel(x, y, new Vector4(
                        data[offset] / 255f,
                        data[offset + 1] / 255f,
                        data[offset + 2] / 255f,
                        data[offset + 3] / 255f));
                }
            }

            return texture;
        }
    }

    private sealed class GalleryHarness : IDisposable
    {
        public readonly TextEngine TextEngine;
        public readonly Dictionary<string, PixelFontSDF> Fonts;
        public readonly UIController Controller = new();
        public readonly UIBuilder<PixelTexture> Ui;

        public GalleryHarness(GalleryAssetsFixture assets)
        {
            TextEngine = assets.TextEngine;
            Fonts = assets.Fonts;
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

        public void Dispose()
        {
        }
    }
}
