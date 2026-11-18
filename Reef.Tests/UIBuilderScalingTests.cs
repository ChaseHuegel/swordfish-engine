using System.Numerics;
using Reef.UI;
using Xunit;

namespace Reef.Tests;

public class UIBuilderScalingTests
{
    [Fact]
    public void DefaultBuilderUsesRawPhysicalDimensions()
    {
        var ui = new UIBuilder<PixelTexture>(3840, 2160, textEngine: null!, new UIController());

        Assert.Equal(3840, ui.PhysicalWidth);
        Assert.Equal(2160, ui.PhysicalHeight);
        Assert.Equal(3840, ui.Width);
        Assert.Equal(2160, ui.Height);
        Assert.Equal(1f, ui.Scale);
        Assert.Equal(new IntVector2(100, 200), ui.MapCursorPosition(new Vector2(100f, 200f)));
    }

    [Theory]
    [InlineData(0f, 720, 1080, 1f)]
    [InlineData(0f, 1080, 1080, 1f)]
    [InlineData(0f, 1440, 1080, 1440f / 1080f)]
    [InlineData(0f, 2160, 1080, 2f)]
    [InlineData(0f, 1440, 720, 2f)]
    [InlineData(-1f, 2160, 1080, 2f)]
    [InlineData(float.NaN, 2160, 1080, 2f)]
    [InlineData(1.5f, 1080, 1080, 1.5f)]
    [InlineData(0.25f, 1080, 1080, 0.5f)]
    [InlineData(8f, 1080, 1080, 4f)]
    [InlineData(0f, 2160, 0, 1f)]
    public void ResolveScaleHandlesAutoManualAndReferenceHeight(
        float configuredScale,
        int physicalHeight,
        int referenceHeight,
        float expected
    )
    {
        float actual = UIBuilder<PixelTexture>.ResolveScale(configuredScale, physicalHeight, referenceHeight);
        Assert.Equal(expected, actual, precision: 4);
    }

    [Fact]
    public void SetScaleAndResizeUpdateLogicalCanvasAndCursorMapping()
    {
        var ui = new UIBuilder<PixelTexture>(3840, 2160, textEngine: null!, new UIController());
        ui.SetScale(configuredScale: 0f, referenceHeight: 1080);

        Assert.Equal(2f, ui.Scale, precision: 4);
        Assert.Equal(1920, ui.Width);
        Assert.Equal(1080, ui.Height);
        Assert.Equal(new IntVector2(960, 540), ui.MapCursorPosition(new Vector2(1920f, 1080f)));

        ui.Resize(1280, 720);
        Assert.Equal(1f, ui.Scale, precision: 4);
        Assert.Equal(1280, ui.Width);
        Assert.Equal(720, ui.Height);

        ui.SetScale(configuredScale: 1.5f, referenceHeight: 1080);
        Assert.Equal(1.5f, ui.Scale, precision: 4);
        Assert.Equal(853, ui.Width);
        Assert.Equal(480, ui.Height);
    }

    [Fact]
    public void MultipleBuildersKeepIndependentCanvasScales()
    {
        var primary = new UIBuilder<PixelTexture>(3840, 2160, textEngine: null!, new UIController());
        var secondary = new UIBuilder<PixelTexture>(1920, 1080, textEngine: null!, new UIController());

        primary.SetScale(0f, referenceHeight: 1080);
        secondary.SetScale(1.5f, referenceHeight: 720);

        Assert.Equal(2f, primary.Scale, precision: 4);
        Assert.Equal(1920, primary.Width);
        Assert.Equal(1080, primary.Height);

        Assert.Equal(1.5f, secondary.Scale, precision: 4);
        Assert.Equal(1280, secondary.Width);
        Assert.Equal(720, secondary.Height);
    }
}
