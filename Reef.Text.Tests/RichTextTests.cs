using System.Numerics;
using Reef.Text;

namespace Reef.Text.Tests;

public class RichTextTests
{
    private static readonly Vector4 _red = new(1f, 0f, 0f, 1f);
    private static readonly Vector4 _blue = new(0f, 0f, 1f, 1f);

    [Fact]
    public void UntaggedTextPassesThroughUntouched()
    {
        RichText rich = RichText.Parse("plain text", start: 0, length: 10);

        Assert.Equal("plain text", rich.Text);
        Assert.All(rich.Color, color => Assert.Null(color));
    }

    [Fact]
    public void StripsTokensAndAlignsColors()
    {
        RichText rich = RichText.Parse("#FF0000Hello #Rworld#BLUE!", start: 0, length: 26);

        Assert.Equal("Hello world!", rich.Text);
        AssertColor(_red, rich.Color[0]);
        AssertColor(_red, rich.Color[5]);
        Assert.Null(rich.Color[6]);
        Assert.Null(rich.Color[10]);
        AssertColor(_blue, rich.Color[11]);
    }

    [Fact]
    public void NameValueStopsAtText()
    {
        RichText rich = RichText.Parse("#REDHello", start: 0, length: 9);

        Assert.Equal("Hello", rich.Text);
        Assert.All(rich.Color, color => AssertColor(_red, color));
    }

    [Fact]
    public void HexValueStopsAtText()
    {
        RichText rich = RichText.Parse("#FF0000Hi", start: 0, length: 9);

        Assert.Equal("Hi", rich.Text);
        Assert.All(rich.Color, color => AssertColor(_red, color));
    }

    [Fact]
    public void EightDigitHexAppliesAlpha()
    {
        RichText rich = RichText.Parse("#FF000080x", start: 0, length: 10);

        Assert.Equal("x", rich.Text);
        AssertColor(new Vector4(1f, 0f, 0f, 0x80 / 255f), rich.Color[0]);
    }

    [Fact]
    public void ResetRestoresElementColor()
    {
        RichText rich = RichText.Parse("#REDa#Rb", start: 0, length: 8);

        Assert.Equal("ab", rich.Text);
        AssertColor(_red, rich.Color[0]);
        Assert.Null(rich.Color[1]);
    }

    [Fact]
    public void LongResetFormRestoresElementColor()
    {
        RichText rich = RichText.Parse("#REDa#RESETb", start: 0, length: 12);

        Assert.Equal("ab", rich.Text);
        AssertColor(_red, rich.Color[0]);
        Assert.Null(rich.Color[1]);
    }

    [Fact]
    public void DoubleHashIsLiteral()
    {
        RichText rich = RichText.Parse("#RED##A", start: 0, length: 7);

        Assert.Equal("#A", rich.Text);
        AssertColor(_red, rich.Color[0]);
        AssertColor(_red, rich.Color[1]);
    }

    [Fact]
    public void DoubleHashWithNoColorRunIsLiteral()
    {
        RichText rich = RichText.Parse("##A", start: 0, length: 3);

        Assert.Equal("#A", rich.Text);
        Assert.Null(rich.Color[0]);
        Assert.Null(rich.Color[1]);
    }

    [Fact]
    public void TrailingHashIsLiteral()
    {
        RichText rich = RichText.Parse("a#", start: 0, length: 2);

        Assert.Equal("a#", rich.Text);
        Assert.Null(rich.Color[1]);
    }

    [Fact]
    public void InvalidValueIsLiteral()
    {
        RichText rich = RichText.Parse("#5zA", start: 0, length: 4);

        Assert.Equal("#5zA", rich.Text);
        Assert.Null(rich.Color[0]);
    }

    [Fact]
    public void TrailingSpaceAfterTokenIsConsumed()
    {
        RichText rich = RichText.Parse("#FF0000 Hi", start: 0, length: 10);

        Assert.Equal("Hi", rich.Text);
        Assert.All(rich.Color, color => AssertColor(_red, color));
    }

    [Fact]
    public void TrailingSpaceAfterResetIsConsumed()
    {
        RichText rich = RichText.Parse("#RED A#RESET B", start: 0, length: 14);

        Assert.Equal("AB", rich.Text);
        AssertColor(_red, rich.Color[0]);
        Assert.Null(rich.Color[1]);
    }

    private static void AssertColor(Vector4 expected, Vector4? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.X, actual.Value.X, 3);
        Assert.Equal(expected.Y, actual.Value.Y, 3);
        Assert.Equal(expected.Z, actual.Value.Z, 3);
        Assert.Equal(expected.W, actual.Value.W, 3);
    }
}