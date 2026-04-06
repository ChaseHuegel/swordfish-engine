using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Globalization;
using WaywardBeyond.Client.Core.Meta;

namespace WaywardBeyond.Client.Core.Services;

internal partial class NameGenerator(
    in Randomizer randomizer,
    in LocalizedFormatter localizedFormatter,
    in IAssetDatabase<LocalizedTags> localizedTagDatabase
) {
    private readonly Randomizer _randomizer = randomizer;
    private readonly LocalizedFormatter _localizedFormatter = localizedFormatter;
    private readonly IAssetDatabase<LocalizedTags> _localizedTagDatabase = localizedTagDatabase;

    public string Generate(string key, Options options)
    {
        Result<LocalizedTags> localizedTags = _localizedTagDatabase.Get(CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
        if (!localizedTags.Success)
        {
            return string.Empty;
        }

        var generatedName = new GeneratedName();
        
        IReadOnlyList<string>? nicknames = localizedTags.Value.GetValues($"names.nickname.{key}");
        if (nicknames?.Count > 0 && _randomizer.NextFloat() <= options.NicknameChance)
        {
            generatedName.Nickname = _randomizer.Select(nicknames);
            
            IReadOnlyList<string>? titles = localizedTags.Value.GetValues($"names.title.{key}");
            if (titles?.Count > 0 && _randomizer.NextFloat() <= options.TitleChance)
            {
                generatedName.Title = _randomizer.Select(titles);
            }
        }
        else
        {
            IReadOnlyList<string>? titles = localizedTags.Value.GetValues($"names.title.{key}");
            if (titles?.Count > 0 && _randomizer.NextFloat() <= options.TitleChance)
            {
                generatedName.Title = _randomizer.Select(titles);
            }
            else
            {
                IReadOnlyList<string>? firstNames = localizedTags.Value.GetValues($"names.first.{key}");
                if (firstNames?.Count > 0 && _randomizer.NextFloat() <= options.FirstNameChance)
                {
                    generatedName.First = _randomizer.Select(firstNames);
                }
            }
            
            IReadOnlyList<string>? lastNames = localizedTags.Value.GetValues($"names.last.{key}");
            if (lastNames?.Count > 0 && _randomizer.NextFloat() <= options.LastNameChance)
            {
                generatedName.Last = _randomizer.Select(lastNames);
            }
            
            IReadOnlyList<string>? subtitles = localizedTags.Value.GetValues($"names.subtitle.{key}");
            if (subtitles?.Count > 0 && _randomizer.NextFloat() <= options.SubtitleChance)
            {
                generatedName.Subtitle = _randomizer.Select(subtitles);
            }
        }

        string result = _localizedFormatter.GetString($"formats.name.{key}", generatedName);
        
        //  Cleanup the result, trimming and collapsing whitespace
        result = CollapseWhitespaceRegex().Replace(input: result, replacement: " ");
        result = result.Trim();
        result = result.Trim('-');
        
        return result;
    }

    private record struct GeneratedName(string? Title, string? First, string? Last, string? Subtitle, string? Nickname);

    internal record struct Options
    (
        float TitleChance,
        float FirstNameChance,
        float LastNameChance,
        float SubtitleChance,
        float NicknameChance
    );

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespaceRegex();
}