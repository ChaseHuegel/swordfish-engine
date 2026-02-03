using System.Text.RegularExpressions;

namespace Swordfish.Library.Extensions;

public static class RegexExtensions
{
    public static Match MatchNext(this Regex regex, string input, int startIndex)
    {
        MatchCollection matches = regex.Matches(input);
        return matches.FindNext(startIndex);

    }

    public static Match MatchPrevious(this Regex regex, string input, int startIndex)
    {
        MatchCollection matches = regex.Matches(input);
        return matches.FindPrevious(startIndex);
    }
    
    public static Match FindNext(this MatchCollection matchCollection, int startIndex)
    {
        foreach (Match match in matchCollection)
        {
            if (match.Index <= startIndex)
            {
                continue;
            }

            return match;
        }

        return Match.Empty;
    }

    public static Match FindPrevious(this MatchCollection matchCollection, int startIndex)
    {
        var previousWord = Match.Empty;
        foreach (Match match in matchCollection)
        {
            if (match.Index >= startIndex)
            {
                continue;
            }

            previousWord = match;
        }

        return previousWord;
    }
}