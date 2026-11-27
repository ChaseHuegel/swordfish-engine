using System;
using Swordfish.Library.Util;
using Xunit;

namespace Swordfish.Tests;

public class TimeWrapTests
{
    [Fact]
    public void TimeWrapSecondsIsWholeTauCycles()
    {
        Assert.True(MathS.TIME_WRAP_SECONDS > 3000d);
        Assert.Equal(0d, MathS.TIME_WRAP_SECONDS % Math.Tau);
    }

    [Fact]
    public void RepeatWrapsElapsedTimeIntoInterval()
    {
        double wrapped = MathS.Repeat((MathS.TIME_WRAP_SECONDS * 3d) + 7.25d, MathS.TIME_WRAP_SECONDS);
        Assert.Equal(7.25d, wrapped, precision: 9);

        double clamped = MathS.Repeat(MathS.TIME_WRAP_SECONDS, MathS.TIME_WRAP_SECONDS);
        Assert.InRange(clamped, 0d, MathS.TIME_WRAP_SECONDS);
    }

    [Theory]
    [InlineData(36d)]
    [InlineData(48d)]
    [InlineData(53d)]
    [InlineData(62d)]
    public void WrapKeepsIntegerFrequencyTrigContinuous(double frequency)
    {
        const double sample = 1.25d;
        double beforeWrap = Math.Sin(sample * frequency);
        double afterWrap = Math.Sin((MathS.TIME_WRAP_SECONDS + sample) * frequency);
        Assert.Equal(beforeWrap, afterWrap, precision: 9);
    }

    [Fact]
    public void WrapKeepsFloatPhaseAccurateInLongSessions()
    {
        const float frequency = 62f;
        const double sample = 0.75d;
        double longTime = (MathS.TIME_WRAP_SECONDS * 1024d) + sample;

        var unwrappedTime = (float)longTime;
        var wrappedTime = (float)MathS.Repeat(longTime, MathS.TIME_WRAP_SECONDS);

        float idealPhase = MathF.Sin((float)sample * frequency);
        float wrappedPhase = MathF.Sin(wrappedTime * frequency);

        Assert.True(MathF.Abs(idealPhase - wrappedPhase) < 0.01f);
        Assert.True(MathF.Abs(unwrappedTime - (float)sample) > 0.01f);
    }
}
