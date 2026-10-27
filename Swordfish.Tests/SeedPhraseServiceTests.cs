using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Localization;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Pins the procedural seed phrase generation, parsing, and dictionary constraints.</summary>
public class SeedPhraseServiceTests
{
    private readonly SeedPhraseService _service = CreateService();

    [Fact]
    public void DictionariesSatisfyGlobalConstraints()
    {
        IReadOnlyList<string>[] categories =
        [
            _service.Adjectives,
            _service.Subjects,
            _service.Verbs,
            _service.Destinations,
        ];

        foreach (IReadOnlyList<string> dictionary in categories)
        {
            Assert.Equal(SeedPhraseService.DICTIONARY_SIZE, dictionary.Count);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string word in dictionary)
            {
                Assert.True(seen.Add(word), $"Duplicate word '{word}' found in dictionary.");
                Assert.InRange(word.Length, 3, 9);
                Assert.True(char.IsAsciiLetterUpper(word[0]), $"Word '{word}' must start with an uppercase ASCII letter.");

                for (var i = 1; i < word.Length; i++)
                {
                    Assert.True(char.IsAsciiLetterLower(word[i]), $"Word '{word}' must be TitleCase ASCII letters only.");
                }
            }
        }
    }

    [Theory]
    [InlineData("Obsidian Corsair Ignites Hyperion", (0L << 0) | (1L << 10) | (0L << 20) | (0L << 30))]
    [InlineData("Derelict Sentry Shields Caldera", (1L << 0) | (3L << 10) | (1L << 20) | (1L << 30))]
    [InlineData("Wayward Nomad Navigates Andromeda", (2L << 0) | (0L << 10) | (2L << 20) | (2L << 30))]
    public void ExactDecodeMatchesBitwiseLayout(string phrase, long expectedSeed)
    {
        long resolved = _service.ResolveSeed(phrase);

        Assert.Equal(expectedSeed, resolved);
        Assert.Equal(phrase, _service.SeedToPhrase(resolved));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData((1023L << 0) | (1023L << 10) | (1023L << 20) | (1023L << 30))]
    [InlineData((42L << 0) | (512L << 10) | (777L << 20) | (999L << 30))]
    public void BijectiveRoundTripPreservesFortyBitSeed(long seed)
    {
        string phrase = _service.SeedToPhrase(seed);
        long resolved = _service.ResolveSeed(phrase);

        Assert.Equal(seed, resolved);
    }

    [Fact]
    public void CaseInsensitivityResolvesToIdenticalSeed()
    {
        long expected = _service.ResolveSeed("Obsidian Corsair Ignites Hyperion");
        long actual = _service.ResolveSeed("obsidian corsair ignites hyperion");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PunctuationAndSpacingNormalizeBeforeLookup()
    {
        long expected = _service.ResolveSeed("Obsidian Corsair Ignites Hyperion");
        long actual = _service.ResolveSeed("  Obsidian-Corsair  ignites, Hyperion. ");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void InvalidWordInSlotFallsBackToFnv1a64()
    {
        long actual = _service.ResolveSeed("Obsidian Apple Ignites Hyperion");
        var expected = (long)FNV1a.ComputeHash64("obsidian apple ignites hyperion");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WrongTokenCountFallsBackToFnv1a64()
    {
        long actual = _service.ResolveSeed("Obsidian Corsair Ignites");
        var expected = (long)FNV1a.ComputeHash64("obsidian corsair ignites");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ArbitraryFreeformStringFallsBackToFnv1a64()
    {
        long actual = _service.ResolveSeed("MyFavoriteWorld42");
        var expected = (long)FNV1a.ComputeHash64("myfavoriteworld42");

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t\r\n  ")]
    public void NullOrWhitespaceReturnsZero(string? input)
    {
        Assert.Equal(0L, _service.ResolveSeed(input));
    }

    private static SeedPhraseService CreateService()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo("assets"));

        IFileParser[] parsers = [new LocalizedTagDefinitionParser()];
        var parseService = new VirtualFileParseService(parsers, vfs);
        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parseService, vfs);

        return new SeedPhraseService(tags);
    }
}
