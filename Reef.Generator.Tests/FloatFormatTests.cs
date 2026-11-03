using Xunit;

namespace Reef.Generator.Tests;

public class FloatFormatTests
{
    [Theory]
    [InlineData(0.30980393f)]
    [InlineData(0.8784314f)]
    [InlineData(0.06666667f)]
    [InlineData(1f)]
    public void FormatFloatRoundTrips(float value)
    {
        string text = FnvHash.FormatFloat(value);

        Assert.True(
            float.TryParse(
                text.TrimEnd('f'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float parsed
            )
        );
        Assert.Equal(value, parsed);
    }
}
