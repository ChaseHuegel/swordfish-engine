using System.Numerics;
using Reef.UI;

namespace Reef.Tests;

public class StyleOverrideTests
{
    [Fact]
    public void StyleOverrideKeepsExplicitZeroValues()
    {
        var style = new StyleOverride
        {
            Color = Vector4.Zero,
            Padding = new Padding(0),
            Border = new BorderStyle { Thickness = 0 },
        };

        Assert.True(style.Color.HasValue);
        Assert.Equal(Vector4.Zero, style.Color.Value);
        Assert.True(style.Padding.HasValue);
        Assert.Equal(0, style.Padding.Value.Left);
        Assert.True(style.Border.HasValue);
        Assert.Equal(0, style.Border.Value.Thickness);
        Assert.Null(style.BackgroundColor);
    }

    [Fact]
    public void ConstraintsOverrideSupportsExplicitAnchorsAndBounds()
    {
        var constraints = new ConstraintsOverride
        {
            Anchors = (Anchors)0,
            Width = new Reef.Constraints.Fixed(10),
            MinWidth = 0,
        };

        Assert.Equal((Anchors)0, constraints.Anchors);
        Assert.NotNull(constraints.Width);
        Assert.Equal(0, constraints.MinWidth);
        Assert.Null(constraints.Height);
    }

    [Fact]
    public void ReefComponentAttributeCarriesNameAndContext()
    {
        var attribute = new ReefComponentAttribute("Card", typeof(StyleOverrideTests));
        Assert.Equal("Card", attribute.Name);
        Assert.Equal(typeof(StyleOverrideTests), attribute.ContextType);
    }
}
