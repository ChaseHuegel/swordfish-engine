#nullable enable
using System.IO;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Graphics;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Tests tint-mask layer alignment and generated fallback masks for brick texture arrays.</summary>
public class PBRTextureArraysParserTests
{
    private sealed class StubTextureParser : IFileParser<Texture>
    {
        public string[] SupportedExtensions => [".png"];

        public Texture Parse(PathInfo file)
        {
            string name = file.GetFileNameWithoutExtension();
            byte value = name.EndsWith(".t") ? (byte)128 : (byte)255;
            return new Texture(name, [value, value, value, 255], 1, 1, mipmaps: false);
        }

        object IFileParser.Parse(PathInfo file) => Parse(file);
    }

    [Fact]
    public void TintMaskArrayAlignsWithAlbedoAndFillsMissingMasksWhite()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string blockDir = Path.Combine(tempDir, "block");
        Directory.CreateDirectory(blockDir);

        try
        {
            File.WriteAllText(Path.Combine(blockDir, "core.png"), string.Empty);
            File.WriteAllText(Path.Combine(blockDir, "core.e.png"), string.Empty);
            File.WriteAllText(Path.Combine(blockDir, "core.t.png"), string.Empty);
            File.WriteAllText(Path.Combine(blockDir, "caution.png"), string.Empty);
            File.WriteAllText(Path.Combine(blockDir, "stray.t.png"), string.Empty);

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(tempDir));
            var parser = new PBRTextureArraysParser(vfs, new StubTextureParser());

            PBRTextureArrays arrays = parser.Parse(new PathInfo("block/"));

            Assert.Equal(arrays.Albedo.Depth, arrays.TintMask.Depth);
            Assert.Equal(arrays.Albedo.IndexOf("core"), arrays.TintMask.IndexOf("core.t"));
            Assert.Equal(arrays.Albedo.IndexOf("caution"), arrays.TintMask.IndexOf("caution.t"));

            Assert.Equal(128, LayerRed(arrays.TintMask, arrays.TintMask.IndexOf("core.t")));
            Assert.Equal(255, LayerRed(arrays.TintMask, arrays.TintMask.IndexOf("caution.t")));
            Assert.Equal(255, LayerRed(arrays.TintMask, 0));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static byte LayerRed(TextureArray array, int layer)
    {
        int layerSize = array.Width * array.Height * 4;
        return array.Pixels[(layer * layerSize) + 0];
    }
}
