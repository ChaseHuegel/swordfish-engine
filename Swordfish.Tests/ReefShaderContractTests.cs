using System.IO;
using Xunit;

namespace Swordfish.Tests;

public class ReefShaderContractTests
{
    [Theory]
    [InlineData("assets/shaders/ui_reef_atlas.glsl")]
    [InlineData("assets/shaders/ui_reef_textured.glsl")]
    public void ReefUiShadersExpandAQuadInstance(string path)
    {
        string source = File.ReadAllText(path);

        //  ReefRenderer binds one instance record per quad and enables no per-vertex
        //  position or uv attribute, so a shader that reads them draws nothing.
        Assert.DoesNotContain("VertexPosition = in_position", source);
        Assert.DoesNotContain("TextureCoord = in_uv;", source);

        Assert.Contains("layout (location = 8) in vec4 in_rect;", source);
        Assert.Contains("layout (location = 9) in vec4 in_uvRect;", source);
        Assert.Contains("gl_VertexID", source);
    }
}
