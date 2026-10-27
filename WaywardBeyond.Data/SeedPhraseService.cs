using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Data;

/// <summary>
///     Translates 64-bit world seeds into deterministic four-word phrases and resolves phrases or custom
///     text back into 64-bit seeds.
/// </summary>
public sealed class SeedPhraseService
{
    /// <summary>The required entry count for each category dictionary.</summary>
    public const int DICTIONARY_SIZE = 1024;

    /// <summary>The localized tag key for the adjective slot.</summary>
    public const string ADJECTIVES_TAG = "seeds.adjectives";

    /// <summary>The localized tag key for the subject slot.</summary>
    public const string SUBJECTS_TAG = "seeds.subjects";

    /// <summary>The localized tag key for the verb slot.</summary>
    public const string VERBS_TAG = "seeds.verbs";

    /// <summary>The localized tag key for the destination slot.</summary>
    public const string DESTINATIONS_TAG = "seeds.destinations";

    private const string DEFAULT_LANGUAGE = "en";
    private const ulong SLOT_MASK = 0x3FF;
    private const int STACKALLOC_CHAR_LIMIT = 256;

    private readonly IReadOnlyList<string> _adjectives;
    private readonly IReadOnlyList<string> _subjects;
    private readonly IReadOnlyList<string> _verbs;
    private readonly IReadOnlyList<string> _destinations;

    private readonly Dictionary<string, int> _adjectiveIndices;
    private readonly Dictionary<string, int> _subjectIndices;
    private readonly Dictionary<string, int> _verbIndices;
    private readonly Dictionary<string, int> _destinationIndices;

    /// <summary>
    ///     Initializes a <see cref="SeedPhraseService"/> from the localized tag database.
    /// </summary>
    public SeedPhraseService(in IAssetDatabase<LocalizedTags> localizedTagDatabase)
    {
        ArgumentNullException.ThrowIfNull(localizedTagDatabase);

        LocalizedTags tags = ResolveTags(localizedTagDatabase);

        _adjectives = GetRequiredDictionary(tags, ADJECTIVES_TAG);
        _subjects = GetRequiredDictionary(tags, SUBJECTS_TAG);
        _verbs = GetRequiredDictionary(tags, VERBS_TAG);
        _destinations = GetRequiredDictionary(tags, DESTINATIONS_TAG);

        _adjectiveIndices = BuildReverseMap(_adjectives, ADJECTIVES_TAG);
        _subjectIndices = BuildReverseMap(_subjects, SUBJECTS_TAG);
        _verbIndices = BuildReverseMap(_verbs, VERBS_TAG);
        _destinationIndices = BuildReverseMap(_destinations, DESTINATIONS_TAG);
    }

    /// <summary>The loaded adjective dictionary.</summary>
    public IReadOnlyList<string> Adjectives => _adjectives;

    /// <summary>The loaded subject dictionary.</summary>
    public IReadOnlyList<string> Subjects => _subjects;

    /// <summary>The loaded verb dictionary.</summary>
    public IReadOnlyList<string> Verbs => _verbs;

    /// <summary>The loaded destination dictionary.</summary>
    public IReadOnlyList<string> Destinations => _destinations;

    /// <summary>
    ///     Encodes the lower 40 bits of a 64-bit seed into a four-word sentence.
    /// </summary>
    public string SeedToPhrase(long seed)
    {
        var value = (ulong)seed;

        var adjIndex = (int)(value & SLOT_MASK);
        var subjIndex = (int)((value >> 10) & SLOT_MASK);
        var verbIndex = (int)((value >> 20) & SLOT_MASK);
        var destIndex = (int)((value >> 30) & SLOT_MASK);

        return $"{_adjectives[adjIndex]} {_subjects[subjIndex]} {_verbs[verbIndex]} {_destinations[destIndex]}";
    }

    /// <summary>
    ///     Parses a four-word phrase or custom input into a 64-bit integer seed.
    /// </summary>
    public long ResolveSeed(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0L;
        }

        string normalized = NormalizeInput(input);
        string[] tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 4 &&
            _adjectiveIndices.TryGetValue(tokens[0], out int adjIdx) &&
            _subjectIndices.TryGetValue(tokens[1], out int subjIdx) &&
            _verbIndices.TryGetValue(tokens[2], out int verbIdx) &&
            _destinationIndices.TryGetValue(tokens[3], out int destIdx))
        {
            ulong reconstructed = (ulong)adjIdx |
                                  ((ulong)subjIdx << 10) |
                                  ((ulong)verbIdx << 20) |
                                  ((ulong)destIdx << 30);

            return (long)reconstructed;
        }

        return (long)FNV1a.ComputeHash64(normalized);
    }

    private static LocalizedTags ResolveTags(IAssetDatabase<LocalizedTags> localizedTagDatabase)
    {
        Result<LocalizedTags> currentCultureTags = localizedTagDatabase.Get(CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
        if (currentCultureTags.Success && currentCultureTags.Value.GetValues(ADJECTIVES_TAG) != null)
        {
            return currentCultureTags.Value;
        }

        Result<LocalizedTags> fallbackTags = localizedTagDatabase.Get(DEFAULT_LANGUAGE);
        if (fallbackTags.Success)
        {
            return fallbackTags.Value;
        }

        throw new InvalidOperationException($"Failed to load localized seed tags for language \"{DEFAULT_LANGUAGE}\".");
    }

    private static IReadOnlyList<string> GetRequiredDictionary(LocalizedTags tags, string tagKey)
    {
        IReadOnlyList<string>? values = tags.GetValues(tagKey);
        if (values == null || values.Count != DICTIONARY_SIZE)
        {
            throw new ArgumentException($"Tag \"{tagKey}\" must contain {DICTIONARY_SIZE} entries.");
        }

        return values;
    }

    private static Dictionary<string, int> BuildReverseMap(IReadOnlyList<string> list, string tagKey)
    {
        var map = new Dictionary<string, int>(list.Count, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < list.Count; i++)
        {
            map[list[i]] = i;
        }

        if (map.Count != DICTIONARY_SIZE)
        {
            throw new ArgumentException($"Tag \"{tagKey}\" must contain {DICTIONARY_SIZE} unique entries.");
        }

        return map;
    }

    private static string NormalizeInput(string raw)
    {
        char[]? rented = null;
        Span<char> buffer = raw.Length <= STACKALLOC_CHAR_LIMIT
            ? stackalloc char[raw.Length]
            : (rented = ArrayPool<char>.Shared.Rent(raw.Length)).AsSpan(0, raw.Length);

        try
        {
            var writeIndex = 0;
            var lastWasSpace = false;

            for (var i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (char.IsLetterOrDigit(c))
                {
                    buffer[writeIndex++] = char.ToLowerInvariant(c);
                    lastWasSpace = false;
                }
                else if (!lastWasSpace && writeIndex > 0)
                {
                    buffer[writeIndex++] = ' ';
                    lastWasSpace = true;
                }
            }

            if (writeIndex > 0 && buffer[writeIndex - 1] == ' ')
            {
                writeIndex--;
            }

            return new string(buffer[..writeIndex]);
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}
