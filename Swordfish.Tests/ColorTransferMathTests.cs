using System;
using System.Numerics;
using Swordfish.Graphics;
using Xunit;

namespace Swordfish.Tests;

public class ColorTransferMathTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 1f)]
    [InlineData(0.5f, 0.214041f)]
    public void SrgbToLinear_MatchesReference(float srgb, float expectedLinear)
    {
        float actual = ColorTransferMath.SrgbToLinear(srgb);
        Assert.Equal(expectedLinear, actual, 4);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 1f)]
    [InlineData(0.214041f, 0.5f)]
    public void LinearToSrgb_MatchesReference(float linear, float expectedSrgb)
    {
        float actual = ColorTransferMath.LinearToSrgb(linear);
        Assert.Equal(expectedSrgb, actual, 4);
    }

    [Fact]
    public void SrgbLinear_RoundTrip_PreservesValues()
    {
        for (int i = 0; i <= 100; i++)
        {
            float srgb = i / 100f;
            float linear = ColorTransferMath.SrgbToLinear(srgb);
            float backToSrgb = ColorTransferMath.LinearToSrgb(linear);
            Assert.Equal(srgb, backToSrgb, 3);
        }
    }

    [Fact]
    public void TonemapPbrNeutral_ZeroMapsToZero()
    {
        Vector3 result = ColorTransferMath.TonemapPbrNeutral(Vector3.Zero);
        Assert.Equal(Vector3.Zero, result);
    }

    [Fact]
    public void TonemapPbrNeutral_HdrValuesMapIntoUnitRange()
    {
        Vector3 hdr = new Vector3(2f, 5f, 10f);
        Vector3 ldr = ColorTransferMath.TonemapPbrNeutral(hdr);
        Assert.True(ldr.X >= 0f && ldr.X < 1f);
        Assert.True(ldr.Y >= 0f && ldr.Y < 1f);
        Assert.True(ldr.Z >= 0f && ldr.Z < 1f);
    }

    [Fact]
    public void TonemapPbrNeutral_PreservesHueOnBrightEmissiveColors()
    {
        // Cyan: (0, 4, 4) -> G and B should remain equal, R < G
        Vector3 cyanHdr = new Vector3(0f, 4f, 4f);
        Vector3 cyanLdr = ColorTransferMath.TonemapPbrNeutral(cyanHdr);
        Assert.Equal(cyanLdr.Y, cyanLdr.Z, 5);
        Assert.True(cyanLdr.X < cyanLdr.Y);

        // Asymmetric: (0, 2, 4) -> B > G > R ordering
        Vector3 asymmetricHdr = new Vector3(0f, 2f, 4f);
        Vector3 asymmetricLdr = ColorTransferMath.TonemapPbrNeutral(asymmetricHdr);
        Assert.True(asymmetricLdr.Z > asymmetricLdr.Y);
        Assert.True(asymmetricLdr.Y > asymmetricLdr.X);
    }

    [Fact]
    public void TonemapPbrNeutral_Monotonicity_BrighterInputsProduceBrighterOutputs()
    {
        Vector3 low = new Vector3(0.1f, 0.2f, 0.3f);
        Vector3 high = new Vector3(1.0f, 2.0f, 3.0f);
        Vector3 lLow = ColorTransferMath.TonemapPbrNeutral(low);
        Vector3 lHigh = ColorTransferMath.TonemapPbrNeutral(high);

        Assert.True(lHigh.X > lLow.X);
        Assert.True(lHigh.Y > lLow.Y);
        Assert.True(lHigh.Z > lLow.Z);
    }

    [Fact]
    public void Texture_ColorSpace_DefaultsToSrgbAndAcceptsLinear()
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
}
