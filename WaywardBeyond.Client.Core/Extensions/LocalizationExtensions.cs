using System;
using System.Text;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.Core.Extensions;

public static class LocalizationExtensions
{
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
                timePlayedBuilder.Append(localization.GetString("ui.word.hours")!);
            }

            if (timeSpan.Minutes >= 1)
            {
                if (timePlayedBuilder.Length > 0)
                {
                    timePlayedBuilder.Append(", ");
                }
                        
                timePlayedBuilder.Append(timeSpan.Minutes);
                timePlayedBuilder.Append(' ');
                timePlayedBuilder.Append(localization.GetString("ui.word.minutes")!);
            }

            if (timeSpan.Seconds >= 1)
            {
                if (timePlayedBuilder.Length > 0)
                {
                    timePlayedBuilder.Append(", ");
                }
                        
                timePlayedBuilder.Append(timeSpan.Seconds);
                timePlayedBuilder.Append(' ');
                timePlayedBuilder.Append(localization.GetString("ui.word.seconds")!);
            }

            result = timePlayedBuilder.ToString();
        }
        else
        {
            result = localization.GetString("ui.text.none")!;
        }

        return result;
    }
}