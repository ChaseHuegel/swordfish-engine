using WaywardBeyond.Data;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Pins the display-name normalization the client input and server relay share.</summary>
public class CharacterNamesTests
{
    [Fact]
    public void TrimsSurroundingWhitespace()
    {
        Assert.Equal("Nova", CharacterNames.Clamp("  Nova \t"));
    }

    [Fact]
    public void ReplacesControlCharactersWithOneSpace()
    {
        Assert.Equal("Nova Prime", CharacterNames.Clamp("Nova\r\n\tPrime"));
    }

    [Fact]
    public void TruncatesToTheMaximumLength()
    {
        string clamped = CharacterNames.Clamp(new string('a', CharacterNames.MAX_LENGTH + 50));

        Assert.Equal(CharacterNames.MAX_LENGTH, clamped.Length);
    }

    [Fact]
    public void NullAndWhitespaceYieldAnEmptyName()
    {
        Assert.Equal(string.Empty, CharacterNames.Clamp(null));
        Assert.Equal(string.Empty, CharacterNames.Clamp("   "));
    }

    [Fact]
    public void LeavesAShortCleanNameUnchanged()
    {
        Assert.Equal("Nova-7", CharacterNames.Clamp("Nova-7"));
    }
}
