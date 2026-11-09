using Swordfish.Input;
using Xunit;

namespace Swordfish.Tests;

public class ScrollAccumulatorTests
{
    [Fact]
    public void NoInputLatchesZero()
    {
        var scroll = new ScrollAccumulator();

        scroll.Latch();

        Assert.Equal(0f, scroll.Value);
    }

    [Fact]
    public void DeltasBetweenFramesAccumulate()
    {
        var scroll = new ScrollAccumulator();

        scroll.Add(0.1f);
        scroll.Add(0.2f);
        scroll.Add(0.3f);
        scroll.Latch();

        Assert.Equal(0.6f, scroll.Value, precision: 5);
    }

    [Fact]
    public void LatchedValueHoldsForTheWholeFrame()
    {
        var scroll = new ScrollAccumulator();

        scroll.Add(1f);
        scroll.Latch();

        Assert.Equal(1f, scroll.Value);
        Assert.Equal(1f, scroll.Value);
    }

    [Fact]
    public void DeltasAfterLatchWaitForTheNextFrame()
    {
        var scroll = new ScrollAccumulator();

        scroll.Add(1f);
        scroll.Latch();
        scroll.Add(2f);

        Assert.Equal(1f, scroll.Value);

        scroll.Latch();

        Assert.Equal(2f, scroll.Value);
    }

    [Fact]
    public void OppositeDeltasCancel()
    {
        var scroll = new ScrollAccumulator();

        scroll.Add(1f);
        scroll.Add(-1f);
        scroll.Latch();

        Assert.Equal(0f, scroll.Value);
    }
}
