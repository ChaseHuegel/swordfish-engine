#nullable enable
using WaywardBeyond.Client.Systems;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Pins the hold-to-repeat timing for an interaction button.</summary>
public class InteractionRepeatTimerTests
{
    private const float INTERVAL = 0.5f;

    [Fact]
    public void DoesNotRepeatBeforeAPress()
    {
        var timer = new InteractionRepeatTimer(INTERVAL);

        Assert.False(timer.Advance(10f, true), "An unpressed timer must never repeat.");
    }

    [Fact]
    public void RepeatsOncePerInterval()
    {
        var timer = new InteractionRepeatTimer(INTERVAL);
        timer.Press();

        Assert.False(timer.Advance(0.25f, true));
        Assert.True(timer.Advance(0.25f, true), "The first interval must repeat.");
        Assert.False(timer.Advance(0.25f, true));
        Assert.True(timer.Advance(0.25f, true), "Each further interval must repeat.");
    }

    [Fact]
    public void CarriesTheRemainderIntoTheNextInterval()
    {
        var timer = new InteractionRepeatTimer(INTERVAL);
        timer.Press();

        Assert.True(timer.Advance(0.75f, true), "A long tick must repeat.");
        Assert.True(timer.Advance(0.25f, true), "The unused time must count toward the next repeat.");
        Assert.False(timer.Advance(0.1f, true));
    }

    [Fact]
    public void StoppingEligibilityStopsTheRepeat()
    {
        var timer = new InteractionRepeatTimer(INTERVAL);
        timer.Press();

        Assert.False(timer.Advance(0.25f, true));
        Assert.False(timer.Advance(0.25f, false), "A release or blocker must not repeat.");
        Assert.False(timer.Advance(1f, true), "A stopped timer must not resume without a press.");
    }

    [Fact]
    public void PressRestartsTheInterval()
    {
        var timer = new InteractionRepeatTimer(INTERVAL);
        timer.Press();

        Assert.False(timer.Advance(0.4f, true));
        timer.Press();
        Assert.False(timer.Advance(0.25f, true), "A new press must restart the interval.");
        Assert.True(timer.Advance(0.25f, true));
    }
}
