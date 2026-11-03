using System;
using System.IO;
using System.Text.Json;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Xunit;

namespace Swordfish.Tests;

public class TypefaceAtlasLoadTests
{
    [Fact]
    public void AtlasImagesAreParsableAsTextures()
    {
        var parser = new TextureParser();
        string[] atlasFiles = Directory.GetFiles("assets/fonts", "*.bmp");
        Assert.NotEmpty(atlasFiles);

        Array.Sort(atlasFiles);
        foreach (string file in atlasFiles)
        {
            Assert.Contains(Path.GetExtension(file), parser.SupportedExtensions);
        }
    }

    [Fact]
    public void AtlasImageMatchesItsAtlasDescription()
    {
        const string path = "assets/fonts/Salmon Sans 9 Regular.bmp";
        var parser = new TextureParser();

        Texture texture = parser.Parse(new PathInfo(path));

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText("assets/fonts/Salmon Sans 9 Regular.json"));
        JsonElement atlas = document.RootElement.GetProperty("atlas");

        Assert.Equal(atlas.GetProperty("width").GetInt32(), texture.Width);
        Assert.Equal(atlas.GetProperty("height").GetInt32(), texture.Height);
        Assert.Equal(texture.Width * texture.Height * 4, texture.Pixels.Length);
    }
}
