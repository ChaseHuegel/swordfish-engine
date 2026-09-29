using System.Numerics;
using Reef.Text;

namespace Reef.Text.Tests;

public class RichTextColorTests
{
    private const int HASH = 1;

    [Fact]
    public void ParsesSixDigitHex()
    {
        bool parsed = RichTextColor.TryParseValue("#1234ab", HASH, valueLength: 6, out Vector4? color, out int consumed);

        Assert.True(parsed);
        Assert.Equal(6, consumed);
        Assert.Equal(0x12 / 255f, color!.Value.X, 3);
        Assert.Equal(0x34 / 255f, color!.Value.Y, 3);
        Assert.Equal(0xab / 255f, color!.Value.Z, 3);
        Assert.Equal(1f, color!.Value.W, 3);
    }

    [Fact]
    public void ParsesEightDigitHexWithAlpha()
    {
        bool parsed = RichTextColor.TryParseValue("#123456ff", HASH, valueLength: 8, out Vector4? color, out int consumed);

        Assert.True(parsed);
        Assert.Equal(8, consumed);
        Assert.Equal(0x12 / 255f, color!.Value.X, 3);
        Assert.Equal(0x34 / 255f, color!.Value.Y, 3);
        Assert.Equal(0x56 / 255f, color!.Value.Z, 3);
        Assert.Equal(0xff / 255f, color!.Value.W, 3);
    }

    [Fact]
    public void PrefersEightDigitHexOverSix()
    {
        bool parsed = RichTextColor.TryParseValue("#123456ff", HASH, valueLength: 8, out Vector4? color, out int consumed);

        Assert.True(parsed);
        Assert.Equal(8, consumed);
    }

    [Fact]
    public void DoesNotConsumeHexFollowedByHexDigit()
    {
        bool parsed = RichTextColor.TryParseValue("#1234567", HASH, valueLength: 7, out Vector4? color, out int consumed);

        Assert.False(parsed);
    }

    [Fact]
    public void ParsesNamedColor()
    {
        bool parsed = RichTextColor.TryParseValue("#RED", HASH, valueLength: 3, out Vector4? color, out int consumed);

        Assert.True(parsed);
        Assert.Equal(3, consumed);
        Assert.Equal(1f, color!.Value.X, 3);
        Assert.Equal(0f, color!.Value.Y, 3);
        Assert.Equal(0f, color!.Value.Z, 3);
    }

    [Fact]
    public void ParsesNamedColorCaseInsensitively()
    {
        bool parsed = RichTextColor.TryParseValue("#BlUe", HASH, valueLength: 4, out Vector4? color, out int consumed);

        Assert.True(parsed);
        Assert.Equal(4, consumed);
        Assert.Equal(1f, color!.Value.Z, 3);
    }

    [Fact]
    public void InvalidHexLengthIsNotFound()
    {
        bool parsed = RichTextColor.TryParseValue("#cafe9", HASH, valueLength: 5, out Vector4? color, out int consumed);

        Assert.False(parsed);
    }

    [Fact]
    public void ResolveLongestName()
    {
        bool parsed = RichTextColor.TryParseValue("#rebeccapurple", HASH, valueLength: 13, out Vector4? color, out int consumed);

        Assert.True(parsed);
        Assert.Equal(13, consumed);
    }

    [Fact]
    public void ResolveLongestCssName()
    {
        bool parsed = RichTextColor.TryParseValue("#lightgoldenrodyellow", HASH, valueLength: 20, out Vector4? color, out int consumed);

        Assert.True(parsed);
        Assert.Equal(20, consumed);
    }

    [Fact]
    public void InvalidValueIsNotFound()
    {
        bool parsed = RichTextColor.TryParseValue("#5z", HASH, valueLength: 2, out Vector4? color, out int consumed);

        Assert.False(parsed);
    }
}