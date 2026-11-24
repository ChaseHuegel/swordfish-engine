using System.IO;
using System.IO.Compression;
using Silk.NET.Core;
using StbImageNext;
using StbImageNext.Encoders;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Util;
using Xunit;

namespace Swordfish.Tests;

public class TextureParserTests
{
    private static byte[] CreateTwoRowPng()
    {
        // Row 0 (top): red; Row 1 (bottom): blue
        byte[] rgba =
        [
            255, 0, 0, 255,
            0, 0, 255, 255,
        ];
        return PngEncoder.EncodeToByteArray(rgba, width: 1, height: 2, ColorComponents.RedGreenBlueAlpha);
    }

    [Fact]
    public void Parse_DecodesPngAndFlipsVertically()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
        try
        {
            File.WriteAllBytes(tempFile, CreateTwoRowPng());

            var parser = new TextureParser();
            Texture texture = parser.Parse(new PathInfo(tempFile));

            Assert.Equal(1, texture.Width);
            Assert.Equal(2, texture.Height);
            Assert.Equal(new byte[] { 0, 0, 255, 255, 255, 0, 0, 255 }, texture.Pixels);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_DecodesPngFromNonSeekableZipEntryStream()
    {
        string tempZip = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
        try
        {
            using (ZipArchive archive = ZipFile.Open(tempZip, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("textures/test.png");
                using Stream entryStream = entry.Open();
                byte[] png = CreateTwoRowPng();
                entryStream.Write(png, 0, png.Length);
            }

            var parser = new TextureParser();
            Texture texture = parser.Parse(new PathInfo($"zip://{tempZip}/textures/test.png"));

            Assert.Equal("test", texture.Name);
            Assert.Equal(1, texture.Width);
            Assert.Equal(2, texture.Height);
            Assert.Equal(new byte[] { 0, 0, 255, 255, 255, 0, 0, 255 }, texture.Pixels);
        }
        finally
        {
            File.Delete(tempZip);
        }
    }

    [Fact]
    public void ImagingLoad_DecodesBufferAndPathWithoutFlip()
    {
        byte[] png = CreateTwoRowPng();
        RawImage fromBuffer = Imaging.Load(png);
        Assert.Equal(1, fromBuffer.Width);
        Assert.Equal(2, fromBuffer.Height);
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 0, 255, 255 }, fromBuffer.Pixels.ToArray());

        string tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
        try
        {
            File.WriteAllBytes(tempFile, png);
            RawImage fromPath = Imaging.Load(new PathInfo(tempFile));
            Assert.Equal(1, fromPath.Width);
            Assert.Equal(2, fromPath.Height);
            Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 0, 255, 255 }, fromPath.Pixels.ToArray());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
