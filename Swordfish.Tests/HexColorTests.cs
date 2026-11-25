using System;
using System.Numerics;
using Swordfish.Library.Util;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Covers <see cref="HexColor"/> validation, normalization, and RGBA parsing.</summary>
public class HexColorTests
{
    [Fact]
    public void IsHexDigitRecognizesValidAndBoundaryCharacters()
    {
        for (var c = '0'; c <= '9'; c++)
        {
            Assert.True(HexColor.IsHexDigit(c));
        }

        for (var c = 'a'; c <= 'f'; c++)
        {
            Assert.True(HexColor.IsHexDigit(c));
        }

        for (var c = 'A'; c <= 'F'; c++)
        {
            Assert.True(HexColor.IsHexDigit(c));
        }

        Assert.False(HexColor.IsHexDigit('/'));
        Assert.False(HexColor.IsHexDigit(':'));
        Assert.False(HexColor.IsHexDigit('@'));
        Assert.False(HexColor.IsHexDigit('G'));
        Assert.False(HexColor.IsHexDigit('`'));
        Assert.False(HexColor.IsHexDigit('g'));
        Assert.False(HexColor.IsHexDigit(' '));
        Assert.False(HexColor.IsHexDigit('#'));
    }

    [Theory]
    [InlineData("#336699", 0x33, 0x66, 0x99)]
    [InlineData("336699", 0x33, 0x66, 0x99)]
    [InlineData("  #aAbBcC\t", 0xAA, 0xBB, 0xCC)]
    public void TryParseRgbParsesSixDigitStrings(string input, byte expectedR, byte expectedG, byte expectedB)
    {
        bool parsed = HexColor.TryParseRgb(input, out Vector3 color);

        Assert.True(parsed);
        Assert.Equal(expectedR / 255f, color.X, 4);
        Assert.Equal(expectedG / 255f, color.Y, 4);
        Assert.Equal(expectedB / 255f, color.Z, 4);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#369")]
    [InlineData("#369f")]
    [InlineData("#336699FF")]
    [InlineData("12 4 6")]
    [InlineData("##112233")]
    [InlineData("#12345z")]
    public void TryParseRgbRejectsInvalidInputs(string? input)
    {
        bool parsed = HexColor.TryParseRgb(input, out Vector3 color);

        Assert.False(parsed);
        Assert.Equal(default, color);
    }

    [Theory]
    [InlineData("#abc", 0xAA, 0xBB, 0xCC, 0xFF)]
    [InlineData("abc", 0xAA, 0xBB, 0xCC, 0xFF)]
    [InlineData("#f08c", 0xFF, 0x00, 0x88, 0xCC)]
    [InlineData("  #123456 ", 0x12, 0x34, 0x56, 0xFF)]
    [InlineData("12345680", 0x12, 0x34, 0x56, 0x80)]
    public void TryParseRgbaAndBytesSupportThreeFourSixAndEightDigits(
        string input,
        byte expectedR,
        byte expectedG,
        byte expectedB,
        byte expectedA
    ) {
        Assert.True(HexColor.TryParseRgbaBytes(input.AsSpan(), out byte r, out byte g, out byte b, out byte a));
        Assert.Equal(expectedR, r);
        Assert.Equal(expectedG, g);
        Assert.Equal(expectedB, b);
        Assert.Equal(expectedA, a);

        Assert.True(HexColor.TryParseRgba(input, out Vector4 color));
        Assert.Equal(expectedR / 255f, color.X, 4);
        Assert.Equal(expectedG / 255f, color.Y, 4);
        Assert.Equal(expectedB / 255f, color.Z, 4);
        Assert.Equal(expectedA / 255f, color.W, 4);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#1")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#123456789")]
    [InlineData("12 4 6")]
    [InlineData("#12345 7")]
    [InlineData("#f08z")]
    [InlineData("#1234567g")]
    public void TryParseRgbaAndBytesRejectInvalidInputs(string? input)
    {
        Assert.False(HexColor.TryParseRgba(input, out Vector4 color));
        Assert.Equal(default, color);

        Assert.False(HexColor.TryParseRgbaBytes(input.AsSpan(), out byte r, out byte g, out byte b, out byte a));
        Assert.Equal((byte)0, r);
        Assert.Equal((byte)0, g);
        Assert.Equal((byte)0, b);
        Assert.Equal((byte)0, a);
    }
}
