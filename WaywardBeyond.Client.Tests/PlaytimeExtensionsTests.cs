using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Tests;

[TestFixture]
public class PlaytimeExtensionsTests
{
    [Test]
    public void Advance_IgnoresZeroLastPlayed_SoEpochNeverLeaks()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Playtime result = new Playtime { LastPlayedMs = 0, AgeMs = 0 }.Advance(now);

        Assert.Multiple(() =>
        {
            Assert.That(result.AgeMs, Is.Zero);
            Assert.That(result.LastPlayedMs, Is.EqualTo(now));
        });
    }

    [Test]
    public void Accumulate_AddsElapsedSinceLastPlayed()
    {
        Playtime result = new Playtime { LastPlayedMs = 1_000, AgeMs = 500 }.Advance(3_000);

        Assert.That(result.AgeMs, Is.EqualTo(2_500));
    }

    [Test]
    public void Advance_KeepsExistingAgeWhenNowEqualsLastPlayed()
    {
        Playtime result = new Playtime { LastPlayedMs = 1_000, AgeMs = 700 }.Advance(1_000);

        Assert.That(result.AgeMs, Is.EqualTo(700));
    }

    [Test]
    public void Advance_ClampsNegativeClockSkewToZeroDelta()
    {
        Playtime result = new Playtime { LastPlayedMs = 5_000, AgeMs = 200 }.Advance(4_000);

        Assert.That(result.AgeMs, Is.EqualTo(200));
    }
}