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
    private const float TITLE_CHANCE = 0.2f;
    private const float FIRST_NAME_CHANCE = 1.0f;
    private const float LAST_NAME_CHANCE = 1.0f;
    private const float SUBTITLE_CHANCE = 0.1f;
    private const float NICKNAME_CHANCE = 0.1f;
    
    private readonly Randomizer _randomizer = randomizer;
    private readonly LocalizedFormatter _localizedFormatter = localizedFormatter;
    private readonly IAssetDatabase<LocalizedTags> _localizedTagDatabase = localizedTagDatabase;

    public string Generate(string key)
    {
        Result<LocalizedTags> localizedTags = _localizedTagDatabase.Get(CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
        if (!localizedTags.Success)
        {
            return string.Empty;
        }

        var generatedName = new GeneratedName();
        
        IReadOnlyList<string>? nicknames = localizedTags.Value.GetValues($"names.nickname.{key}");
        if (nicknames != null && _randomizer.NextFloat() <= NICKNAME_CHANCE)
        {
            generatedName.Nickname = _randomizer.Select(nicknames);
            
            IReadOnlyList<string>? titles = localizedTags.Value.GetValues($"names.title.{key}");
            if (titles != null && _randomizer.NextFloat() <= TITLE_CHANCE)
            {
                generatedName.Title = _randomizer.Select(titles);
            }
        }
        else
        {
            IReadOnlyList<string>? titles = localizedTags.Value.GetValues($"names.title.{key}");
            if (titles != null && _randomizer.NextFloat() <= TITLE_CHANCE)
            {
                generatedName.Title = _randomizer.Select(titles);
            }
            else
            {
                IReadOnlyList<string>? firstNames = localizedTags.Value.GetValues($"names.first.{key}");
                if (firstNames != null && _randomizer.NextFloat() <= FIRST_NAME_CHANCE)
                {
                    generatedName.First = _randomizer.Select(firstNames);
                }
            }
            
            IReadOnlyList<string>? lastNames = localizedTags.Value.GetValues($"names.last.{key}");
            if (lastNames != null && _randomizer.NextFloat() <= LAST_NAME_CHANCE)
            {
                generatedName.Last = _randomizer.Select(lastNames);
            }
            
            IReadOnlyList<string>? subtitles = localizedTags.Value.GetValues($"names.subtitle.{key}");
            if (subtitles != null && _randomizer.NextFloat() <= SUBTITLE_CHANCE)
            {
                generatedName.Subtitle = _randomizer.Select(subtitles);
            }
        }

        string result = _localizedFormatter.GetString($"formats.name.{key}", generatedName);
        
        //  Cleanup the result, trimming and collapsing whitespace
        result = CollapseWhitespaceRegex().Replace(input: result, replacement: " ");
        result = result.Trim();
        
        return result;
    }

    private record struct GeneratedName(string? Title, string? First, string? Last, string? Subtitle, string? Nickname);

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespaceRegex();
}