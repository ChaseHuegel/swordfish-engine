using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Tests;

[TestFixture]
public class SaveTimeTests
{
    [Test]
    public void Accumulate_IgnoresZeroLastPlayed_SoEpochNeverLeaks()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long age = SaveTime.Accumulate(0, 0, now);

        Assert.That(age, Is.Zero);
    }

    [Test]
    public void Accumulate_AddsElapsedSinceLastPlayed()
    {
        long lastPlayed = 1_000;
        long age = SaveTime.Accumulate(500, lastPlayed, 3_000);

        Assert.That(age, Is.EqualTo(2_500));
    }

    [Test]
    public void Accumulate_KeepsExistingAgeWhenNowEqualsLastPlayed()
    {
        long now = 1_000;
        long age = SaveTime.Accumulate(700, now, now);

        Assert.That(age, Is.EqualTo(700));
    }

    [Test]
    public void Accumulate_ClampsNegativeClockSkewToZeroDelta()
    {
        long age = SaveTime.Accumulate(200, 5_000, 4_000);

        Assert.That(age, Is.EqualTo(200));
    }
}