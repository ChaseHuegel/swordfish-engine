#nullable enable
using System;
using System.Numerics;
using WaywardBeyond.Client.UI.Layers;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Tests concentric multi-ring radial layout, overlap prevention, and canvas clamping.</summary>
public class RadialMenuLayoutTests
{
    [Theory]
    [InlineData(4, 88)]
    [InlineData(6, 72)]
    public void SmallMenusStayOnSingleInnerRingWithChordClampedHitSize(int count, int expectedHitSize)
    {
        var options = new RadialMenuLayoutOptions();
        RadialMenuLayoutResult layout = RadialMenuLayout.Compute(count, canvasWidth: 1280, canvasHeight: 720, options);

        Assert.Equal(count, layout.Positions.Length);
        Assert.Equal(1, layout.RingCount);
        Assert.Equal(expectedHitSize, layout.HitSize);

        for (var i = 0; i < layout.Positions.Length; i++)
        {
            Assert.Equal(options.InnerRadius, layout.Positions[i].Length(), precision: 2);
        }
    }

    [Theory]
    [InlineData(25)]
    [InlineData(32)]
    public void HighCountMenusExpandIntoNonOverlappingConcentricRingsInside720pCanvas(int count)
    {
        var options = new RadialMenuLayoutOptions();
        RadialMenuLayoutResult layout = RadialMenuLayout.Compute(count, canvasWidth: 1280, canvasHeight: 720, options);

        Assert.Equal(count, layout.Positions.Length);
        Assert.True(layout.RingCount >= 2, $"Expected multiple rings for {count} items.");
        Assert.True(layout.HitSize >= (int)options.NodeSize && layout.HitSize <= options.DefaultHitSize);

        float maxAllowedRadius = 720f * 0.5f - options.NodeSize * 0.5f - options.CanvasMargin;
        for (var i = 0; i < layout.Positions.Length; i++)
        {
            Assert.True(layout.Positions[i].Length() <= maxAllowedRadius + 0.01f, $"Node {i} exceeded canvas radius.");

            for (var j = i + 1; j < layout.Positions.Length; j++)
            {
                Vector2 delta = layout.Positions[i] - layout.Positions[j];
                float dist = delta.Length();
                Assert.True(dist >= options.NodePitch - 0.5f, $"Nodes {i} and {j} too close ({dist:F1} < {options.NodePitch:F1}).");
                bool separatedAabb = MathF.Abs(delta.X) >= options.NodeSize + options.MinGap - 0.5f
                    || MathF.Abs(delta.Y) >= options.NodeSize + options.MinGap - 0.5f;
                Assert.True(separatedAabb, $"Square nodes {i} and {j} overlapped visually (dx={delta.X:F1}, dy={delta.Y:F1}).");
            }
        }
    }

    [Fact]
    public void ExplicitMaxRadiusAndExtremeOverflowClampInsideBoundsWithoutClipping()
    {
        var options = RadialMenuLayoutOptions.Default with { MaxRadius = 180f };
        RadialMenuLayoutResult layout = RadialMenuLayout.Compute(count: 100, canvasWidth: 1280, canvasHeight: 720, options);

        Assert.Equal(100, layout.Positions.Length);
        for (var i = 0; i < layout.Positions.Length; i++)
        {
            Assert.True(layout.Positions[i].Length() <= 180.01f, $"Node {i} exceeded explicit MaxRadius.");
        }

        //  Implicit canvas clamp when no explicit MaxRadius is given on a small 480p canvas.
        var implicitOptions = new RadialMenuLayoutOptions();
        RadialMenuLayoutResult smallCanvas = RadialMenuLayout.Compute(count: 100, canvasWidth: 640, canvasHeight: 480, implicitOptions);
        float maxImplicit = 480f * 0.5f - implicitOptions.NodeSize * 0.5f - implicitOptions.CanvasMargin;

        Assert.Equal(100, smallCanvas.Positions.Length);
        for (var i = 0; i < smallCanvas.Positions.Length; i++)
        {
            Assert.True(smallCanvas.Positions[i].Length() <= maxImplicit + 0.01f, $"Node {i} exceeded implicit canvas radius.");
        }
    }
}
