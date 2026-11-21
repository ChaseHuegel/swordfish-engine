using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;

namespace Swordfish.Tests;

public class OffscreenRendererTests
{
    [Fact]
    public void FlipRowsVertical_ReversesRowOrderPreservingPixelsWithinEachRow()
    {
        byte[] input =
        [
            1, 2, 3, 4,   5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24,
        ];

        byte[] flipped = GLOffscreenRenderer.FlipRowsVertical(input, width: 2, height: 3, bytesPerPixel: 4);

        Assert.Equal(
            [
                17, 18, 19, 20, 21, 22, 23, 24,
                9, 10, 11, 12, 13, 14, 15, 16,
                1, 2, 3, 4,   5, 6, 7, 8,
            ],
            flipped
        );
    }

    [Fact]
    public void Texture_DefaultsToNearestFilterAndPreservesExplicitLinearFilter()
    {
        var defaultTex = new Texture("default", new byte[4], 1, 1, mipmaps: false);
        var linearTex = new Texture("linear", new byte[4], 1, 1, mipmaps: true, ColorSpace.Srgb, TextureFilter.Linear);

        Assert.Equal(TextureFilter.Nearest, defaultTex.Filter);
        Assert.Equal(TextureFilter.Linear, linearTex.Filter);
        Assert.True(linearTex.Mipmaps);
    }

    [Fact]
    public void OffscreenRenderRequest_AppliesExpectedDefaults()
    {
        var mesh = new Mesh([], [], [], [], []);
        var shader = new Shader("test", []);
        var material = new Material(shader);

        var request = new OffscreenRenderRequest(
            "icon",
            mesh,
            material,
            RenderOptions.Default,
            Matrix4x4.Identity,
            Matrix4x4.Identity,
            Matrix4x4.Identity,
            128,
            128
        );

        Assert.Equal(4, request.Samples);
        Assert.True(request.Mipmaps);
        Assert.Equal(TextureFilter.Linear, request.Filter);
        Assert.Equal(ColorSpace.Srgb, request.ColorSpace);
        Assert.Equal(Vector4.Zero, request.ClearColor);
    }
}
