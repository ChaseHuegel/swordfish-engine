using System;
using System.IO;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Xunit;

namespace Swordfish.Tests;

public class TonemapperTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 1f)]
    [InlineData(0.5f, 0.214041f)]
    public void SrgbToLinearMatchesReference(float srgb, float expectedLinear)
    {
        float actual = Tonemapper.SrgbToLinear(srgb);
        Assert.Equal(expectedLinear, actual, 4);
    }

    [Fact]
    public void TextureColorSpaceDefaultsToSrgbAndAcceptsLinear()
    {
        var defaultTex = new Texture("default", new byte[4], 1, 1, mipmaps: false);
        var linearTex = new Texture("linear", new byte[4], 1, 1, mipmaps: false, ColorSpace.Linear);
        var linearArray = new TextureArray("linear_array", [linearTex], mipmaps: false, ColorSpace.Linear);
        var srgbCubemap = new TextureCubemap(
            "cubemap",
            [defaultTex, defaultTex, defaultTex, defaultTex, defaultTex, defaultTex],
            mipmaps: false
        );

        Assert.Equal(ColorSpace.Srgb, defaultTex.ColorSpace);
        Assert.Equal(ColorSpace.Linear, linearTex.ColorSpace);
        Assert.Equal(ColorSpace.Linear, linearArray.ColorSpace);
        Assert.Equal(ColorSpace.Srgb, srgbCubemap.ColorSpace);
    }

    [Theory]
    [InlineData("ui_reef_textured.glsl")]
    [InlineData("ui_default.glsl")]
    [InlineData("bloom.glsl")]
    [InlineData("bloom_extract.glsl")]
    public void GlslParserWrapsFullFragmentBodyWhenHelperFunctionsPrecedeIt(string fileName)
    {
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string shaderPath = Path.Combine(repoRoot, "Swordfish", "assets", "shaders", fileName);
        var parser = new GlslParser(NullLogger.Instance, new VirtualFileSystem());

        Shader shader = parser.Parse(new PathInfo(shaderPath));

        Assert.Equal(2, shader.Sources.Length);
        foreach (ShaderSource source in shader.Sources)
        {
            Assert.DoesNotContain("vec4 fragment()\n#endif", source.Source.Replace("\r\n", "\n"));
            Assert.DoesNotContain("vec4 vertex()\n#endif", source.Source.Replace("\r\n", "\n"));
        }
    }
}
