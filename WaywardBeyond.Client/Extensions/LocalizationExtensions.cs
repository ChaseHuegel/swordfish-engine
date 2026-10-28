using Swordfish.Library.Globalization;
using System.Text;
using System;
using WaywardBeyond.Client.Extensions;

namespace WaywardBeyond.Client.Extensions;

public static class LocalizationExtensions
{
    /// <summary>Gets a localized string, and throws when the key is missing.</summary>
    /// <exception cref="InvalidOperationException">The key is not present in the active language.</exception>
    public static string GetRequired(this ILocalization localization, string key)
    {
        return localization.GetString(key)
            ?? throw new InvalidOperationException($"The localization key \"{key}\" is missing.");
    }

    /// <summary>Gets a localized species name, falling back to the raw species ID.</summary>
    public static string GetSpeciesName(this ILocalization localization, string species)
    {
        if (string.IsNullOrEmpty(species))
        {
            return string.Empty;
        }

        string key = $"body.species.{species}";
        string name = localization.GetString(key) ?? key;
        return name == key ? species : name;
    }

    /// <summary>Formats the colonist level and species line, for example "Level 4 · Human".</summary>
    public static string GetCharacterLevelLine(this ILocalization localization, int level, string species)
    {
        string levelLabel = localization.GetRequired("ui.label.character.level");
        string speciesName = localization.GetSpeciesName(species);
        return speciesName.Length == 0
            ? $"{levelLabel} {level}"
            : $"{levelLabel} {level} · {speciesName}";
    }

    public static string GetLongString(this ILocalization localization, TimeSpan timeSpan)
    {
        string result;
        if ((int)timeSpan.TotalSeconds > 0)
        {
            var timePlayedBuilder = new StringBuilder();
            if (timeSpan.TotalHours >= 1)
            {
                timePlayedBuilder.Append((int)timeSpan.TotalHours);
                timePlayedBuilder.Append(' ');
                timePlayedBuilder.Append(localization.GetRequired("ui.word.hours"));
            }

            if (timeSpan.Minutes >= 1)
            {
                if (timePlayedBuilder.Length > 0)
                {
                    timePlayedBuilder.Append(", ");
                }
                        
                timePlayedBuilder.Append(timeSpan.Minutes);
                timePlayedBuilder.Append(' ');
                timePlayedBuilder.Append(localization.GetRequired("ui.word.minutes"));
            }

            if (timeSpan.Seconds >= 1)
            {
                if (timePlayedBuilder.Length > 0)
                {
                    timePlayedBuilder.Append(", ");
                }
                        
                timePlayedBuilder.Append(timeSpan.Seconds);
                timePlayedBuilder.Append(' ');
                timePlayedBuilder.Append(localization.GetRequired("ui.word.seconds"));
            }

            result = timePlayedBuilder.ToString();
        }
        else
        {
            result = localization.GetRequired("ui.label.none");
        }

        return result;
    }
}
